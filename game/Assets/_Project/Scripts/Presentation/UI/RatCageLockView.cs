using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 실험용 쥐 케이지 잠그기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 자물쇠 박스 4개를 클릭해 잠근다.
    /// </summary>
    public sealed class RatCageLockView : MonoBehaviour
    {
        private const float PanelWidth = 400f;
        private const float PanelHeight = 200f;
        private const float LockSize = 56f;

        [SerializeField] private RatCageLockStation _station;

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
            RatCageLockStation station,
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
            RatCageLockStation station,
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
                $"쥐 케이지 잠그기 ({_station.Rules.PlacedCount}/{_station.Rules.ItemCount})");

            var currentEvent = Event.current;
            var lockCount = _station.Rules.ItemCount;
            var spacing = 16f;
            var totalWidth = lockCount * LockSize + (lockCount - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;

            for (var index = 0; index < lockCount; index++)
            {
                var isLocked = _station.Rules.IsPlaced(index);
                var lockRect = new Rect(
                    startX + index * (LockSize + spacing),
                    panelRect.y + 70f,
                    LockSize,
                    LockSize);

                var previousColor = GUI.color;
                GUI.color = isLocked
                    ? new Color(0.3f, 0.9f, 0.45f)
                    : new Color(0.7f, 0.6f, 0.2f);
                GUI.Box(lockRect, isLocked ? "잠김" : "열림");
                GUI.color = previousColor;

                if (!isLocked &&
                    currentEvent.type == EventType.MouseDown &&
                    lockRect.Contains(currentEvent.mousePosition))
                {
                    _station.LockCage(_localPlayer, index);
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
