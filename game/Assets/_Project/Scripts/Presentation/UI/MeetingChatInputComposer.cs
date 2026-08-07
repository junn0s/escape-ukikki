using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 토론 채팅의 네이티브 IME 입력창이다.
    ///
    /// IMGUI TextField는 macOS 한글 조합 중 OnGUI 재평가와 포커스 전환이
    /// 겹치면 자모를 확정 문자처럼 나눌 수 있다. 실제 편집 상태는 uGUI
    /// InputField가 소유하게 하고, MeetingView는 완성된 문자열만 읽는다.
    /// </summary>
    public sealed class MeetingChatInputComposer : MonoBehaviour
    {
        private const int CanvasSortingOrder = 32000;

        private Canvas _canvas;
        private InputField _inputField;
        private GameObject _ownedEventSystem;
        private bool _isVisible;
        private bool _isSubmitRequested;
        private bool _shouldFocus;

        public string Draft => _inputField != null
            ? _inputField.text
            : string.Empty;

        public int DraftLength => Draft.Length;

        public void Show(Rect screenRect, int maximumLength)
        {
            EnsureUi();
            if (_canvas == null || _inputField == null)
            {
                return;
            }

            PositionInput(screenRect);
            _inputField.characterLimit = Mathf.Max(1, maximumLength);
            if (!_isVisible)
            {
                _isVisible = true;
                _canvas.gameObject.SetActive(true);
                _shouldFocus = true;
                Input.imeCompositionMode = IMECompositionMode.On;
            }
        }

        public void Hide()
        {
            if (!_isVisible)
            {
                return;
            }

            _isVisible = false;
            _isSubmitRequested = false;
            _shouldFocus = false;
            if (_inputField != null)
            {
                _inputField.DeactivateInputField();
            }

            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }

            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        public void Clear()
        {
            _inputField?.SetTextWithoutNotify(string.Empty);
        }

        public void RequestSubmit()
        {
            if (_isVisible)
            {
                _isSubmitRequested = true;
            }
        }

        public bool ConsumeSubmitRequest()
        {
            if (!_isSubmitRequested ||
                !string.IsNullOrEmpty(Input.compositionString))
            {
                return false;
            }

            _isSubmitRequested = false;
            return true;
        }

        private void Update()
        {
            if (!_isVisible || _inputField == null)
            {
                return;
            }

            if (_shouldFocus)
            {
                _shouldFocus = false;
                _inputField.Select();
                _inputField.ActivateInputField();
            }

        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (_inputField != null)
            {
                _inputField.onSubmit.RemoveListener(HandleSubmitted);
            }

            if (_ownedEventSystem != null)
            {
                Destroy(_ownedEventSystem);
            }
        }

        private void EnsureUi()
        {
            if (_canvas != null)
            {
                return;
            }

            EnsureEventSystem();

            var canvasObject = new GameObject(
                "[UI] MeetingChatComposer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            var fieldObject = new GameObject(
                "ChatInput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(InputField));
            fieldObject.transform.SetParent(canvasObject.transform, false);
            var fieldImage = fieldObject.GetComponent<Image>();
            fieldImage.color = new Color(0.035f, 0.055f, 0.08f, 0.98f);

            var fontSet = Resources.Load<ImguiFontSet>(
                ImguiFontSet.ResourcePath);
            var font = fontSet != null ? fontSet.PreferredFont : null;

            var text = CreateText(
                fieldObject.transform,
                "Text",
                font,
                new Color(0.94f, 0.97f, 1f),
                "");
            var placeholder = CreateText(
                fieldObject.transform,
                "Placeholder",
                font,
                new Color(0.48f, 0.58f, 0.66f, 0.82f),
                "메시지를 입력하세요");
            placeholder.fontStyle = FontStyle.Italic;

            _inputField = fieldObject.GetComponent<InputField>();
            _inputField.textComponent = text;
            _inputField.placeholder = placeholder;
            _inputField.lineType = InputField.LineType.SingleLine;
            _inputField.contentType = InputField.ContentType.Standard;
            _inputField.caretColor = new Color(0.28f, 0.92f, 1f);
            _inputField.selectionColor = new Color(0.16f, 0.62f, 0.78f, 0.55f);
            _inputField.customCaretColor = true;
            // InputField 자체의 OSX 조합 종료 억제 로직을 거친 Submit만 받는다.
            // 조합 확정용 Enter는 이 이벤트를 만들지 않는다.
            _inputField.onSubmit.AddListener(HandleSubmitted);

            var fieldRect = fieldObject.GetComponent<RectTransform>();
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.zero;
            fieldRect.pivot = Vector2.zero;
            canvasObject.SetActive(false);
        }

        private void HandleSubmitted(string _)
        {
            if (!_isVisible)
            {
                return;
            }

            _isSubmitRequested = true;
            _shouldFocus = true;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Font font,
            Color color,
            string value)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 6f);
            rect.offsetMax = new Vector2(-12f, -6f);
            return text;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            _ownedEventSystem = new GameObject(
                "[UI] MeetingChatEventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            _ownedEventSystem.transform.SetParent(transform, false);
            _ownedEventSystem
                .GetComponent<InputSystemUIInputModule>()
                .AssignDefaultActions();
        }

        private void PositionInput(Rect screenRect)
        {
            var fieldRect = _inputField.GetComponent<RectTransform>();
            fieldRect.anchoredPosition = new Vector2(
                screenRect.x,
                Screen.height - screenRect.yMax);
            fieldRect.sizeDelta = screenRect.size;

            Input.compositionCursorPos = new Vector2(
                screenRect.x + 18f,
                Screen.height - screenRect.yMax + screenRect.height * 0.5f);
        }
    }
}
