using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 중앙 보안 광장의 CCTV 화면 닦기 미션이다(GDD §10.2). 지저분한 모니터를
    /// 마우스로 문질러 깨끗하게 만든다. 실제 상태 전이는 서버가 판정하고
    /// 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class CctvScreenCleaningStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalScrubRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private ScrubProgressMissionRules _rules;

        public event Action<CctvScreenCleaningStation> StateChanged;
        public event Action<CctvScreenCleaningStation, GameObject> MissionOpened;

        public ScrubProgressMissionRules Rules => _rules ??=
            new ScrubProgressMissionRules(
                _config != null ? _config.CctvScreenScrubCount : 10);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "화면 세척 완료"
                : "CCTV 화면 닦기";

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
            Action<GameObject> scrubRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalScrubRequest = scrubRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalScrubRequest = null;
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

        /// <summary>마우스로 화면을 문지를 때마다 호출한다.</summary>
        public void RequestScrub(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalScrubRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 진행 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(int scrubCount, bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(scrubCount, isCompleted);
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
