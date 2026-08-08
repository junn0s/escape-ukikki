using UnityEngine;
using MonkeyLab.Gameplay.Villain;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 메인 전력선 절단 미션의 상자형 임시 UI다(GDD §13.2).
    /// 전선 박스 3개를 클릭해 자른다. 같은 자리의 퓨즈 교체와 동일한
    /// 상자 UI를 공유해 겉모습이 구분되지 않는다.
    /// </summary>
    public sealed class PowerLineCutView : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float PanelHeight = 200f;
        private const float WireSize = 56f;

        [SerializeField] private PowerLineCutStation _station;

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
            PowerLineCutStation station,
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
            PowerLineCutStation station,
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
            GUI.Box(panelRect, "퓨즈 교체");

            var currentEvent = Event.current;
            var wireCount = _station.Rules.ItemCount;
            var spacing = 16f;
            var totalWidth = wireCount * WireSize + (wireCount - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;

            for (var index = 0; index < wireCount; index++)
            {
                var isCut = _station.Rules.IsPlaced(index);
                var wireRect = new Rect(
                    startX + index * (WireSize + spacing),
                    panelRect.y + 70f,
                    WireSize,
                    WireSize);

                var previousColor = GUI.color;
                GUI.color = isCut
                    ? new Color(0.4f, 0.4f, 0.4f)
                    : new Color(0.9f, 0.75f, 0.2f);
                GUI.Box(wireRect, isCut ? "절단됨" : "전선");
                GUI.color = previousColor;

                if (!isCut &&
                    currentEvent.type == EventType.MouseDown &&
                    wireRect.Contains(currentEvent.mousePosition))
                {
                    _station.CutWire(LocalPlayer, index);
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
