using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 실험실 B의 플라스크 용액 채우기 미션이다(GDD §10.2). 버튼을 누르고
    /// 있다가 게이지가 목표 구간(90~100%)에 있을 때 정확히 손을 뗀다. 실제
    /// 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class FlaskFillStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.4f, 0.45f, 1f);
        [SerializeField]
        private Color _holdingColor = new(0.3f, 0.75f, 0.9f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, bool> _externalHoldRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private FillGaugeMissionRules _rules;

        public event Action<FlaskFillStation> StateChanged;
        public event Action<FlaskFillStation, GameObject> MissionOpened;

        public FillGaugeMissionRules Rules => _rules ??=
            new FillGaugeMissionRules(
                _config != null ? _config.FlaskFillTargetMinNormalized : 0.9f,
                _config != null ? _config.FlaskFillTargetMaxNormalized : 1f,
                _config != null ? _config.FlaskFillDurationSeconds : 4f);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "용액 채우기 완료"
                : "플라스크 용액 채우기 (누르고 있기)";

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
            Action<GameObject, bool> holdRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalHoldRequest = holdRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalHoldRequest = null;
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

        public void BeginHold(GameObject interactor)
        {
            SetHolding(interactor, true);
        }

        public void EndHold(GameObject interactor)
        {
            SetHolding(interactor, false);
        }

        private void SetHolding(GameObject interactor, bool isHolding)
        {
            if (isHolding && !CanInteract(interactor))
            {
                return;
            }

            _externalHoldRequest?.Invoke(interactor, isHolding);
        }

        /// <summary>서버가 확정한 진행 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            float filledSeconds,
            bool isHolding,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(filledSeconds, isHolding, isCompleted);
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
                : Rules.IsHolding
                    ? _holdingColor
                    : _idleColor;
        }
    }
}
