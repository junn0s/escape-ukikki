using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 슬라이드 글라스 닦기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 얼룩 박스를 마우스로 문질러(클릭 연타) 지운다.
    /// </summary>
    public sealed class SlideGlassView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 240f;
        private const float StainSize = 56f;

        [SerializeField] private SlideGlassStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

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
            SlideGlassStation station,
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
            SlideGlassStation station,
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
                $"슬라이드 글라스 닦기 ({_station.Rules.CleanedCount}/{_station.Rules.StainCount})");

            var currentEvent = Event.current;
            var stainCount = _station.Rules.StainCount;
            for (var index = 0; index < stainCount; index++)
            {
                var stainRect = new Rect(
                    panelRect.x + 24f + index * (StainSize + 16f),
                    panelRect.y + 60f,
                    StainSize,
                    StainSize);
                var scrubs = _station.Rules.GetScrubCount(index);
                var required = _config != null
                    ? _config.SlideGlassScrubsPerStain
                    : 5;
                GUI.Box(
                    stainRect,
                    _station.Rules.IsClean(index)
                        ? "깨끗함"
                        : $"얼룩\n{scrubs}/{required}");

                if (currentEvent.type == EventType.MouseDown &&
                    stainRect.Contains(currentEvent.mousePosition))
                {
                    _station.ScrubStain(_localPlayer, index);
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
