using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 차단기 올리기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 스위치 박스 4개를 클릭해 올린다.
    /// </summary>
    public sealed class CircuitBreakerView : MonoBehaviour
    {
        private const float PanelWidth = 400f;
        private const float PanelHeight = 200f;
        private const float SwitchSize = 56f;

        [SerializeField] private CircuitBreakerStation _station;

        private bool _isOpenBacking;
        private GameObject _localPlayer;

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
            CircuitBreakerStation station,
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
            CircuitBreakerStation station,
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
                $"차단기 올리기 ({_station.Rules.PlacedCount}/{_station.Rules.ItemCount})");

            var currentEvent = Event.current;
            var switchCount = _station.Rules.ItemCount;
            var spacing = 16f;
            var totalWidth = switchCount * SwitchSize + (switchCount - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;

            for (var index = 0; index < switchCount; index++)
            {
                var isUp = _station.Rules.IsPlaced(index);
                var switchRect = new Rect(
                    startX + index * (SwitchSize + spacing),
                    panelRect.y + 70f,
                    SwitchSize,
                    SwitchSize);

                var previousColor = GUI.color;
                GUI.color = isUp
                    ? new Color(0.3f, 0.9f, 0.45f)
                    : new Color(0.6f, 0.3f, 0.3f);
                GUI.Box(switchRect, isUp ? "▲ ON" : "▼ OFF");
                GUI.color = previousColor;

                if (!isUp &&
                    currentEvent.type == EventType.MouseDown &&
                    switchRect.Contains(currentEvent.mousePosition))
                {
                    _station.FlipSwitch(_localPlayer, index);
                }
            }

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
            }
        }
    }
}
