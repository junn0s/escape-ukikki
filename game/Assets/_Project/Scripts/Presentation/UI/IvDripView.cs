using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 수액 속도 조절 미션의 상자형 임시 UI다(GDD §10.2).
    /// 오르내리는 슬라이더 박스를 목표 초록선 구간에서 클릭으로 멈춘다.
    /// </summary>
    public sealed class IvDripView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 260f;
        private const float TrackHeight = 180f;
        private const float MarkerSize = 20f;

        [SerializeField] private IvDripStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

        private bool _isOpenBacking;
        private GameObject _localPlayer;
        private float _localElapsedSeconds;

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
            IvDripStation station,
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
            IvDripStation station,
            GameObject interactor)
        {
            if (interactor == _localPlayer)
            {
                _isOpen = true;
                _localElapsedSeconds = 0f;
            }
        }

        private void Update()
        {
            if (_isOpen && _station != null && !_station.Rules.IsCompleted)
            {
                _localElapsedSeconds += Time.deltaTime;
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
            GUI.Box(panelRect, "수액 속도 조절");

            var trackRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 20f,
                panelRect.y + 40f,
                40f,
                TrackHeight);
            GUI.Box(trackRect, string.Empty);

            var halfWidth = _config != null
                ? _config.IvDripTargetHalfWidthNormalized
                : 0.08f;
            var targetRect = new Rect(
                trackRect.x - 10f,
                trackRect.y + trackRect.height * (0.5f - halfWidth),
                trackRect.width + 20f,
                trackRect.height * halfWidth * 2f);
            var previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.4f, 0.6f);
            GUI.Box(targetRect, string.Empty);
            GUI.color = previousColor;

            var position = _station.GetCurrentPositionNormalized(
                _localElapsedSeconds);
            var markerRect = new Rect(
                trackRect.x + trackRect.width * 0.5f - MarkerSize * 0.5f,
                trackRect.y + trackRect.height * position - MarkerSize * 0.5f,
                MarkerSize,
                MarkerSize);
            GUI.Box(markerRect, "●");

            var buttonRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 60f,
                panelRect.y + panelRect.height - 44f,
                120f,
                32f);
            if (GUI.Button(buttonRect, "[정지]"))
            {
                _station.RequestStop(_localPlayer, _localElapsedSeconds);
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
            }
        }
    }
}
