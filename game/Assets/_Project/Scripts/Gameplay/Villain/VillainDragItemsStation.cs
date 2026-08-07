using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 아이템 여러 개를 목표 지점으로 드래그하는 형태의 빌런 전용 미션이다
    /// (GDD §13.2). 투약 기록 삭제(입원실)가 이 조작을 쓴다. 같은 자리의
    /// 생존자 미션과 겉모습을 공유하는 위장 오브젝트다. 실제 상태 전이는
    /// 서버가 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class VillainDragItemsStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField, Min(1)] private int _itemCount = 3;
        [SerializeField] private VillainMissionKind _kind;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.4f, 0.4f, 0.55f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.65f, 0.2f, 0.85f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalPlaceRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DragItemsMissionRules _rules;

        public event Action<VillainDragItemsStation> StateChanged;
        public event Action<VillainDragItemsStation, GameObject> MissionOpened;

        public DragItemsMissionRules Rules => _rules ??=
            new DragItemsMissionRules(_itemCount);
        public VillainMissionKind Kind => _kind;
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        // 생존자에게는 위장 대상과 동일한 문구가 보인다.
        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "완료"
                : $"바이탈 기록 ({Rules.PlacedCount}/{Rules.ItemCount})";

        public void Configure(
            SpriteRenderer stationRenderer,
            int itemCount,
            VillainMissionKind kind,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _itemCount = itemCount;
            _kind = kind;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject, int> placeRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalPlaceRequest = placeRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalPlaceRequest = null;
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

        /// <summary>아이템 하나를 목표 지점으로 드래그해 놓았을 때 호출한다.</summary>
        public void PlaceItem(GameObject interactor, int itemIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalPlaceRequest?.Invoke(interactor, itemIndex);
        }

        /// <summary>서버가 확정한 배치 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] placedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < placedFlags.Length; index++)
            {
                if (placedFlags[index])
                {
                    Rules.TryPlaceItem(index);
                }
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
