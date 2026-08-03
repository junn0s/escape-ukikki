using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 백신실 해독제 제작기다. 백신실 A와 B가 각각 독립된 한 대를 가진다(GDD §14.3).
    /// 제작 시작 뒤에는 자리를 떠나도 진행되며, 완성품은 누구나 먼저 가져갈 수 있다.
    /// 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class AntidoteFabricatorPrototype : MonoBehaviour,
        IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private AntidoteBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private string _displayName = "백신 제작기";
        [SerializeField] private Color _idleColor = new(0.2f, 0.6f, 0.8f, 1f);
        [SerializeField]
        private Color _producingColor = new(0.85f, 0.7f, 0.2f, 1f);
        [SerializeField] private Color _readyColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<AntidoteFabricatorPrototype> StateChanged;

        public AntidoteFabricator Fabricator { get; } = new();
        public AntidoteBalanceConfig Config => _config;
        public string RoomId => _roomId;
        public string DisplayName => _displayName;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Fabricator.State switch
            {
                FabricatorState.Idle => "해독제 제작 시작",
                FabricatorState.Producing =>
                    $"제작 중 {FormatRemaining(Fabricator.RemainingSeconds)}",
                FabricatorState.Ready => "해독제 가져가기",
                _ => "제작기"
            };

        public void Configure(
            SpriteRenderer stationRenderer,
            AntidoteBalanceConfig config,
            string roomId,
            string displayName)
        {
            _stationRenderer = stationRenderer;
            _config = config;
            _roomId = roomId;
            _displayName = displayName;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject> requestInteraction)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalInteractionRequest = requestInteraction;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalInteractionRequest = null;
        }

        public void ApplyInteractionFeedback(
            AntidoteRejectionReason rejectionReason)
        {
            _interactionFeedback =
                AntidoteInteractionFeedback.ToPrompt(rejectionReason);
        }

        public void ClearInteractionFeedback()
        {
            _interactionFeedback = string.Empty;
        }

        /// <summary>
        /// Producing 상태에서도 참을 반환한다. <c>PlayerInteractor</c>가 이 값으로
        /// 프롬프트 대상을 고르기 때문에, 거짓을 주면 남은 시간을 볼 수 없다.
        /// 실제 조작 차단은 <see cref="Interact"/>와 서버 검증이 담당한다(SDD §12.1).
        /// </summary>
        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally = _config != null && isActiveAndEnabled;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            // Producing 상태에서는 남은 시간 확인만 가능하다(SDD §12.1).
            if (!CanInteract(interactor) ||
                Fabricator.State == FabricatorState.Producing)
            {
                return;
            }

            // 제작 시작인지 완성품 획득인지는 서버가 상태를 보고 결정한다.
            _externalInteractionRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            FabricatorState state,
            float remainingSeconds,
            float totalDurationSeconds)
        {
            Fabricator.ApplyAuthoritativeSnapshot(
                state,
                remainingSeconds,
                totalDurationSeconds);
            ApplyVisuals();
        }

        public static string FormatRemaining(float remainingSeconds)
        {
            var clamped = Mathf.Max(0f, remainingSeconds);
            var minutes = Mathf.FloorToInt(clamped / 60f);
            var seconds = Mathf.FloorToInt(clamped % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        private void Awake()
        {
            if (_stationRenderer == null)
            {
                _stationRenderer = GetComponent<SpriteRenderer>();
            }

            if (_config == null)
            {
                Debug.LogError(
                    "[Antidote] Fabricator balance config is missing.",
                    this);
            }
        }

        private void OnEnable()
        {
            Fabricator.StateChanged += HandleFabricatorStateChanged;
            ApplyVisuals();
        }

        private void OnDisable()
        {
            Fabricator.StateChanged -= HandleFabricatorStateChanged;
        }

        private void HandleFabricatorStateChanged(AntidoteFabricator fabricator)
        {
            ClearInteractionFeedback();
            ApplyVisuals();
            StateChanged?.Invoke(this);
        }

        private void ApplyVisuals()
        {
            if (_stationRenderer == null)
            {
                return;
            }

            _stationRenderer.color = Fabricator.State switch
            {
                FabricatorState.Producing => _producingColor,
                FabricatorState.Ready => _readyColor,
                _ => _idleColor
            };
        }
    }
}
