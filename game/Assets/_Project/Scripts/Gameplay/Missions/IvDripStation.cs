using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 입원실의 수액 속도 조절 미션이다(GDD §10.2). 오르내리는 슬라이더를 중앙
    /// 초록선에서 클릭으로 멈춘다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는
    /// 표시와 요청만 담당한다.
    /// </summary>
    public sealed class IvDripStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.6f, 0.7f, 0.75f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, float> _externalStopRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private TimingStopMissionRules _rules;
        private float _elapsedSeconds;

        public event Action<IvDripStation> StateChanged;
        public event Action<IvDripStation, GameObject> MissionOpened;

        public TimingStopMissionRules Rules => _rules ??=
            new TimingStopMissionRules(
                0.5f - HalfWidth,
                0.5f + HalfWidth,
                _config != null ? _config.IvDripCycleSeconds : 2f);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;
        public float CycleSeconds =>
            _config != null ? _config.IvDripCycleSeconds : 2f;

        private float HalfWidth =>
            _config != null ? _config.IvDripTargetHalfWidthNormalized : 0.08f;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "수액 속도 조절 완료"
                : "수액 속도 조절";

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
            Action<GameObject, float> stopRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalStopRequest = stopRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalStopRequest = null;
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

        /// <summary>클라이언트가 관측한 경과 시각으로 정지를 요청한다.</summary>
        public void RequestStop(GameObject interactor, float elapsedSeconds)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalStopRequest?.Invoke(interactor, elapsedSeconds);
        }

        /// <summary>현재 슬라이더 위치를 계산한다. 뷰가 매 프레임 호출한다.</summary>
        public float GetCurrentPositionNormalized(float serverElapsedSeconds)
        {
            return Rules.GetPositionNormalized(serverElapsedSeconds);
        }

        /// <summary>서버가 확정한 완료 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(isCompleted);
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
