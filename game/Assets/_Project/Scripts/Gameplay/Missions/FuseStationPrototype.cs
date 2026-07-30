using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    public sealed class FuseStationPrototype : MonoBehaviour, IInteractable
    {
        private const float RestoredIndicatorIntensity = 4f;

        [SerializeField] private Renderer _stationRenderer;
        [SerializeField] private Light _indicatorLight;
        [SerializeField] private FuseMissionConfig _config;
        [SerializeField] private MissionPrototypeKind _kind;
        [SerializeField] private Color _restoredColor = new(0.15f, 1f, 0.35f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private FuseMissionInstance _mission;
        private GameObject _activeInteractor;
        private PlayerInputReader _activeInput;
        private PlayerMotor _activeMotor;
        private PlayerAimController _activeAim;
        private bool _isRestored;
        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;

        public event Action<FuseStationPrototype> MissionStarted;
        public event Action<FuseStationPrototype> ProgressChanged;
        public event Action<FuseStationPrototype, int, int> MissionFailed;
        public event Action<FuseStationPrototype> MissionCancelled;
        public event Action<FuseStationPrototype> MissionCompleted;

        public string Prompt => _kind switch
        {
            MissionPrototypeKind.FuseSequence => "퓨즈 순서 맞추기",
            MissionPrototypeKind.BreakerSequence => "차단기 기동하기",
            MissionPrototypeKind.CctvReboot => "CCTV 재부팅하기",
            MissionPrototypeKind.SampleSorting => "시료 분류하기",
            _ => "미션 수행하기"
        };
        public Transform InteractionTransform => transform;
        public MissionState State => _mission?.State ?? MissionState.Assigned;
        public IReadOnlyList<int> RequiredOrder => _mission?.RequiredOrder ?? Array.Empty<int>();
        public int ProgressIndex => _mission?.ProgressIndex ?? 0;
        public int FuseCount => _config != null ? _config.FuseCount : 0;
        public bool IsMissionActive => State == MissionState.InProgress;
        public bool IsRestored => _isRestored;
        public FuseMissionConfig Config => _config;
        public MissionPrototypeKind Kind => _kind;

        public void Configure(
            Renderer stationRenderer,
            Light indicatorLight,
            FuseMissionConfig config)
        {
            Configure(
                stationRenderer,
                indicatorLight,
                config,
                MissionPrototypeKind.FuseSequence);
        }

        public void Configure(
            Renderer stationRenderer,
            Light indicatorLight,
            FuseMissionConfig config,
            MissionPrototypeKind kind)
        {
            _stationRenderer = stationRenderer;
            _indicatorLight = indicatorLight;
            _config = config;
            _kind = kind;
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
                !_isRestored &&
                !IsMissionActive &&
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
            if (_isRestored || IsMissionActive || _config == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            _activeInput = interactor.GetComponent<PlayerInputReader>();
            _activeMotor = interactor.GetComponent<PlayerMotor>();
            _activeAim = interactor.GetComponent<PlayerAimController>();
            if (_activeInput == null || _activeMotor == null || _activeAim == null)
            {
                Debug.LogError(
                    $"[Mission] {_kind} requires player input, motor and aim components.",
                    this);
                ClearActivePlayer();
                return;
            }

            _activeInteractor = interactor;
            _mission = new FuseMissionInstance(CreateShuffledOrder(_config.FuseCount));
            _mission.Begin();
            _activeInput.CancelPressed += CancelMission;
            SetPlayerControlEnabled(false);
            MissionStarted?.Invoke(this);
            Debug.Log(
                $"[Mission] {_kind} started by {interactor.name}.",
                this);
        }

        public void ApplyAuthoritativeCompletion()
        {
            if (_isRestored)
            {
                return;
            }

            if (IsMissionActive)
            {
                _mission.Cancel();
                ReleasePlayer();
                _mission = null;
            }

            _isRestored = true;
            ApplyRestoredVisuals();
        }

        public void SubmitFuse(int fuseId)
        {
            if (_mission == null)
            {
                return;
            }

            var expectedFuseId = ProgressIndex < RequiredOrder.Count
                ? RequiredOrder[ProgressIndex]
                : 0;
            var result = _mission.SubmitFuse(fuseId);
            switch (result)
            {
                case FuseMissionInputResult.Accepted:
                    ProgressChanged?.Invoke(this);
                    break;
                case FuseMissionInputResult.Failed:
                    HandleFailure(fuseId, expectedFuseId);
                    break;
                case FuseMissionInputResult.Completed:
                    HandleCompletion();
                    break;
            }
        }

        public bool IsFuseInserted(int fuseId)
        {
            if (_mission == null)
            {
                return false;
            }

            for (var index = 0; index < ProgressIndex; index++)
            {
                if (RequiredOrder[index] == fuseId)
                {
                    return true;
                }
            }

            return false;
        }

        public void CancelMission()
        {
            if (_mission == null || _mission.State != MissionState.InProgress)
            {
                return;
            }

            _mission.Cancel();
            MissionCancelled?.Invoke(this);
            Debug.Log($"[Mission] {_kind} cancelled and reset.", this);
            ReleasePlayer();
            _mission = null;
        }

        private void OnDisable()
        {
            if (IsMissionActive)
            {
                _mission.Cancel();
            }

            ReleasePlayer();
            if (!_isRestored)
            {
                _mission = null;
            }
        }

        private void HandleFailure(int submittedFuseId, int expectedFuseId)
        {
            var interactorName = _activeInteractor != null ? _activeInteractor.name : "Unknown";
            MissionFailed?.Invoke(this, submittedFuseId, expectedFuseId);
            Debug.Log(
                $"[Mission] {_kind} failed by {interactorName}: expected {expectedFuseId}, received {submittedFuseId}.",
                this);
            ReleasePlayer();
            _mission = null;
        }

        private void HandleCompletion()
        {
            _isRestored = true;
            ApplyRestoredVisuals();
            MissionCompleted?.Invoke(this);
            var interactorName = _activeInteractor != null ? _activeInteractor.name : "Unknown";
            Debug.Log(
                $"[Mission] {_kind} completed by {interactorName}.",
                this);
            ReleasePlayer();
        }

        private void ApplyRestoredVisuals()
        {
            if (_stationRenderer != null)
            {
                if (_stationRenderer is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.color = _restoredColor;
                }
                else
                {
                    _propertyBlock ??= new MaterialPropertyBlock();
                    _stationRenderer.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor("_BaseColor", _restoredColor);
                    _stationRenderer.SetPropertyBlock(_propertyBlock);
                }
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = _restoredColor;
                _indicatorLight.intensity = RestoredIndicatorIntensity;
            }
        }

        private void ReleasePlayer()
        {
            if (_activeInput != null)
            {
                _activeInput.CancelPressed -= CancelMission;
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

        private static int[] CreateShuffledOrder(int fuseCount)
        {
            var order = new int[fuseCount];
            for (var index = 0; index < fuseCount; index++)
            {
                order[index] = index + 1;
            }

            for (var index = order.Length - 1; index > 0; index--)
            {
                var swapIndex = UnityEngine.Random.Range(0, index + 1);
                (order[index], order[swapIndex]) = (order[swapIndex], order[index]);
            }

            return order;
        }
    }
}
