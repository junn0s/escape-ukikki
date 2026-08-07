using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// CCTV 화면 닦기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 화면 박스 안에서 마우스를 문지르듯 드래그할 때마다 진행률이 오른다.
    /// </summary>
    public sealed class CctvScreenCleaningView : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float PanelHeight = 260f;
        private const float ScrubMoveThreshold = 12f;

        [SerializeField] private CctvScreenCleaningStation _station;

        private bool _isOpenBacking;
        private GameObject _localPlayer;
        private Vector2 _lastMousePosition;
        private bool _hasLastMousePosition;

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
            CctvScreenCleaningStation station,
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
            _hasLastMousePosition = false;
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
            CctvScreenCleaningStation station,
            GameObject interactor)
        {
            if (interactor == _localPlayer)
            {
                _isOpen = true;
                _hasLastMousePosition = false;
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

            var progress = _station.Rules.GetProgressNormalized();
            var screenRect = new Rect(
                panelRect.x + 30f,
                panelRect.y + 40f,
                panelRect.width - 60f,
                panelRect.height - 100f);
            var previousColor = GUI.color;
            GUI.color = Color.Lerp(
                new Color(0.15f, 0.15f, 0.15f),
                new Color(0.7f, 0.85f, 0.95f),
                progress);
            GUI.Box(screenRect, $"{progress * 100f:0}%");
            GUI.color = previousColor;

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                screenRect.Contains(currentEvent.mousePosition))
            {
                _lastMousePosition = currentEvent.mousePosition;
                _hasLastMousePosition = true;
            }
            else if (currentEvent.type == EventType.MouseDrag &&
                     screenRect.Contains(currentEvent.mousePosition))
            {
                if (_hasLastMousePosition &&
                    Vector2.Distance(
                        currentEvent.mousePosition,
                        _lastMousePosition) >= ScrubMoveThreshold)
                {
                    _station.RequestScrub(_localPlayer);
                    _lastMousePosition = currentEvent.mousePosition;
                }
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                _hasLastMousePosition = false;
            }

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
            }
        }
    }
}
