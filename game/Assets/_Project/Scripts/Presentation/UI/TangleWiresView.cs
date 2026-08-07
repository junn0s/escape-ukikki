using UnityEngine;
using MonkeyLab.Gameplay.Villain;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 보안 카메라 선 꼬기 미션의 상자형 임시 UI다(GDD §13.2).
    /// 전선 박스 4개를 색 구분 없이 모두 '단락' 단자 박스로 드래그한다.
    /// 같은 자리의 CCTV 화면 닦기와 동일한 상자 UI를 공유해 겉모습이
    /// 구분되지 않는다.
    /// </summary>
    public sealed class TangleWiresView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 240f;
        private const float WireSize = 48f;
        private const float TargetSize = 64f;

        [SerializeField] private TangleWiresStation _station;

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

        public void Configure(TangleWiresStation station, GameObject localPlayer)
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
            TangleWiresStation station,
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
            GUI.Box(panelRect, "CCTV 화면 닦기");

            var targetRect = new Rect(
                panelRect.x + panelRect.width - TargetSize - 24f,
                panelRect.y + panelRect.height - TargetSize - 24f,
                TargetSize,
                TargetSize);
            var previousColor = GUI.color;
            GUI.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            GUI.Box(targetRect, "단락");
            GUI.color = previousColor;

            var currentEvent = Event.current;
            var wireCount = _station.Rules.WireCount;
            for (var index = 0; index < wireCount; index++)
            {
                if (_station.Rules.IsPlugged(index))
                {
                    continue;
                }

                var wireRect = _draggedIndex == index
                    ? new Rect(
                        _dragPosition.x - WireSize * 0.5f,
                        _dragPosition.y - WireSize * 0.5f,
                        WireSize,
                        WireSize)
                    : new Rect(
                        panelRect.x + 24f + index * (WireSize + 16f),
                        panelRect.y + 40f,
                        WireSize,
                        WireSize);

                GUI.Box(wireRect, "전선");

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
                    if (targetRect.Contains(currentEvent.mousePosition))
                    {
                        _station.PlugWire(_localPlayer, _draggedIndex);
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
