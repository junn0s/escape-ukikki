using System.Text;
using UnityEngine;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 환자 바이탈 기록 미션의 상자형 임시 UI다(GDD §10.2).
    /// 모니터에 표시된 숫자를 숫자 키패드로 그대로 입력한다.
    /// </summary>
    public sealed class PatientVitalsView : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float PanelHeight = 240f;

        [SerializeField] private PatientVitalsStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;

        private bool _isOpenBacking;
        private GameObject _localPlayer;

        /// <summary>
        /// 실제로 조작 중인 플레이어다. 네트워크 모드에서는 소유 플레이어가,
        /// 단독 재생에서는 씬의 프로토타입 플레이어가 된다.
        /// </summary>
        private GameObject LocalPlayer =>
            LocalGameplayPlayer.Resolve(_localPlayer);
        private readonly StringBuilder _inputBuffer = new();

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
            PatientVitalsStation station,
            SurvivorMissionBalanceConfig config,
            GameObject localPlayer)
        {
            Unsubscribe();
            _station = station;
            _config = config;
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
            _inputBuffer.Clear();
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
            PatientVitalsStation station,
            GameObject interactor)
        {
            if (interactor == LocalPlayer)
            {
                _isOpen = true;
                _inputBuffer.Clear();
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

            ReadKeyInput();

            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUI.Box(panelRect, "환자 바이탈 기록");

            var monitorStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.9f, 0.5f) }
            };
            GUI.Box(
                new Rect(panelRect.x + 40f, panelRect.y + 40f, panelRect.width - 80f, 60f),
                string.IsNullOrEmpty(_station.DisplayedCode)
                    ? "----"
                    : _station.DisplayedCode,
                monitorStyle);

            var codeLength = _config != null
                ? _config.PatientVitalsCodeLength
                : 4;
            var slotSize = 44f;
            var spacing = 8f;
            var totalWidth = codeLength * slotSize + (codeLength - 1) * spacing;
            var startX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;
            var slotY = panelRect.y + 120f;
            for (var index = 0; index < codeLength; index++)
            {
                var filled = index < _inputBuffer.Length;
                GUI.Box(
                    new Rect(startX + index * (slotSize + spacing), slotY, slotSize, slotSize),
                    filled ? _inputBuffer[index].ToString() : "_");
            }

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.76f, 0.84f) }
            };
            GUI.Label(
                new Rect(panelRect.x, slotY + slotSize + 12f, panelRect.width, 24f),
                "숫자 키로 입력하세요",
                hintStyle);
        }

        private void ReadKeyInput()
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
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

            var digit = KeyCodeToDigit(currentEvent.keyCode);
            var codeLength = _config != null
                ? _config.PatientVitalsCodeLength
                : 4;
            if (digit == '\0' || _inputBuffer.Length >= codeLength)
            {
                return;
            }

            _inputBuffer.Append(digit);
            currentEvent.Use();
            if (_inputBuffer.Length == codeLength)
            {
                _station.SubmitCode(LocalPlayer, _inputBuffer.ToString());
                _inputBuffer.Clear();
            }
        }

        private static char KeyCodeToDigit(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                return (char)('0' + (keyCode - KeyCode.Alpha0));
            }

            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                return (char)('0' + (keyCode - KeyCode.Keypad0));
            }

            return '\0';
        }
    }
}
