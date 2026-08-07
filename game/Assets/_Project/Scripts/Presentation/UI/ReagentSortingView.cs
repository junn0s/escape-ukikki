using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 시약병 분류 미션의 상자형 임시 UI다(GDD §10.2).
    /// 시약병 박스를 색이 맞는 칸 박스로 드래그한다.
    /// </summary>
    public sealed class ReagentSortingView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 260f;
        private const float ItemSize = 48f;
        private const float BinSize = 64f;

        private static readonly string[] ReagentNames = { "빨강", "파랑", "노랑" };
        private static readonly Color[] ReagentColors =
        {
            new(0.8f, 0.2f, 0.2f, 1f),
            new(0.2f, 0.3f, 0.8f, 1f),
            new(0.85f, 0.75f, 0.2f, 1f)
        };

        [SerializeField] private ReagentSortingStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

        private bool _isOpenBacking;
        private GameObject _localPlayer;
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
            ReagentSortingStation station,
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
            ReagentSortingStation station,
            GameObject interactor)
        {
            if (interactor == _localPlayer)
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
                $"시약병 분류 ({_station.Rules.SortedCount}/{_station.Rules.ReagentCount})");

            var currentEvent = Event.current;
            var binCount = ReagentNames.Length;
            var bins = new Rect[binCount];
            for (var binIndex = 0; binIndex < binCount; binIndex++)
            {
                var previousColor = GUI.color;
                bins[binIndex] = new Rect(
                    panelRect.x + 24f + binIndex * (BinSize + 24f),
                    panelRect.y + panelRect.height - BinSize - 24f,
                    BinSize,
                    BinSize);
                GUI.color = ReagentColors[binIndex];
                GUI.Box(bins[binIndex], ReagentNames[binIndex]);
                GUI.color = previousColor;
            }

            var reagentCount = _station.Rules.ReagentCount;
            for (var index = 0; index < reagentCount; index++)
            {
                if (_station.Rules.IsSorted(index))
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
                        panelRect.y + 48f,
                        ItemSize,
                        ItemSize);

                var targetBin = _station.Rules.GetTargetBinIndex(index);
                var previousColor = GUI.color;
                GUI.color = targetBin >= 0 && targetBin < ReagentColors.Length
                    ? ReagentColors[targetBin]
                    : previousColor;
                GUI.Box(itemRect, "시약병");
                GUI.color = previousColor;

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
                    for (var binIndex = 0; binIndex < binCount; binIndex++)
                    {
                        if (bins[binIndex].Contains(currentEvent.mousePosition))
                        {
                            _station.PlaceReagent(
                                _localPlayer,
                                _draggedIndex,
                                binIndex);
                            break;
                        }
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
