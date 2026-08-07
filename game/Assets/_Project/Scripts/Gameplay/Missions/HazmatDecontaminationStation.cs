using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 격리실 A의 방호복 소독 미션이다(GDD §10.2). 버튼을 누르면 6초간 화면
    /// 전체가 김으로 가려져 시야가 완전히 막힌다. 한 번 시작하면 중단 없이
    /// 끝까지 진행된다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class HazmatDecontaminationStation :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.5f, 0.55f, 0.6f, 1f);
        [SerializeField]
        private Color _runningColor = new(0.75f, 0.85f, 0.9f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalStartRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<HazmatDecontaminationStation> StateChanged;
        public event
            Action<HazmatDecontaminationStation, GameObject> MissionOpened;

        public TimedBlindMissionRules Rules { get; } = new();
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;
        public float RequiredSeconds =>
            _config != null ? _config.HazmatDecontaminationSeconds : 6f;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "소독 완료"
                : Rules.IsRunning
                    ? "소독 중..."
                    : "방호복 소독 시작";

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
            Action<GameObject> startRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalStartRequest = startRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalStartRequest = null;
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
                                      !Rules.IsRunning && !Rules.IsCompleted;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>소독 버튼을 눌러 시작을 요청한다.</summary>
        public void StartDecontamination(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalStartRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 진행 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            float elapsedSeconds,
            bool isRunning,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(
                elapsedSeconds,
                isRunning,
                isCompleted);
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
                : Rules.IsRunning
                    ? _runningColor
                    : _idleColor;
        }
    }
}
