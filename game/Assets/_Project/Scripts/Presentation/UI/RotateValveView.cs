using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 액체 보관실 밸브의 상자형 임시 UI다(GDD §10.2, §13.2).
    /// 좌우 버튼으로 밸브 박스를 돌린다. 빌런에게는 반시계 방향 버튼이,
    /// 생존자에게는 시계 방향 버튼이 강조된다 — 둘 다 항상 조작 가능하지만
    /// 서버가 역할에 맞는 진행치에만 반영한다.
    /// </summary>
    public sealed class RotateValveView : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 220f;
        private const float RotateDegreesPerClick = 15f;

        [SerializeField] private RotateValveStation _station;

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
            RotateValveStation station,
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
            RotateValveStation station,
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

            if (_station.LockRules.IsCompleted ||
                _station.LoosenRules.IsCompleted)
            {
                _isOpen = false;
                return;
            }

            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUI.Box(panelRect, "밸브 조작");

            var dialRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 40f,
                panelRect.y + 40f,
                80f,
                80f);
            var previousMatrix = GUI.matrix;
            var displayAngle = _station.LockRules.AccumulatedDegrees -
                                _station.LoosenRules.AccumulatedDegrees;
            GUIUtility.RotateAroundPivot(displayAngle, dialRect.center);
            GUI.Box(dialRect, "▲");
            GUI.matrix = previousMatrix;

            var progressStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter
            };
            GUI.Label(
                new Rect(panelRect.x, panelRect.y + 40f, panelRect.width, 20f),
                $"잠금 {_station.LockRules.GetProgressNormalized() * 100f:0}% · " +
                $"풀림 {_station.LoosenRules.GetProgressNormalized() * 100f:0}%",
                progressStyle);

            var leftButtonRect = new Rect(
                panelRect.x + 30f,
                panelRect.y + 150f,
                120f,
                36f);
            var rightButtonRect = new Rect(
                panelRect.x + panelRect.width - 150f,
                panelRect.y + 150f,
                120f,
                36f);

            if (GUI.Button(leftButtonRect, "◀ 반시계 (풀기)"))
            {
                _station.RotateValve(LocalPlayer, -RotateDegreesPerClick);
            }

            if (GUI.Button(rightButtonRect, "시계 (잠금) ▶"))
            {
                _station.RotateValve(LocalPlayer, RotateDegreesPerClick);
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
