using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 에어록 압력 조절 미션의 상자형 임시 UI다(GDD §10.2).
    /// 좌우 버튼으로 다이얼 박스를 돌려 눈금을 0에 맞춘다.
    /// </summary>
    public sealed class AirlockDialView : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 200f;
        private const float RotateDegreesPerClick = 6f;

        [SerializeField] private AirlockDialStation _station;

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

        public void Configure(AirlockDialStation station, GameObject localPlayer)
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
            AirlockDialStation station,
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
            GUI.Box(panelRect, "에어록 압력 조절");

            var dialRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 40f,
                panelRect.y + 40f,
                80f,
                80f);
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(
                _station.Rules.CurrentAngleDegrees,
                dialRect.center);
            GUI.Box(dialRect, "▲");
            GUI.matrix = previousMatrix;

            GUI.Label(
                new Rect(panelRect.x, panelRect.y + 40f, panelRect.width, 20f),
                $"눈금: {_station.Rules.CurrentAngleDegrees:0}°",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter
                });

            var leftButtonRect = new Rect(
                panelRect.x + 30f,
                panelRect.y + 140f,
                80f,
                36f);
            var rightButtonRect = new Rect(
                panelRect.x + panelRect.width - 110f,
                panelRect.y + 140f,
                80f,
                36f);

            if (GUI.Button(leftButtonRect, "◀ 반시계"))
            {
                _station.RotateDial(_localPlayer, -RotateDegreesPerClick);
            }

            if (GUI.Button(rightButtonRect, "시계 ▶"))
            {
                _station.RotateDial(_localPlayer, RotateDegreesPerClick);
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
