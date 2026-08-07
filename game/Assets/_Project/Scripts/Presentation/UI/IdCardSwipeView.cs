using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// ID 카드 긁기 미션의 상자형 임시 UI다(GDD §10.2).
    /// 카드 박스를 리더기 박스로 드래그하되, 너무 빠르거나 느리면 실패한다.
    /// </summary>
    public sealed class IdCardSwipeView : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float PanelHeight = 200f;
        private const float CardSize = 56f;
        private const float ReaderSize = 64f;

        [SerializeField] private IdCardSwipeStation _station;

        private bool _isOpenBacking;
        private GameObject _localPlayer;
        private bool _isDragging;
        private float _dragStartTime;
        private Vector2 _dragPosition;

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
            IdCardSwipeStation station,
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
            _isDragging = false;
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
            IdCardSwipeStation station,
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
            GUI.Box(panelRect, "ID 카드 긁기");

            var readerRect = new Rect(
                panelRect.x + panelRect.width - ReaderSize - 24f,
                panelRect.y + panelRect.height * 0.5f - ReaderSize * 0.5f,
                ReaderSize,
                ReaderSize);
            GUI.Box(readerRect, "리더기");

            var cardRect = _isDragging
                ? new Rect(
                    _dragPosition.x - CardSize * 0.5f,
                    _dragPosition.y - CardSize * 0.5f,
                    CardSize,
                    CardSize)
                : new Rect(
                    panelRect.x + 24f,
                    panelRect.y + panelRect.height * 0.5f - CardSize * 0.5f,
                    CardSize,
                    CardSize);
            GUI.Box(cardRect, "카드");

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                !_isDragging &&
                cardRect.Contains(currentEvent.mousePosition))
            {
                _isDragging = true;
                _dragStartTime = Time.unscaledTime;
                _dragPosition = currentEvent.mousePosition;
            }
            else if (_isDragging && currentEvent.type == EventType.MouseDrag)
            {
                _dragPosition = currentEvent.mousePosition;
            }
            else if (_isDragging && currentEvent.type == EventType.MouseUp)
            {
                if (readerRect.Contains(currentEvent.mousePosition))
                {
                    var duration = Time.unscaledTime - _dragStartTime;
                    _station.RequestSwipe(_localPlayer, duration);
                }

                _isDragging = false;
            }

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.76f, 0.84f) }
            };
            GUI.Label(
                new Rect(panelRect.x, panelRect.y + panelRect.height - 30f, panelRect.width, 24f),
                "적당한 속도로 드래그하세요",
                hintStyle);

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isDragging = false;
                _isOpen = false;
            }
        }
    }
}
