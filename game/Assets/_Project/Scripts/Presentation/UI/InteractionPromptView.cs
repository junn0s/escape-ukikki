using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        private const float FadeSpeed = 7f;

        /// <summary>조작 방법 안내다. 문구 톤은 ui-ux-design.md를 따른다.</summary>
        private const string InteractionHintText = "E를 눌러 상호작용";

        [SerializeField] private PlayerInteractor _interactor;

        private GUIStyle _keyStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _hintStyle;
        private string _lastPrompt = string.Empty;
        private float _visibility;
        private float _promptChangedAt;

        public PlayerInteractor Interactor => _interactor;

        public void Configure(PlayerInteractor interactor)
        {
            _interactor = interactor;
        }

        private void Update()
        {
            var hasTarget = _interactor != null && _interactor.HasTarget;
            _visibility = Mathf.MoveTowards(
                _visibility,
                hasTarget ? 1f : 0f,
                FadeSpeed * Time.unscaledDeltaTime);
            if (!hasTarget || _interactor.CurrentPrompt == _lastPrompt)
            {
                return;
            }

            _lastPrompt = _interactor.CurrentPrompt;
            _promptChangedAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (_interactor == null || _visibility <= 0.001f)
            {
                return;
            }

            EnsureStyles();
            const float width = 390f;

            // 미션 이름 아래에 조작 안내를 한 줄 더 둔다.
            const float height = 82f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - 118f,
                width,
                height);
            var pop = 1f + Mathf.Max(
                0f,
                1f - (Time.unscaledTime - _promptChangedAt) * 5f) * 0.05f;
            var animatedRect = new Rect(
                rect.center.x - rect.width * pop * 0.5f,
                rect.center.y - rect.height * pop * 0.5f,
                rect.width * pop,
                rect.height * pop);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _visibility);
            DrawSolidRect(
                animatedRect,
                new Color(0.01f, 0.04f, 0.06f, 0.92f));
            DrawSolidRect(
                new Rect(
                    animatedRect.x,
                    animatedRect.yMax - 3f,
                    animatedRect.width,
                    3f),
                new Color(0.22f, 0.88f, 0.92f, 1f));

            var keyRect = new Rect(
                animatedRect.x + 12f,
                animatedRect.y + 10f,
                48f,
                animatedRect.height - 20f);
            DrawSolidRect(keyRect, new Color(0.14f, 0.40f, 0.44f, 1f));
            GUI.Label(keyRect, "E", _keyStyle);
            var textRect = new Rect(
                animatedRect.x + 70f,
                animatedRect.y + 8f,
                animatedRect.width - 82f,
                animatedRect.height * 0.5f);
            GUI.Label(textRect, _interactor.CurrentPrompt, _promptStyle);
            GUI.Label(
                new Rect(
                    textRect.x,
                    textRect.yMax - 2f,
                    textRect.width,
                    animatedRect.height * 0.42f),
                InteractionHintText,
                _hintStyle);
            GUI.color = previousColor;
        }

        private void EnsureStyles()
        {
            if (_keyStyle != null)
            {
                return;
            }

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold
            };
            _keyStyle.normal.textColor = Color.white;
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 19,
                fontStyle = FontStyle.Bold
            };
            _promptStyle.normal.textColor = Color.white;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14
            };
            _hintStyle.normal.textColor =
                new Color(0.62f, 0.88f, 0.92f, 1f);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color *= color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
