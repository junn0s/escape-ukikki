using UnityEngine;

namespace MonkeyLab.Presentation
{
    public sealed class ScenePlaceholderView : MonoBehaviour
    {
        [SerializeField] private string _title = "ESCAPE UKIKKI";
        [SerializeField, TextArea] private string _description = "Prototype scene";

        public void Initialize(string title, string description)
        {
            _title = title;
            _description = description;
        }

        private void OnGUI()
        {
            var panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 20,
                padding = new RectOffset(20, 20, 12, 12)
            };

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.78f, 0.25f) }
            };

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(24, 24, 440, 120), GUIContent.none, panelStyle);
            GUI.Label(new Rect(44, 38, 400, 38), _title, titleStyle);
            GUI.Label(new Rect(44, 82, 400, 48), _description, bodyStyle);
        }
    }
}
