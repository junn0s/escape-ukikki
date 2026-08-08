using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 공기 필터 교체 미션의 상자형 임시 UI다(GDD §10.2).
    /// 낡은 필터 박스를 빼내고, 새 필터 박스를 꽂는다.
    /// </summary>
    public sealed class SwapFilterView : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 180f;

        [SerializeField] private SwapFilterStation _station;

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

        public void Configure(SwapFilterStation station, GameObject localPlayer)
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
            SwapFilterStation station,
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
            GUI.Box(panelRect, "공기 필터 교체");

            var slotRect = new Rect(
                panelRect.x + panelRect.width * 0.5f - 40f,
                panelRect.y + 50f,
                80f,
                80f);

            if (!_station.Rules.IsOldFilterRemoved)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                GUI.Box(slotRect, "낡은 필터");
                GUI.color = previousColor;

                if (GUI.Button(
                        new Rect(
                            panelRect.x + panelRect.width * 0.5f - 60f,
                            panelRect.y + panelRect.height - 44f,
                            120f,
                            32f),
                        "[빼기]"))
                {
                    _station.RequestSwap(LocalPlayer, isInstallingNew: false);
                }
            }
            else
            {
                var previousColor = GUI.color;
                GUI.color = new Color(0.9f, 0.85f, 0.3f, 1f);
                GUI.Box(slotRect, "새 필터");
                GUI.color = previousColor;

                if (GUI.Button(
                        new Rect(
                            panelRect.x + panelRect.width * 0.5f - 60f,
                            panelRect.y + panelRect.height - 44f,
                            120f,
                            32f),
                        "[꽂기]"))
                {
                    _station.RequestSwap(LocalPlayer, isInstallingNew: true);
                }
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
