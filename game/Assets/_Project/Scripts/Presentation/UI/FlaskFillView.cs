using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 플라스크 용액 채우기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 버튼을 누르고 있으면 게이지 박스가 차오르고, 목표 구간(90~100%)에서
    /// 손을 떼야 완료한다.
    /// </summary>
    public sealed class FlaskFillView : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 200f;

        [SerializeField] private FlaskFillStation _station;
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
            FlaskFillStation station,
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
            FlaskFillStation station,
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
            GUI.Box(panelRect, "플라스크 용액 채우기");

            var progress = _station.Rules.GetProgressNormalized();
            var targetMin = _config != null
                ? _config.FlaskFillTargetMinNormalized
                : 0.9f;

            var barRect = new Rect(
                panelRect.x + 20f,
                panelRect.y + 40f,
                panelRect.width - 40f,
                24f);
            GUI.Box(barRect, string.Empty);

            var previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.4f, 0.5f);
            GUI.Box(
                new Rect(
                    barRect.x + barRect.width * targetMin,
                    barRect.y,
                    barRect.width * (1f - targetMin),
                    barRect.height),
                string.Empty);
            GUI.color = previousColor;

            var fillRect = new Rect(
                barRect.x,
                barRect.y,
                barRect.width * progress,
                barRect.height);
            GUI.Box(fillRect, $"{progress * 100f:0}%");

            var currentEvent = Event.current;
            var buttonRect = new Rect(
                panelRect.x + 60f,
                panelRect.y + 80f,
                panelRect.width - 120f,
                40f);
            GUI.Box(buttonRect, "[꼭지] 꾹 누르고 있기");

            var isPressed = currentEvent.type == EventType.MouseDown &&
                             buttonRect.Contains(currentEvent.mousePosition);
            var isReleased = currentEvent.type == EventType.MouseUp;
            if (isPressed)
            {
                _station.BeginHold(LocalPlayer);
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
            if (_station != null && LocalPlayer != null)
            {
                _station.EndHold(LocalPlayer);
            }
        }
    }
}
