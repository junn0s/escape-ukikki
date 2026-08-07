using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 액체 보관실의 폐기물 통 압축 미션이다(GDD §10.2). 레버를 내린 채
    /// 마우스를 5초간 누르고 있는다. 백신 데이터 다운로드와 같은 조작을
    /// 공유한다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class WasteCompactorStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.4f, 0.35f, 0.3f, 1f);
        [SerializeField]
        private Color _holdingColor = new(0.7f, 0.55f, 0.2f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, bool> _externalHoldRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<WasteCompactorStation> StateChanged;
        public event Action<WasteCompactorStation, GameObject> MissionOpened;

        public HoldButtonMissionRules Rules { get; } = new();
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;
        public float RequiredSeconds =>
            _config != null ? _config.WasteCompactorHoldSeconds : 5f;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "압축 완료"
                : "폐기물 압축 (레버 누르고 있기)";

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
            float heldSeconds,
            bool isHolding,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(heldSeconds, isHolding, isCompleted);
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
