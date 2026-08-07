using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 폐기물 통 압축 미션의 상자형 임시 UI다(GDD §10.2).
    /// 레버 박스를 마우스로 누르고 있는다.
    /// </summary>
    public sealed class WasteCompactorView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 140f;

        [SerializeField] private WasteCompactorStation _station;

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
            WasteCompactorStation station,
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
            ReleaseIfHolding();
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
            WasteCompactorStation station,
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

            GUI.Box(panelRect, "폐기물 통 압축");

            var progress = _station.Rules.GetProgressNormalized(
                _station.RequiredSeconds);
            var barRect = new Rect(
                panelRect.x + 20f,
                panelRect.y + 40f,
                panelRect.width - 40f,
                24f);
            GUI.Box(barRect, string.Empty);
            var fillRect = new Rect(
                barRect.x,
                barRect.y,
                barRect.width * progress,
                barRect.height);
            GUI.Box(fillRect, string.Empty);

            var currentEvent = Event.current;
            var buttonRect = new Rect(
                panelRect.x + 60f,
                panelRect.y + 80f,
                panelRect.width - 120f,
                40f);
            GUI.Box(buttonRect, "[레버] 꾹 누르고 있기");

            var isPressed = currentEvent.type == EventType.MouseDown &&
                             buttonRect.Contains(currentEvent.mousePosition);
            var isReleased = currentEvent.type == EventType.MouseUp;
            if (isPressed)
            {
                _station.BeginHold(_localPlayer);
            }
            else if (isReleased)
            {
                ReleaseIfHolding();
            }

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                ReleaseIfHolding();
                _isOpen = false;
            }
        }

        private void ReleaseIfHolding()
        {
            if (_station != null && _localPlayer != null)
            {
                _station.EndHold(_localPlayer);
            }
        }
    }
}
