using System.Text;
using MonkeyLab.Gameplay.Infection;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 백신실 제작대 코드 입력 화면이다(GDD §14.3). PC에서 기억한 5자리 배합 코드를
    /// 입력해 합성을 시작한다. <see cref="AntidoteTerminalView"/>와 마찬가지로 이동을
    /// 잠그지 않는다 — 문밖 경계가 미션의 일부다.
    /// </summary>
    public sealed class AntidoteKeypadView : MonoBehaviour
    {
        private const float PanelWidth = 460f;
        private const float PanelHeight = 260f;
        private const float StatusMessageDurationSeconds = 2.5f;
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        [SerializeField] private AntidoteFabricatorPrototype _fabricator;
        [SerializeField] private AntidoteService _antidoteService;

        private bool _isOpenBacking;
        private bool _isSubscribed;
        private readonly StringBuilder _inputBuffer = new();
        private string _statusMessage = string.Empty;
        private float _statusVisibleUntil;
        private FabricatorState _previousState = FabricatorState.Idle;

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
            AntidoteFabricatorPrototype fabricator,
            AntidoteService antidoteService)
        {
            Unsubscribe();
            _fabricator = fabricator;
            _antidoteService = antidoteService;
            Subscribe();
        }

        /// <summary>
        /// 자기 플레이어의 해독제 서비스로 다시 연결한다. 씬을 만들 때 받은 프로토타입
        /// 서비스는 네트워크 모드에서 쓰이지 않아 화면이 열리지 않는다(GDD §14.3).
        /// </summary>
        public void BindAntidoteService(AntidoteService antidoteService)
        {
            if (_antidoteService == antidoteService)
            {
                return;
            }

            Unsubscribe();
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
            _inputBuffer.Clear();
        }

        private void OnGUI()
        {
            if (_isOpen && _fabricator != null)
            {
                ReadKeyInput();
                DrawKeypadPanel();
            }

            if (!string.IsNullOrEmpty(_statusMessage) &&
                Time.unscaledTime <= _statusVisibleUntil)
            {
                DrawStatusBanner();
            }
        }

        private void ReadKeyInput()
        {
            var currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                _fabricator.Fabricator.State != FabricatorState.AwaitingCode)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.Backspace)
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Length--;
                }

                currentEvent.Use();
                return;
            }

            var codeLength = _antidoteService.Config != null
                ? _antidoteService.Config.CodeLength
                : 5;
            var letter = KeyCodeToLetter(currentEvent.keyCode);
            if (letter == '\0' || _inputBuffer.Length >= codeLength)
            {
                return;
            }

            _inputBuffer.Append(letter);
            currentEvent.Use();
            if (_inputBuffer.Length == codeLength)
            {
                _fabricator.SubmitCode(
                    _antidoteService.gameObject,
                    _inputBuffer.ToString());
                _inputBuffer.Clear();
            }
        }

        private static char KeyCodeToLetter(KeyCode keyCode)
        {
            if (keyCode < KeyCode.A || keyCode > KeyCode.Z)
            {
                return '\0';
            }

            return Alphabet[keyCode - KeyCode.A];
        }

        private void DrawKeypadPanel()
        {
            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);

            var previousColor = GUI.color;
            GUI.color = new Color(0.078f, 0.102f, 0.149f, 0.97f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.227f, 0.29f, 0.388f, 1f);
            GUI.DrawTexture(
                new Rect(panelRect.x, panelRect.y, panelRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(panelRect.x + 16f, panelRect.y + 14f, panelRect.width - 32f, 28f),
                "해독제 배합대",
                titleStyle);

            if (_fabricator.Fabricator.State == FabricatorState.Synthesizing)
            {
                DrawSynthesizing(panelRect);
            }
            else if (_fabricator.Fabricator.State == FabricatorState.Ready)
            {
                DrawReady(panelRect);
            }
            else
            {
                DrawSlots(panelRect);
            }

            var closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            if (GUI.Button(
                    new Rect(
                        panelRect.x + panelRect.width - 110f,
                        panelRect.y + panelRect.height - 46f,
                        94f,
                        30f),
                    "나가기 (Esc)",
                    closeStyle))
            {
                _isOpen = false;
            }
        }

        private void DrawSlots(Rect panelRect)
        {
            var codeLength = _antidoteService.Config != null
                ? _antidoteService.Config.CodeLength
                : 5;
            var slotSize = 52f;
            var spacing = 10f;
            var totalWidth = codeLength * slotSize + (codeLength - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;
            var slotY = panelRect.y + 70f;

            for (var index = 0; index < codeLength; index++)
            {
                var filled = index < _inputBuffer.Length;
                var slotStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = filled
                            ? new Color(0.3f, 0.9f, 0.45f)
                            : new Color(0.5f, 0.56f, 0.64f)
                    }
                };
                GUI.Box(
                    new Rect(startX + index * (slotSize + spacing), slotY, slotSize, slotSize),
                    filled ? _inputBuffer[index].ToString() : "_",
                    slotStyle);
            }

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.76f, 0.84f) }
            };
            GUI.Label(
                new Rect(panelRect.x + 16f, slotY + slotSize + 12f, panelRect.width - 32f, 24f),
                "A~Z 키로 코드를 입력하세요",
                hintStyle);
        }

        private void DrawSynthesizing(Rect panelRect)
        {
            var progress = Mathf.RoundToInt(
                _fabricator.Fabricator.NormalizedProgress * 100f);
            var bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.7f, 0.2f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 16f, panelRect.y + 70f, panelRect.width - 32f, 72f),
                $"해독제 합성 중... {progress}%",
                bodyStyle);
        }

        private static void DrawReady(Rect panelRect)
        {
            var bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.9f, 0.45f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 16f, panelRect.y + 70f, panelRect.width - 32f, 72f),
                "[E] 주사기 획득",
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

        private void HandleFabricatorStateChanged(
            AntidoteFabricatorPrototype fabricator)
        {
            var currentState = fabricator.Fabricator.State;
            switch (currentState)
            {
                case FabricatorState.AwaitingCode:
                    _isOpen = true;
                    _inputBuffer.Clear();
                    break;
                case FabricatorState.Idle:
                    // AwaitingCode에서 곧장 Idle로 돌아온 경우만 오입 3회 무효화다.
                    // Ready에서 누군가 완성품을 가져가 Idle이 된 경우는 정상 흐름이다.
                    if (_previousState == FabricatorState.AwaitingCode)
                    {
                        _isOpen = false;
                        ShowStatus("코드가 무효화되었습니다 — PC에서 다시 발급받으세요");
                    }

                    break;
            }

            _previousState = currentState;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _fabricator == null)
            {
                return;
            }

            _fabricator.StateChanged += HandleFabricatorStateChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _fabricator == null)
            {
                return;
            }

            _fabricator.StateChanged -= HandleFabricatorStateChanged;
            _isSubscribed = false;
        }
    }
}
