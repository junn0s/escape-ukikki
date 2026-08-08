using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 냉동고 온도 조절 미션의 상자형 임시 UI다(GDD §10.2). 위/아래 버튼으로
    /// 목표 온도에 맞추고 일정 시간 유지한다.
    /// </summary>
    public sealed class FreezerTemperatureView : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 240f;

        [SerializeField] private FreezerTemperatureStation _station;
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
            FreezerTemperatureStation station,
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
            FreezerTemperatureStation station,
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
            GUI.Box(panelRect, "냉동고 온도 조절");

            var targetTemperature = _config != null
                ? _config.FreezerTargetTemperature
                : -20;
            var requiredSeconds = _config != null
                ? _config.FreezerHoldSeconds
                : 3f;

            var readoutRect = new Rect(
                panelRect.x + 40f,
                panelRect.y + 44f,
                panelRect.width - 80f,
                48f);
            var previousColor = GUI.color;
            GUI.color = _station.Rules.IsAtTarget
                ? new Color(0.3f, 0.9f, 0.45f)
                : new Color(0.85f, 0.85f, 0.9f);
            GUI.Box(
                readoutRect,
                $"{_station.Rules.CurrentTemperature}℃  (목표 {targetTemperature}℃)");
            GUI.color = previousColor;

            var downRect = new Rect(
                panelRect.x + 30f,
                panelRect.y + 104f,
                80f,
                40f);
            if (GUI.Button(downRect, "▼ 낮추기"))
            {
                _station.AdjustTemperature(LocalPlayer, -1);
            }

            var upRect = new Rect(
                panelRect.x + panelRect.width - 110f,
                panelRect.y + 104f,
                80f,
                40f);
            if (GUI.Button(upRect, "▲ 높이기"))
            {
                _station.AdjustTemperature(LocalPlayer, 1);
            }

            var progress =
                _station.Rules.GetProgressNormalized(requiredSeconds);
            var barRect = new Rect(
                panelRect.x + 20f,
                panelRect.y + 160f,
                panelRect.width - 40f,
                24f);
            GUI.Box(barRect, string.Empty);
            GUI.Box(
                new Rect(
                    barRect.x,
                    barRect.y,
                    barRect.width * progress,
                    barRect.height),
                $"유지 {progress * requiredSeconds:0.0}/{requiredSeconds:0.0}초");

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
            }
        }
    }
}
