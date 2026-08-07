using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 방호복 소독 미션의 상자형 임시 UI다(GDD §10.2).
    /// 시작 버튼을 누르면 진행 중 화면 전체를 반투명 상자로 덮어 시야를 막는다.
    /// 이동은 잠그지 않는다 — 시야 차단 자체가 위험 요소다.
    /// </summary>
    public sealed class HazmatDecontaminationView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 140f;

        [SerializeField] private HazmatDecontaminationStation _station;

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
            HazmatDecontaminationStation station,
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
            HazmatDecontaminationStation station,
            GameObject interactor)
        {
            if (interactor == _localPlayer)
            {
                _isOpen = true;
            }
        }

        private void OnGUI()
        {
            if (_station == null)
            {
                return;
            }

            if (_station.Rules.IsRunning)
            {
                DrawFogOverlay();
                return;
            }

            if (!_isOpen || _station.Rules.IsCompleted)
            {
                _isOpen = false;
                return;
            }

            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUI.Box(panelRect, "방호복 소독");

            var buttonRect = new Rect(
                panelRect.x + 60f,
                panelRect.y + 60f,
                panelRect.width - 120f,
                40f);
            if (GUI.Button(buttonRect, "[소독 시작]"))
            {
                _station.StartDecontamination(_localPlayer);
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
            }
        }

        /// <summary>
        /// 소독 진행 중에는 화면 전체를 반투명 상자로 덮는다. 컴포넌트 아트가
        /// 없어 김 효과 대신 사각 오버레이로만 표현한다.
        /// </summary>
        private void DrawFogOverlay()
        {
            var previousColor = GUI.color;
            var progress = _station.Rules.GetProgressNormalized(
                _station.RequiredSeconds);
            GUI.color = new Color(0.85f, 0.9f, 0.92f, 0.55f + progress * 0.35f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.25f, 0.3f) }
            };
            GUI.Label(
                new Rect(0f, Screen.height * 0.5f - 20f, Screen.width, 40f),
                "소독 중...",
                labelStyle);
        }
    }
}
