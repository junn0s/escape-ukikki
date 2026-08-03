using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Presentation.Settings;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class MonsterBiteAlertView : MonoBehaviour
    {
        private const float AlertDurationSeconds = 2f;

        [SerializeField] private MonsterTarget _target;

        private bool _isSubscribed;
        private float _visibleUntil;

        public void Configure(MonsterTarget target)
        {
            Unsubscribe();
            _target = target;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _visibleUntil)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(24),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.70f, 0.02f, 0.04f);
            GUI.Box(
                new Rect((Screen.width - 520f) * 0.5f, 150f, 520f, 64f),
                "원숭이에게 물렸습니다!",
                style);
            GUI.backgroundColor = previousColor;
        }

        private void HandleBitten(
            MonsterTarget target,
            MonsterBiteController source)
        {
            _visibleUntil = Time.unscaledTime + AlertDurationSeconds;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _target == null)
            {
                return;
            }

            _target.BitePresented += HandleBitten;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _target == null)
            {
                return;
            }

            _target.BitePresented -= HandleBitten;
            _isSubscribed = false;
        }
    }
}
