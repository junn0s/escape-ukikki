using MonkeyLab.Gameplay.Application;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class GracePeriodView : MonoBehaviour
    {
        [SerializeField] private LocalRoundPhasePrototype _roundPhase;

        public void Configure(LocalRoundPhasePrototype roundPhase)
        {
            _roundPhase = roundPhase;
        }

        private void OnGUI()
        {
            if (_roundPhase == null || _roundPhase.IsMonsterAggressionEnabled)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Box(
                new Rect((Screen.width - 420f) * 0.5f, 28f, 420f, 44f),
                $"시작 보호 {Mathf.CeilToInt(_roundPhase.RemainingGracePeriodSeconds)}초 — 괴물은 순찰만 합니다",
                style);
        }
    }
}
