using MonkeyLab.Gameplay.Application;
using MonkeyLab.Network;
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
            if (_roundPhase == null ||
                _roundPhase.IsMonsterAggressionEnabled ||
                (NetworkRoundState.Current != null &&
                 NetworkRoundState.Current.IsSpawned))
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Box(
                new Rect((Screen.width - 300f) * 0.5f, 16f, 300f, 32f),
                $"시작 보호 {_roundPhase.RemainingGracePeriodSeconds:0}초",
                style);
        }
    }
}
