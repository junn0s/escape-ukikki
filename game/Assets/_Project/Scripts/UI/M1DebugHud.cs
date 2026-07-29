using MonkeyLab.Core;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Monsters;
using UnityEngine;

namespace MonkeyLab.Presentation
{
    /// <summary>
    /// M1 검증용 임시 HUD. IMGUI로 최소 상태만 표시한다.
    /// 정식 HUD(ui-ux-design.md §6)가 생기면 제거한다.
    ///
    /// 개발 기능이므로 릴리스 빌드에서 비활성화한다 (mvp-scope.md §6).
    /// </summary>
    public sealed class M1DebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerInfection _infection;
        [SerializeField] private MonsterBrain _monster;

        private GUIStyle _style;

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };

            var rect = new Rect(16f, 16f, 520f, 28f);

            DrawLine(ref rect, "── M1 프로토타입 ──");
            DrawLine(ref rect, "WASD 이동 / 마우스 조준 / E 상호작용");

            if (_monster != null)
            {
                DrawLine(ref rect, $"괴물 상태: {ToKorean(_monster.State)}");
            }

            if (_infection != null)
            {
                string infectionText = _infection.IsDead
                    ? "사망"
                    : _infection.IsInfected
                        ? $"감염 — 남은 시간 {_infection.RemainingSeconds:F1}초"
                        : "정상";

                DrawLine(ref rect, $"감염: {infectionText}");
            }

            if (_interactor != null && !string.IsNullOrEmpty(_interactor.CurrentPrompt))
            {
                DrawLine(ref rect, $"[E] {_interactor.CurrentPrompt}");
            }
#endif
        }

        private void DrawLine(ref Rect rect, string text)
        {
            GUI.Label(rect, text, _style);
            rect.y += rect.height;
        }

        private static string ToKorean(MonsterState state) => state switch
        {
            MonsterState.Patrol => "순찰",
            MonsterState.RoomIdle => "방 체류",
            MonsterState.InvestigateNoise => "소리 조사",
            MonsterState.Chase => "추격",
            MonsterState.Bite => "물기",
            MonsterState.Search => "수색",
            MonsterState.RecoverPath => "경로 복구",
            _ => state.ToString()
        };
    }
}
