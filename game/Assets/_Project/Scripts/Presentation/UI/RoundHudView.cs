using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
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
            var summary = NetworkRoundSummaryAuthority.Current;
            var entryCount = summary != null ? summary.EntryCount : 0;
            const float width = 620f;
            var height = 250f + entryCount * 24f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(20f, (Screen.height - height) * 0.5f),
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            var title = _roundState.Outcome == RoundOutcome.SurvivorsWin
                ? "생존자 승리"
                : "빌런 승리";
            GUI.Label(
                new Rect(rect.x, rect.y + 20f, width, 46f),
                title,
                _resultTitleStyle);
            GUI.Label(
                new Rect(rect.x, rect.y + 70f, width, 28f),
                CreateEndReasonLabel(
                    _roundState.EndReason,
                    _roundState.IsVillainAbandoned),
                _resultBodyStyle);

            GUILayout.BeginArea(
                new Rect(rect.x + 24f, rect.y + 104f, width - 48f, height - 160f));
            DrawResultSummary(summary);
            GUILayout.EndArea();

            DrawReturnToLobbyButton(rect);
        }

        /// <summary>
        /// GDD §20의 역할 공개, 강화 최종 단계, 플레이어별 완료 미션 수,
        /// 발견·놓친 단서를 표시한다.
        /// 사건 타임라인은 컷라인(로드맵 §14의 3번)에 따라 제외했다.
        /// </summary>
        private void DrawResultSummary(NetworkRoundSummaryAuthority summary)
        {
            var progressPercent = _roundState.Config != null
                ? Mathf.RoundToInt(
                    _roundState.ProjectPoints * 100f /
                    _roundState.Config.ProjectMaximumPoints)
                : 0;
            GUILayout.Label(
                $"최종 프로젝트 진행률 {progressPercent}%",
                _resultBodyStyle);

            if (summary == null || !summary.HasSummary)
            {
                GUILayout.Label("결과 집계를 기다리고 있습니다.", _resultBodyStyle);
                return;
            }

            GUILayout.Label(
                $"강화 단계 — 후각 {summary.ScentLevel} / " +
                $"개체 {summary.PopulationLevel} / " +
                $"독성 {summary.ToxicityLevel}",
                _resultBodyStyle);
            GUILayout.Label(
                $"단서 — 발견 {summary.InspectedClueCount}개 / " +
                $"놓침 {summary.MissedClueCount}개",
                _resultBodyStyle);
            GUILayout.Space(6f);

            for (var index = 0; index < summary.EntryCount; index++)
            {
                var entry = summary.GetEntry(index);
                var roleLabel = entry.Role == PlayerRole.Villain
                    ? "빌런"
                    : "생존자";
                var lifeLabel = entry.LifeState == PlayerLifeState.DeadGhost
                    ? "사망"
                    : "생존";
                GUILayout.Label(
                    $"{entry.SlotIndex + 1}번 ({entry.Color}) — {roleLabel}, " +
                    $"{lifeLabel}, 미션 " +
                    $"{entry.CompletedMissionCount}/{entry.AssignedMissionCount}",
                    _resultBodyStyle);
            }
        }

        /// <summary>
        /// 호스트만 로비로 되돌릴 수 있다. 이 경로가 다음 판의 시작점이다
        /// (mvp-scope §3.2, §7 "라운드 완료").
        /// </summary>
        private void DrawReturnToLobbyButton(Rect resultRect)
        {
            var networkManager = NetworkManager.Singleton;
            var buttonRect = new Rect(
                resultRect.x + (resultRect.width - 220f) * 0.5f,
                resultRect.yMax - 44f,
                220f,
                30f);
            if (networkManager == null || !networkManager.IsServer)
            {
                GUI.Label(
                    buttonRect,
                    "호스트가 로비로 돌아가기를 누르면 이동합니다.",
                    _resultBodyStyle);
                return;
            }

            if (GUI.Button(buttonRect, "로비로 돌아가기"))
            {
                _roundState.RequestReturnToLobby();
            }
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

        private static string CreateEndReasonLabel(
            RoundEndReason reason,
            bool isVillainAbandoned)
        {
            // 같은 승패 입력을 쓰지만 원인이 다르므로 문구를 구분한다(GDD §19.2).
            if (reason == RoundEndReason.VillainExiled && isVillainAbandoned)
            {
                return "빌런이 접속을 종료해 돌아오지 않았습니다.";
            }

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
