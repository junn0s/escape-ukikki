using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 빌런 전용 강화 스테이션이다. 축 하나를 담당하며 채널링 방식으로 진행한다.
    /// docs/balance-and-telemetry.md §6에 따라 중단 시 즉시 초기화한다.
    /// </summary>
    public sealed class UpgradeStationPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer _stationRenderer;
        [SerializeField] private UpgradeBalanceConfig _config;
        [SerializeField] private UpgradeAxis _axis;
        [SerializeField] private Color _idleColor = new(0.65f, 0.2f, 0.85f, 1f);
        [SerializeField] private Color _channelingColor = new(1f, 0.45f, 0.1f, 1f);
        [SerializeField] private Color _maxedColor = new(0.35f, 0.35f, 0.4f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private GameObject _activeInteractor;
        private PlayerInputReader _activeInput;
        private PlayerMotor _activeMotor;
        private PlayerAimController _activeAim;
        private float _elapsedSeconds;
        private bool _isChanneling;
        private bool _isAxisMaxed;
        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;

        public event Action<UpgradeStationPrototype> ChannelStarted;
        public event Action<UpgradeStationPrototype> ProgressChanged;
        public event Action<UpgradeStationPrototype> ChannelCancelled;
        public event Action<UpgradeStationPrototype> ChannelCompleted;

        public string Prompt => _axis switch
        {
            UpgradeAxis.Scent => "후각 강화하기",
            UpgradeAxis.Population => "개체 강화하기",
            UpgradeAxis.Toxicity => "독성 강화하기",
            _ => "강화하기"
        };

        public Transform InteractionTransform => transform;
        public UpgradeAxis Axis => _axis;
        public UpgradeBalanceConfig Config => _config;
        public bool IsChanneling => _isChanneling;
        public bool IsAxisMaxed => _isAxisMaxed;
        public float RequiredSeconds =>
            _config != null ? _config.GetUpgradeMissionSeconds(_axis) : 0f;
        public float NormalizedProgress =>
            RequiredSeconds > 0f
                ? Mathf.Clamp01(_elapsedSeconds / RequiredSeconds)
                : 0f;

        public void Configure(
            Renderer stationRenderer,
            UpgradeBalanceConfig config,
            UpgradeAxis axis)
        {
            _stationRenderer = stationRenderer;
            _config = config;
            _axis = axis;
        }

        public void SetInteractionAuthority(
            Func<GameObject, bool> canInteract,
            Action<GameObject> requestInteraction)
        {
            _externalCanInteract = canInteract;
            _externalInteractionRequest = requestInteraction;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_externalInteractionRequest?.Target != authorityOwner)
            {
                return;
            }

            _externalCanInteract = null;
            _externalInteractionRequest = null;
        }

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally =
                !_isAxisMaxed &&
                !_isChanneling &&
                _config != null &&
                isActiveAndEnabled;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (_externalInteractionRequest != null)
            {
                _externalInteractionRequest.Invoke(interactor);
                return;
            }

            BeginApprovedInteraction(interactor);
        }

        public void BeginApprovedInteraction(GameObject interactor)
        {
            if (_isAxisMaxed || _isChanneling || _config == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            _activeInput = interactor.GetComponent<PlayerInputReader>();
            _activeMotor = interactor.GetComponent<PlayerMotor>();
            _activeAim = interactor.GetComponent<PlayerAimController>();
            if (_activeInput == null || _activeMotor == null ||
                _activeAim == null)
            {
                Debug.LogError(
                    "[Upgrade] Station requires player input, motor and aim components.",
                    this);
                ClearActivePlayer();
                return;
            }

            _activeInteractor = interactor;
            _elapsedSeconds = 0f;
            _isChanneling = true;
            _activeInput.CancelPressed += CancelChannel;
            SetPlayerControlEnabled(false);
            ApplyVisuals();
            ChannelStarted?.Invoke(this);
            Debug.Log(
                $"[Upgrade] {_axis} channel started by {interactor.name}.",
                this);
        }

        public void CancelChannel()
        {
            if (!_isChanneling)
            {
                return;
            }

            _isChanneling = false;
            _elapsedSeconds = 0f;
            ReleasePlayer();
            ApplyVisuals();
            ChannelCancelled?.Invoke(this);
            Debug.Log($"[Upgrade] {_axis} channel cancelled and reset.", this);
        }

        /// <summary>
        /// 서버가 축의 최대 단계 도달을 통보하면 더 이상 상호작용을 받지 않는다.
        /// </summary>
        public void ApplyAxisMaxed()
        {
            if (_isChanneling)
            {
                CancelChannel();
            }

            _isAxisMaxed = true;
            ApplyVisuals();
        }

        public void ApplyAuthoritativeCompletion()
        {
            if (_isChanneling)
            {
                _isChanneling = false;
                _elapsedSeconds = 0f;
                ReleasePlayer();
            }

            ApplyVisuals();
        }

        private void Update()
        {
            if (!_isChanneling || _config == null)
            {
                return;
            }

            if (_activeInteractor == null)
            {
                CancelChannel();
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            ProgressChanged?.Invoke(this);
            if (_elapsedSeconds < RequiredSeconds)
            {
                return;
            }

            _isChanneling = false;
            _elapsedSeconds = 0f;
            ReleasePlayer();
            ApplyVisuals();
            ChannelCompleted?.Invoke(this);
        }

        private void OnDisable()
        {
            if (_isChanneling)
            {
                _isChanneling = false;
                _elapsedSeconds = 0f;
            }

            ReleasePlayer();
        }

        private void ApplyVisuals()
        {
            if (_stationRenderer == null)
            {
                return;
            }

            var color = _isAxisMaxed
                ? _maxedColor
                : _isChanneling
                    ? _channelingColor
                    : _idleColor;
            if (_stationRenderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = color;
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _stationRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _stationRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ReleasePlayer()
        {
            if (_activeInput != null)
            {
                _activeInput.CancelPressed -= CancelChannel;
            }

            SetPlayerControlEnabled(true);
            ClearActivePlayer();
        }

        private void SetPlayerControlEnabled(bool isEnabled)
        {
            _activeMotor?.SetMovementEnabled(isEnabled);
            _activeAim?.SetAimingEnabled(isEnabled);
        }

        private void ClearActivePlayer()
        {
            _activeInteractor = null;
            _activeInput = null;
            _activeMotor = null;
            _activeAim = null;
        }
    }
}
