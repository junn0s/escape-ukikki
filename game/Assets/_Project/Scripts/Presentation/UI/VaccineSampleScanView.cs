using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 백신 샘플 스캔 미션의 상자형 임시 UI다(GDD §10.2). 샘플 박스 3개를
    /// 반드시 왼쪽부터 순서대로 클릭해 스캔한다.
    /// </summary>
    public sealed class VaccineSampleScanView : MonoBehaviour
    {
        private const float PanelWidth = 400f;
        private const float PanelHeight = 200f;
        private const float SampleSize = 64f;

        [SerializeField] private VaccineSampleScanStation _station;

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
            VaccineSampleScanStation station,
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
            VaccineSampleScanStation station,
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
                $"백신 샘플 스캔 ({_station.Rules.ScannedCount}/{_station.Rules.SampleCount}) — 순서대로 스캔");

            var currentEvent = Event.current;
            var sampleCount = _station.Rules.SampleCount;
            var spacing = 20f;
            var totalWidth =
                sampleCount * SampleSize + (sampleCount - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;
            var nextIndex = _station.Rules.ScannedCount;

            for (var index = 0; index < sampleCount; index++)
            {
                var isScanned = _station.Rules.IsScanned(index);
                var sampleRect = new Rect(
                    startX + index * (SampleSize + spacing),
                    panelRect.y + 70f,
                    SampleSize,
                    SampleSize);

                var previousColor = GUI.color;
                GUI.color = isScanned
                    ? new Color(0.3f, 0.9f, 0.45f)
                    : index == nextIndex
                        ? new Color(0.9f, 0.8f, 0.3f)
                        : new Color(0.5f, 0.5f, 0.5f);
                GUI.Box(sampleRect, $"샘플 {index + 1}");
                GUI.color = previousColor;

                if (!isScanned &&
                    currentEvent.type == EventType.MouseDown &&
                    sampleRect.Contains(currentEvent.mousePosition))
                {
                    _station.ScanSample(_localPlayer, index);
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
