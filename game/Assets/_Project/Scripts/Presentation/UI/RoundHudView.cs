using MonkeyLab.Gameplay.Application;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class RoundHudView : MonoBehaviour
    {
        private NetworkRoundState _roundState;
        private GUIStyle _hudStyle;
        private GUIStyle _resultTitleStyle;
        private GUIStyle _resultBodyStyle;

        private void OnEnable()
        {
            NetworkRoundState.CurrentChanged += BindCurrentRound;
            BindCurrentRound();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindCurrentRound;
            UnbindRound();
        }

        private void BindCurrentRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += RepaintView;
            }
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= RepaintView;
            }

            _roundState = null;
        }

        private void RepaintView()
        {
        }

        private void OnGUI()
        {
            if (_roundState == null || !_roundState.IsSpawned)
            {
                return;
            }

            EnsureStyles();
            DrawRoundStatus();
            if (_roundState.Phase == RoundPhase.RoundResult)
            {
                DrawRoundResult();
            }

            if (_roundState.CanUseDevelopmentControls)
            {
                DrawDevelopmentControls();
            }
        }

        private void DrawRoundStatus()
        {
            var phaseText = _roundState.Phase switch
            {
                RoundPhase.RoleReveal =>
                    $"역할 확인 {CeilSeconds(_roundState.RemainingPhaseSeconds)}초",
                RoundPhase.GracePeriod =>
                    $"시작 보호 {CeilSeconds(_roundState.RemainingPhaseSeconds)}초",
                RoundPhase.Exploration =>
                    $"남은 시간 {FormatTime(_roundState.RemainingRoundSeconds)}",
                _ => "라운드 종료"
            };
            var progressPercent = _roundState.Config != null
                ? Mathf.RoundToInt(
                    _roundState.ProjectPoints * 100f /
                    _roundState.Config.ProjectMaximumPoints)
                : 0;
            var missionText = CreateLocalMissionText();
            GUI.Box(
                new Rect(18f, 18f, 300f, 96f),
                $"{phaseText}\n프로젝트 {progressPercent}%  " +
                $"[{CreateMilestoneLabel(_roundState.ProjectMilestone)}]" +
                missionText,
                _hudStyle);
        }

        private void DrawRoundResult()
        {
            const float width = 580f;
            const float height = 190f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            var title = _roundState.Outcome == RoundOutcome.SurvivorsWin
                ? "생존자 승리"
                : "빌런 승리";
            GUI.Label(
                new Rect(rect.x, rect.y + 28f, width, 56f),
                title,
                _resultTitleStyle);
            GUI.Label(
                new Rect(rect.x, rect.y + 96f, width, 42f),
                CreateEndReasonLabel(_roundState.EndReason),
                _resultBodyStyle);
        }

        private void DrawDevelopmentControls()
        {
            var y = Screen.height - 48f;
            if (GUI.Button(
                    new Rect(18f, y, 150f, 30f),
                    "개발: 탐색 시작"))
            {
                _roundState.SkipToExplorationForDevelopment();
            }

            if (GUI.Button(
                    new Rect(176f, y, 150f, 30f),
                    "개발: 진행도 +25%"))
            {
                _roundState.AddQuarterProgressForDevelopment();
            }

            if (GUI.Button(
                    new Rect(334f, y, 150f, 30f),
                    "개발: 제한시간 5초"))
            {
                _roundState.SetFiveSecondTimeoutForDevelopment();
            }
        }

        private void EnsureStyles()
        {
            if (_hudStyle != null)
            {
                return;
            }

            _hudStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            _hudStyle.normal.textColor = Color.white;
            _resultTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 38,
                fontStyle = FontStyle.Bold
            };
            _resultTitleStyle.normal.textColor = Color.white;
            _resultBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            _resultBodyStyle.normal.textColor =
                new Color(0.82f, 0.9f, 0.96f);
        }

        private static int CeilSeconds(float seconds)
        {
            return Mathf.CeilToInt(Mathf.Max(0f, seconds));
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = CeilSeconds(seconds);
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string CreateMilestoneLabel(ProjectMilestone milestone)
        {
            return milestone switch
            {
                ProjectMilestone.FacilityGuidance => "시설 안내 활성화",
                ProjectMilestone.SecurityAccess => "보안 정보 활성화",
                ProjectMilestone.ExitGuidance => "탈출구 안내 활성화",
                ProjectMilestone.Completed => "프로젝트 완료",
                _ => "기본 단계"
            };
        }

        private static string CreateEndReasonLabel(RoundEndReason reason)
        {
            return reason switch
            {
                RoundEndReason.VillainExiled => "빌런이 추방되었습니다.",
                RoundEndReason.ProjectCompleted => "프로젝트가 100% 완성되었습니다.",
                RoundEndReason.AllRealSurvivorsLost => "모든 진짜 생존자를 잃었습니다.",
                RoundEndReason.TimeExpired => "제한시간이 끝났습니다.",
                _ => string.Empty
            };
        }

        private static string CreateLocalMissionText()
        {
            var playerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject == null ||
                !playerObject.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                journal.AssignedCount <= 0)
            {
                return string.Empty;
            }

            return $"\n개인 미션 {journal.CompletedCount}/" +
                   $"{journal.AssignedCount}";
        }
    }
}
