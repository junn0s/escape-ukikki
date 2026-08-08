using UnityEngine;
using MonkeyLab.Gameplay.Villain;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 빌런 전용 8초 누르기 미션의 상자형 임시 UI다(GDD §13.2).
    /// 같은 자리의 생존자 미션과 동일한 상자 UI를 공유해 겉모습이 구분되지 않는다.
    /// </summary>
    public sealed class VillainHoldButtonView : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float PanelHeight = 140f;

        [SerializeField] private VillainHoldButtonStation _station;

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
            VillainHoldButtonStation station,
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
            VillainHoldButtonStation station,
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

            GUI.Box(panelRect, "백신 데이터 다운로드");

            var progress = _station.Rules.GetProgressNormalized(
                _station.RequiredHoldSeconds);
            var barRect = new Rect(
                panelRect.x + 20f,
                panelRect.y + 40f,
                panelRect.width - 40f,
                24f);
            GUI.Box(barRect, string.Empty);
            var fillRect = new Rect(
                barRect.x,
                barRect.y,
                barRect.width * progress,
                barRect.height);
            GUI.Box(fillRect, string.Empty);

            var currentEvent = Event.current;
            var buttonRect = new Rect(
                panelRect.x + 60f,
                panelRect.y + 80f,
                panelRect.width - 120f,
                40f);
            GUI.Box(buttonRect, "[다운로드] 꾹 누르고 있기");

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
