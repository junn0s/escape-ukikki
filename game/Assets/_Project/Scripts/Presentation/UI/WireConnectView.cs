using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 배선 복구 미션의 상자형 임시 UI다(GDD §10.2).
    /// 왼쪽 전선 박스를 같은 색 오른쪽 단자 박스로 드래그한다.
    /// </summary>
    public sealed class WireConnectView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 260f;
        private const float TerminalSize = 48f;

        private static readonly string[] ColorNames =
        {
            "빨강", "파랑", "노랑", "초록"
        };
        private static readonly Color[] WireColors =
        {
            new(0.8f, 0.2f, 0.2f, 1f),
            new(0.2f, 0.3f, 0.8f, 1f),
            new(0.85f, 0.75f, 0.2f, 1f),
            new(0.25f, 0.7f, 0.3f, 1f)
        };

        [SerializeField] private WireConnectStation _station;

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

        public void Configure(WireConnectStation station, GameObject localPlayer)
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
            WireConnectStation station,
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
                $"배선 복구 ({_station.Rules.ConnectedCount}/{_station.Rules.WireCount})");

            var currentEvent = Event.current;
            var wireCount = _station.Rules.WireCount;
            var terminalRects = new Rect[wireCount];

            for (var index = 0; index < wireCount; index++)
            {
                var colorIndex = _station.Rules.GetColor(index);
                var previousColor = GUI.color;
                terminalRects[index] = new Rect(
                    panelRect.x + panelRect.width - TerminalSize - 24f,
                    panelRect.y + 48f + index * (TerminalSize + 12f),
                    TerminalSize,
                    TerminalSize);
                GUI.color = colorIndex >= 0 && colorIndex < WireColors.Length
                    ? WireColors[colorIndex]
                    : previousColor;
                GUI.Box(
                    terminalRects[index],
                    colorIndex >= 0 && colorIndex < ColorNames.Length
                        ? ColorNames[colorIndex]
                        : string.Empty);
                GUI.color = previousColor;
            }

            for (var index = 0; index < wireCount; index++)
            {
                if (_station.Rules.IsConnected(index))
                {
                    continue;
                }

                var colorIndex = _station.Rules.GetColor(index);
                var wireRect = _draggedIndex == index
                    ? new Rect(
                        _dragPosition.x - TerminalSize * 0.5f,
                        _dragPosition.y - TerminalSize * 0.5f,
                        TerminalSize,
                        TerminalSize)
                    : new Rect(
                        panelRect.x + 24f,
                        panelRect.y + 48f + index * (TerminalSize + 12f),
                        TerminalSize,
                        TerminalSize);

                var previousColor = GUI.color;
                GUI.color = colorIndex >= 0 && colorIndex < WireColors.Length
                    ? WireColors[colorIndex]
                    : previousColor;
                GUI.Box(
                    wireRect,
                    colorIndex >= 0 && colorIndex < ColorNames.Length
                        ? ColorNames[colorIndex]
                        : string.Empty);
                GUI.color = previousColor;

                if (currentEvent.type == EventType.MouseDown &&
                    _draggedIndex < 0 &&
                    wireRect.Contains(currentEvent.mousePosition))
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
                    for (var index = 0; index < wireCount; index++)
                    {
                        if (terminalRects[index].Contains(
                                currentEvent.mousePosition))
                        {
                            _station.ConnectWire(
                                LocalPlayer,
                                _draggedIndex,
                                index);
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
