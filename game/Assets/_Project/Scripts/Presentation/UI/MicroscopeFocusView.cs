using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 현미경 렌즈 초점 미션의 상자형 임시 UI다(GDD §10.2).
    /// 버튼으로 슬라이더 박스를 밀어 올려 초록 안전선 구간에서 확정한다.
    /// </summary>
    public sealed class MicroscopeFocusView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 260f;
        private const float TrackHeight = 180f;
        private const float MarkerSize = 20f;

        [SerializeField] private MicroscopeFocusStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

        private bool _isOpenBacking;
        private GameObject _localPlayer;

        /// <summary>
        /// 실제로 조작 중인 플레이어다. 네트워크 모드에서는 소유 플레이어가,
        /// 단독 재생에서는 씬의 프로토타입 플레이어가 된다.
        /// </summary>
        private GameObject LocalPlayer =>
            LocalGameplayPlayer.Resolve(_localPlayer);

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
            MicroscopeFocusStation station,
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
            MicroscopeFocusStation station,
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
            GUI.Box(panelRect, "현미경 렌즈 초점");

            var trackRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 20f,
                panelRect.y + 40f,
                40f,
                TrackHeight);
            GUI.Box(trackRect, string.Empty);

            var targetMin = _config != null
                ? _config.MicroscopeFocusTargetMinNormalized
                : 0.55f;
            var targetMax = _config != null
                ? _config.MicroscopeFocusTargetMaxNormalized
                : 0.7f;
            var targetRect = new Rect(
                trackRect.x - 10f,
                trackRect.y + trackRect.height * (1f - targetMax),
                trackRect.width + 20f,
                trackRect.height * (targetMax - targetMin));
            var previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.4f, 0.6f);
            GUI.Box(targetRect, string.Empty);
            GUI.color = previousColor;

            var markerY = trackRect.y +
                          trackRect.height * (1f - _station.Rules.PositionNormalized);
            var markerRect = new Rect(
                trackRect.x + trackRect.width * 0.5f - MarkerSize * 0.5f,
                markerY - MarkerSize * 0.5f,
                MarkerSize,
                MarkerSize);
            GUI.Box(markerRect, "●");

            var pushRate = _config != null
                ? _config.MicroscopeFocusPushPerSecond
                : 0.5f;
            var pushButtonRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 60f,
                panelRect.y + panelRect.height - 90f,
                120f,
                32f);
            if (GUI.RepeatButton(pushButtonRect, "▲ 밀어 올리기"))
            {
                _station.PushSlider(
                    LocalPlayer,
                    pushRate * Time.unscaledDeltaTime);
            }

            var confirmButtonRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 60f,
                panelRect.y + panelRect.height - 44f,
                120f,
                32f);
            if (GUI.Button(confirmButtonRect, "[확정]"))
            {
                _station.ConfirmFocus(LocalPlayer);
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
