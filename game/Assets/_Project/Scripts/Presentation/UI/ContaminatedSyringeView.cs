using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 오염된 주사기 폐기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 주사기 박스를 휴지통 박스로 드래그한다.
    /// </summary>
    public sealed class ContaminatedSyringeView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 240f;
        private const float ItemSize = 48f;
        private const float TrashSize = 64f;

        [SerializeField] private ContaminatedSyringeStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

        private bool _isOpenBacking;
        private GameObject _localPlayer;

        /// <summary>
        /// 실제로 조작 중인 플레이어다. 네트워크 모드에서는 소유 플레이어가,
        /// 단독 재생에서는 씬의 프로토타입 플레이어가 된다.
        /// </summary>
        private GameObject LocalPlayer =>
            LocalGameplayPlayer.Resolve(_localPlayer);
        private int _draggedIndex = -1;
        private Vector2 _dragPosition;

        private bool _isOpen
        {
            get => _isOpenBacking;
            set
            {
                _isOpenBacking = value;
                MissionOverlayState.SetOpen(value);
            }
        }

        public void Configure(
            ContaminatedSyringeStation station,
            SurvivorMissionBalanceConfig config,
            GameObject localPlayer)
        {
            Unsubscribe();
            _station = station;
            _config = config;
            _localPlayer = localPlayer;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _isOpen = false;
            _draggedIndex = -1;
        }

        private void Subscribe()
        {
            if (_station != null)
            {
                _station.MissionOpened += HandleMissionOpened;
            }
        }

        private void Unsubscribe()
        {
            if (_station != null)
            {
                _station.MissionOpened -= HandleMissionOpened;
            }
        }

        private void HandleMissionOpened(
            ContaminatedSyringeStation station,
            GameObject interactor)
        {
            if (interactor == LocalPlayer)
            {
                _isOpen = true;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen || _station == null)
            {
                return;
            }

            if (_station.Rules.IsCompleted)
            {
                _isOpen = false;
                return;
            }

            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUI.Box(
                panelRect,
                $"오염된 주사기 폐기 ({_station.Rules.PlacedCount}/{_station.Rules.ItemCount})");

            var trashRect = new Rect(
                panelRect.x + panelRect.width - TrashSize - 24f,
                panelRect.y + panelRect.height - TrashSize - 24f,
                TrashSize,
                TrashSize);
            GUI.Box(trashRect, "휴지통");

            var currentEvent = Event.current;
            var itemCount = _station.Rules.ItemCount;
            for (var index = 0; index < itemCount; index++)
            {
                if (_station.Rules.IsPlaced(index))
                {
                    continue;
                }

                var itemRect = _draggedIndex == index
                    ? new Rect(
                        _dragPosition.x - ItemSize * 0.5f,
                        _dragPosition.y - ItemSize * 0.5f,
                        ItemSize,
                        ItemSize)
                    : new Rect(
                        panelRect.x + 24f + index * (ItemSize + 16f),
                        panelRect.y + 40f,
                        ItemSize,
                        ItemSize);

                GUI.Box(itemRect, "주사기");

                if (currentEvent.type == EventType.MouseDown &&
                    _draggedIndex < 0 &&
                    itemRect.Contains(currentEvent.mousePosition))
                {
                    _draggedIndex = index;
                    _dragPosition = currentEvent.mousePosition;
                }
            }

            if (_draggedIndex >= 0)
            {
                if (currentEvent.type == EventType.MouseDrag)
                {
                    _dragPosition = currentEvent.mousePosition;
                }
                else if (currentEvent.type == EventType.MouseUp)
                {
                    if (trashRect.Contains(currentEvent.mousePosition))
                    {
                        _station.PlaceItem(LocalPlayer, _draggedIndex);
                    }

                    _draggedIndex = -1;
                }
            }

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _draggedIndex = -1;
                _isOpen = false;
            }
        }
    }
}
