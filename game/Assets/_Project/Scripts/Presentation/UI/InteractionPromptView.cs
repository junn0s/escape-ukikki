using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor _interactor;

        public void Configure(PlayerInteractor interactor)
        {
            _interactor = interactor;
        }

        private void OnGUI()
        {
            if (_interactor == null || !_interactor.HasTarget)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            const float width = 280f;
            const float height = 54f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - 110f,
                width,
                height);
            GUI.Box(rect, "[E] " + _interactor.CurrentPrompt, style);
        }
    }
}
