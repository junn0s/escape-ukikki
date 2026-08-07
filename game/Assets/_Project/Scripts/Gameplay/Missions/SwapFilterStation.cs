using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 격리실 B의 공기 필터 교체 미션이다(GDD §10.2). 낡은 필터를 드래그해
    /// 빼고, 새 필터를 드래그해 꽂는다. 실제 상태 전이는 서버가 판정하고 이
    /// 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class SwapFilterStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.3f, 0.32f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, bool> _externalSwapRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<SwapFilterStation> StateChanged;
        public event Action<SwapFilterStation, GameObject> MissionOpened;

        public SwapFilterMissionRules Rules { get; } = new();
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "필터 교체 완료"
                : !Rules.IsOldFilterRemoved
                    ? "낡은 필터 빼기"
                    : "새 필터 꽂기";

        public void Configure(
            SpriteRenderer stationRenderer,
            SurvivorMissionBalanceConfig config,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _config = config;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject, bool> swapRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalSwapRequest = swapRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalSwapRequest = null;
        }

        public void ApplyInteractionFeedback(string feedback)
        {
            _interactionFeedback = feedback;
        }

        public void ClearInteractionFeedback()
        {
            _interactionFeedback = string.Empty;
        }

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally = _config != null && isActiveAndEnabled &&
                                      !Rules.IsCompleted;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>낡은 필터를 뺄 때는 false, 새 필터를 꽂을 때는 true로 요청한다.</summary>
        public void RequestSwap(GameObject interactor, bool isInstallingNew)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalSwapRequest?.Invoke(interactor, isInstallingNew);
        }

        /// <summary>서버가 확정한 교체 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            bool isOldFilterRemoved,
            bool isNewFilterInstalled)
        {
            Rules.Reset();
            if (isOldFilterRemoved)
            {
                Rules.TryRemoveOldFilter();
            }

            if (isNewFilterInstalled)
            {
                Rules.TryInstallNewFilter();
            }

            ClearInteractionFeedback();
            ApplyVisuals();
            StateChanged?.Invoke(this);
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
                    "[Mission] Survivor mission balance config is missing.",
                    this);
            }
        }

        private void ApplyVisuals()
        {
            if (_stationRenderer == null)
            {
                return;
            }

            _stationRenderer.color = Rules.IsCompleted
                ? _completedColor
                : _idleColor;
        }
    }
}
