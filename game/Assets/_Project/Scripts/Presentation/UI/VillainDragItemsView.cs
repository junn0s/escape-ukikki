using UnityEngine;
using MonkeyLab.Gameplay.Villain;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 빌런 전용 드래그 배치 미션의 상자형 임시 UI다(GDD §13.2).
    /// 같은 자리의 생존자 미션과 동일한 상자 UI를 공유해 겉모습이 구분되지 않는다.
    /// </summary>
    public sealed class VillainDragItemsView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 240f;
        private const float ItemSize = 48f;
        private const float TargetSize = 64f;

        [SerializeField] private VillainDragItemsStation _station;

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
            VillainDragItemsStation station,
            GameObject localPlayer)
        {
            Unsubscribe();
            _station = station;
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
            VillainDragItemsStation station,
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
                $"환자 바이탈 기록 ({_station.Rules.PlacedCount}/{_station.Rules.ItemCount})");

            var targetRect = new Rect(
                panelRect.x + panelRect.width - TargetSize - 24f,
                panelRect.y + panelRect.height - TargetSize - 24f,
                TargetSize,
                TargetSize);
            GUI.Box(targetRect, "저장");

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

                GUI.Box(itemRect, "기록지");

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
                    if (targetRect.Contains(currentEvent.mousePosition))
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
