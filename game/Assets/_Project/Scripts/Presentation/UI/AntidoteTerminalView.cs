using MonkeyLab.Gameplay.Infection;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 백신실 중앙 제어 PC 화면이다(GDD §14.2). 혈청 분석 연출 후 5자리 배합 코드를
    /// 표시하며, 코드는 화면에만 잠깐 남고 어디에도 저장되지 않는다.
    ///
    /// <see cref="FuseMissionView"/>와 달리 이동을 잠그지 않는다. 코드를 확인한 뒤
    /// 같은 방 반대편 제작대까지 걸어가며 문밖을 경계하는 것이 미션의 일부이기 때문이다
    /// (map-level-design.md §7.2).
    /// </summary>
    public sealed class AntidoteTerminalView : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 220f;
        private const float StatusMessageDurationSeconds = 2.5f;

        [SerializeField] private AntidoteTerminalPrototype _terminal;
        [SerializeField] private AntidoteService _antidoteService;

        private bool _isOpenBacking;
        private bool _isSubscribed;
        private string _statusMessage = string.Empty;
        private float _statusVisibleUntil;

        /// <summary>
        /// 열림 상태를 <see cref="MissionOverlayState"/>에 함께 알린다. 다만 이동은
        /// 잠그지 않으므로 <see cref="FuseMissionView"/>처럼 조준·이동 컨트롤러를
        /// 붙잡지 않는다.
        /// </summary>
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
            AntidoteTerminalPrototype terminal,
            AntidoteService antidoteService)
        {
            Unsubscribe();
            _terminal = terminal;
            _antidoteService = antidoteService;
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

        private void OnGUI()
        {
            if (_isOpen && _terminal != null)
            {
                DrawTerminalPanel();
            }

            if (!string.IsNullOrEmpty(_statusMessage) &&
                Time.unscaledTime <= _statusVisibleUntil)
            {
                DrawStatusBanner();
            }
        }

        private void DrawTerminalPanel()
        {
            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);

            var previousColor = GUI.color;
            GUI.color = new Color(0.05f, 0.09f, 0.07f, 0.97f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.6f, 0.4f, 1f);
            GUI.DrawTexture(
                new Rect(panelRect.x, panelRect.y, panelRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.34f, 1f, 0.52f) }
            };
            GUI.Label(
                new Rect(panelRect.x + 16f, panelRect.y + 14f, panelRect.width - 32f, 28f),
                "중앙 제어 PC",
                titleStyle);

            if (_terminal.IsAnalyzing)
            {
                DrawAnalyzing(panelRect);
            }
            else if (_antidoteService != null && _antidoteService.HasValidCode)
            {
                DrawIssuedCode(panelRect);
            }
            else
            {
                DrawIdlePrompt(panelRect);
            }

            var closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            if (GUI.Button(
                    new Rect(
                        panelRect.x + panelRect.width - 110f,
                        panelRect.y + panelRect.height - 46f,
                        94f,
                        30f),
                    "닫기 (Esc)",
                    closeStyle) ||
                (Event.current.type == EventType.KeyDown &&
                 Event.current.keyCode == KeyCode.Escape))
            {
                _isOpen = false;
            }
        }

        private static void DrawAnalyzing(Rect panelRect)
        {
            var bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                normal = { textColor = new Color(0.34f, 1f, 0.52f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 16f, panelRect.y + 56f, panelRect.width - 32f, 100f),
                "  감염체 혈청 분석 중...",
                bodyStyle);
        }

        private void DrawIssuedCode(Rect panelRect)
        {
            var codeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.34f, 1f, 0.52f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 16f, panelRect.y + 56f, panelRect.width - 32f, 72f),
                _antidoteService.IssuedCode,
                codeStyle);

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.85f, 0.78f) }
            };
            GUI.Label(
                new Rect(panelRect.x + 16f, panelRect.y + 132f, panelRect.width - 32f, 24f),
                "코드는 저장되지 않습니다. 제작대까지 기억하세요.",
                hintStyle);
        }

        private static void DrawIdlePrompt(Rect panelRect)
        {
            var bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                normal = { textColor = new Color(0.34f, 1f, 0.52f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 16f, panelRect.y + 56f, panelRect.width - 32f, 100f),
                "  RECOVERY://VACCINE_LAB\n  [E] 배합 코드 조회",
                bodyStyle);
        }

        private void DrawStatusBanner()
        {
            var statusStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            GUI.Box(
                new Rect((Screen.width - 320f) * 0.5f, 180f, 320f, 40f),
                _statusMessage,
                statusStyle);
        }

        private void ShowStatus(string message)
        {
            _statusMessage = message;
            _statusVisibleUntil = Time.unscaledTime + StatusMessageDurationSeconds;
        }

        private void HandleAnalyzingStateChanged(AntidoteTerminalPrototype terminal)
        {
            if (terminal.IsAnalyzing)
            {
                _isOpen = true;
            }
        }

        private void HandleCodeStateChanged(AntidoteService service)
        {
            if (service.HasValidCode)
            {
                _isOpen = true;
                ShowStatus("배합 코드가 발급되었습니다");
            }
        }

        private void Subscribe()
        {
            if (_isSubscribed)
            {
                return;
            }

            if (_terminal != null)
            {
                _terminal.AnalyzingStateChanged += HandleAnalyzingStateChanged;
            }

            if (_antidoteService != null)
            {
                _antidoteService.CodeStateChanged += HandleCodeStateChanged;
            }

            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
            {
                return;
            }

            if (_terminal != null)
            {
                _terminal.AnalyzingStateChanged -= HandleAnalyzingStateChanged;
            }

            if (_antidoteService != null)
            {
                _antidoteService.CodeStateChanged -= HandleCodeStateChanged;
            }

            _isSubscribed = false;
        }
    }
}
