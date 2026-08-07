using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실의 오염된 주사기 폐기 미션이다(GDD §10.2). 주사기 3개를 휴지통으로
    /// 드래그해서 없앤다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class ContaminatedSyringeStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.6f, 0.2f, 0.2f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalPlaceRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DragItemsMissionRules _rules;

        public event Action<ContaminatedSyringeStation> StateChanged;
        public event Action<ContaminatedSyringeStation, GameObject> MissionOpened;

        public DragItemsMissionRules Rules =>
            _rules ??= new DragItemsMissionRules(
                _config != null ? _config.ContaminatedSyringeCount : 3);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "폐기 완료"
                : $"주사기 폐기 ({Rules.PlacedCount}/{Rules.ItemCount})";

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
            var canInteractLocally = _config != null && isActiveAndEnabled &&
                                      !Rules.IsCompleted;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>주사기 하나를 휴지통으로 드래그해 놓았을 때 호출한다.</summary>
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
