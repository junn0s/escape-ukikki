using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 중앙 보안 광장의 ID 카드 긁기 미션이다(GDD §10.2). 카드를 리더기로
    /// 드래그하되 너무 빠르거나 느리면 실패한다. 실제 상태 전이는 서버가
    /// 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class IdCardSwipeStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.4f, 0.45f, 0.55f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, float> _externalSwipeRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private SwipeSpeedMissionRules _rules;

        public event Action<IdCardSwipeStation> StateChanged;
        public event Action<IdCardSwipeStation, GameObject> MissionOpened;

        public SwipeSpeedMissionRules Rules => _rules ??=
            new SwipeSpeedMissionRules(
                _config != null ? _config.IdCardSwipeMinSeconds : 0.4f,
                _config != null ? _config.IdCardSwipeMaxSeconds : 1.2f);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "출입 인증 완료"
                : "ID 카드 긁기";

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
            Action<GameObject, float> swipeRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalSwipeRequest = swipeRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalSwipeRequest = null;
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

        /// <summary>드래그를 마쳤을 때 걸린 시간(초)으로 판정을 요청한다.</summary>
        public void RequestSwipe(GameObject interactor, float durationSeconds)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalSwipeRequest?.Invoke(interactor, durationSeconds);
        }

        /// <summary>서버가 확정한 판정 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            bool isCompleted,
            int failedAttemptCount)
        {
            Rules.ApplyAuthoritativeSnapshot(isCompleted, failedAttemptCount);
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
