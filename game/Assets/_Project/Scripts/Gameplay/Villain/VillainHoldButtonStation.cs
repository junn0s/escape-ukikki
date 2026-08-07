using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 버튼을 8초간 누르고 있는 형태의 빌런 전용 미션이다(GDD §13.2). 배양액
    /// 오염시키기(실험실 A)와 환풍구 역류 조작(격리실 B)이 이 조작을 공유한다.
    /// 같은 자리의 생존자 미션과 겉모습을 공유하는 위장 오브젝트다. 실제 상태
    /// 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class VillainHoldButtonStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField, Min(0.1f)] private float _requiredHoldSeconds = 8f;
        [SerializeField] private VillainMissionKind _kind;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField]
        private Color _holdingColor = new(0.3f, 0.75f, 0.9f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.65f, 0.2f, 0.85f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, bool> _externalHoldRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<VillainHoldButtonStation> StateChanged;
        public event Action<VillainHoldButtonStation, GameObject> MissionOpened;

        public HoldButtonMissionRules Rules { get; } = new();
        public VillainMissionKind Kind => _kind;
        public string RoomId => _roomId;
        public float RequiredHoldSeconds => _requiredHoldSeconds;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        // 생존자에게는 위장 대상과 동일한 문구가 보인다. 실제 문구는 §13.2 표를 따른다.
        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "완료"
                : "다운로드 (누르고 있기)";

        public void Configure(
            SpriteRenderer stationRenderer,
            float requiredHoldSeconds,
            VillainMissionKind kind,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _requiredHoldSeconds = requiredHoldSeconds;
            _kind = kind;
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
            var canInteractLocally = isActiveAndEnabled && !Rules.IsCompleted;
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
