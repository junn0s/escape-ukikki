using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Settings;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class RoundHudView : MonoBehaviour
    {
        private const float DevelopmentFiveMinutesSeconds = 300f;
        private const float DevelopmentOneMinuteSeconds = 60f;
        private const float DevelopmentFiveSeconds = 5f;

        private NetworkRoundState _roundState;
        private GUIStyle _hudStyle;
        private GUIStyle _hudHeaderStyle;
        private GUIStyle _hudBodyStyle;
        private GUIStyle _progressLabelStyle;
        private GUIStyle _resultTitleStyle;
        private GUIStyle _resultBodyStyle;
        private readonly List<ulong> _developmentClientIds = new();
        private Vector2 _developmentScroll;
        private int _developmentTargetIndex;
        private bool _isDevelopmentPanelOpen;
        private bool _showNoiseRadius;
        private LineRenderer _noiseRadiusLine;
        private Material _noiseRadiusMaterial;

        private void OnEnable()
        {
            LocalGameSettings.Changed += HandleSettingsChanged;
            NetworkRoundState.CurrentChanged += BindCurrentRound;
            BindCurrentRound();
        }

        private void OnDisable()
        {
            LocalGameSettings.Changed -= HandleSettingsChanged;
            NetworkRoundState.CurrentChanged -= BindCurrentRound;
            UnbindRound();
            CleanupNoiseRadius();
        }

        private void Update()
        {
            UpdateNoiseRadius();
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

        private void HandleSettingsChanged()
        {
            _hudStyle = null;
            _hudHeaderStyle = null;
            _hudBodyStyle = null;
            _progressLabelStyle = null;
            _resultTitleStyle = null;
            _resultBodyStyle = null;
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
                // 로비 복귀 버튼이 이 OnGUI 호출 안에서 씬 전환을 시작하면
                // OnDisable이 _roundState를 비운다. 아래 개발 버튼에서 다시 접근하지 않는다.
                return;
            }

            if (_roundState != null &&
                _roundState.CanUseDevelopmentControls)
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
                RoundPhase.MeetingDiscussion =>
                    $"토론 {CeilSeconds(_roundState.RemainingPhaseSeconds)}초",
                RoundPhase.MeetingVote =>
                    $"투표 {CeilSeconds(_roundState.RemainingPhaseSeconds)}초",
                RoundPhase.MeetingResult =>
                    $"결과 확인 {CeilSeconds(_roundState.RemainingPhaseSeconds)}초",
                _ => "라운드 종료"
            };
            var progressPercent = _roundState.Config != null
                ? Mathf.RoundToInt(
                    _roundState.ProjectPoints * 100f /
                    _roundState.Config.ProjectMaximumPoints)
                : 0;
            var rect = new Rect(18f, 18f, 340f, 116f);
            GUI.Box(rect, GUIContent.none, _hudStyle);

            var headerColor = _roundState.Phase == RoundPhase.Exploration &&
                              _roundState.RemainingRoundSeconds <= 60f
                ? new Color(1f, 0.22f, 0.18f)
                : _roundState.Phase == RoundPhase.Exploration &&
                  _roundState.RemainingRoundSeconds <= 120f
                    ? new Color(1f, 0.64f, 0.16f)
                    : Color.white;
            _hudHeaderStyle.normal.textColor = headerColor;
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 9f, rect.width - 32f, 30f),
                phaseText,
                _hudHeaderStyle);

            var progressRect = new Rect(
                rect.x + 16f,
                rect.y + 44f,
                rect.width - 32f,
                18f);
            DrawSolidRect(progressRect, new Color(0.02f, 0.08f, 0.10f, 1f));
            DrawSolidRect(
                new Rect(
                    progressRect.x,
                    progressRect.y,
                    progressRect.width * Mathf.Clamp01(progressPercent / 100f),
                    progressRect.height),
                new Color(0.18f, 0.82f, 0.76f, 1f));
            GUI.Label(
                progressRect,
                $"프로젝트 {progressPercent}%",
                _progressLabelStyle);

            var missionText = CreateLocalMissionText();
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 68f, rect.width - 32f, 38f),
                $"{CreateMilestoneLabel(_roundState.ProjectMilestone)}" +
                (string.IsNullOrEmpty(missionText)
                    ? string.Empty
                    : $"   ·   {missionText}"),
                _hudBodyStyle);
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

            if (GUI.Button(buttonRect, "로비 복귀"))
            {
                _roundState.RequestReturnToLobby();
            }
        }

        private void DrawDevelopmentControls()
        {
            var currentEvent = Event.current;
            if (currentEvent != null &&
                currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.F10)
            {
                _isDevelopmentPanelOpen = !_isDevelopmentPanelOpen;
                currentEvent.Use();
            }

            if (GUI.Button(
                    new Rect(18f, Screen.height - 48f, 180f, 30f),
                    _isDevelopmentPanelOpen
                        ? "개발 패널 닫기 [F10]"
                        : "개발 패널 열기 [F10]"))
            {
                _isDevelopmentPanelOpen = !_isDevelopmentPanelOpen;
            }

            if (_isDevelopmentPanelOpen)
            {
                DrawDevelopmentPanel();
            }
        }

        private void DrawDevelopmentPanel()
        {
            RefreshDevelopmentPlayers();
            const float width = 410f;
            var height = Mathf.Min(760f, Screen.height - 40f);
            var rect = new Rect(
                Screen.width - width - 20f,
                20f,
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(rect.x + 14f, rect.y + 12f, width - 28f, height - 24f));
            _developmentScroll = GUILayout.BeginScrollView(_developmentScroll);
            GUILayout.Label("서버 개발 데모 패널", _hudHeaderStyle);
            GUILayout.Label(
                "에디터·Development 빌드 전용 / 릴리스 자동 숨김",
                _hudBodyStyle);

            DrawDevelopmentRoundControls();
            DrawDevelopmentPlayerControls();
            DrawDevelopmentUpgradeControls();
            DrawDevelopmentDiagnostics();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawDevelopmentRoundControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("라운드", _hudHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("탐색 즉시 시작"))
            {
                _roundState.SkipToExplorationForDevelopment();
            }

            if (GUILayout.Button("회의 제한 초기화"))
            {
                _roundState.ResetMeetingCooldownForDevelopment();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("프로젝트 단계", _hudBodyStyle);
            GUILayout.BeginHorizontal();
            DrawProgressButton(0);
            DrawProgressButton(25);
            DrawProgressButton(50);
            DrawProgressButton(75);
            DrawProgressButton(100);
            GUILayout.EndHorizontal();

            GUILayout.Label("남은 시간", _hudBodyStyle);
            GUILayout.BeginHorizontal();
            DrawTimeButton(
                "전체",
                _roundState.Config.ExplorationDurationSeconds);
            DrawTimeButton("5분", DevelopmentFiveMinutesSeconds);
            DrawTimeButton("1분", DevelopmentOneMinuteSeconds);
            DrawTimeButton("5초", DevelopmentFiveSeconds);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("생존자 승리"))
            {
                _roundState.ForceOutcomeForDevelopment(
                    RoundOutcome.SurvivorsWin);
            }

            if (GUILayout.Button("빌런 승리"))
            {
                _roundState.ForceOutcomeForDevelopment(
                    RoundOutcome.VillainWins);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDevelopmentPlayerControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("대상 플레이어", _hudHeaderStyle);
            if (_developmentClientIds.Count == 0)
            {
                GUILayout.Label("연결된 플레이어가 없습니다.", _hudBodyStyle);
                return;
            }

            _developmentTargetIndex = Mathf.Clamp(
                _developmentTargetIndex,
                0,
                _developmentClientIds.Count - 1);
            var targetClientId =
                _developmentClientIds[_developmentTargetIndex];
            var targetPlayer = NetworkManager.Singleton.ConnectedClients[
                targetClientId].PlayerObject;
            var avatar = targetPlayer != null
                ? targetPlayer.GetComponent<NetworkPlayerAvatar>()
                : null;
            var infection = targetPlayer != null
                ? targetPlayer.GetComponent<NetworkInfectionAuthority>()
                : null;
            var inventory = targetPlayer != null
                ? targetPlayer.GetComponent<NetworkAntidoteInventoryAuthority>()
                : null;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(42f)))
            {
                _developmentTargetIndex =
                    (_developmentTargetIndex - 1 +
                     _developmentClientIds.Count) %
                    _developmentClientIds.Count;
            }

            GUILayout.Label(
                avatar != null
                    ? $"{avatar.SlotIndex + 1}번 {avatar.Nickname}  " +
                      $"client {targetClientId}"
                    : $"client {targetClientId}",
                _hudBodyStyle);
            if (GUILayout.Button("▶", GUILayout.Width(42f)))
            {
                _developmentTargetIndex =
                    (_developmentTargetIndex + 1) %
                    _developmentClientIds.Count;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"역할: {avatar?.Role.ToString() ?? "-"} / " +
                $"상태: {infection?.LifeState.ToString() ?? "-"} / " +
                $"해독제: {inventory?.CarriedCount ?? 0}",
                _hudBodyStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("생존자로"))
            {
                _roundState.SetPlayerRoleForDevelopment(
                    targetClientId,
                    PlayerRole.Survivor);
            }

            if (GUILayout.Button("빌런으로"))
            {
                _roundState.SetPlayerRoleForDevelopment(
                    targetClientId,
                    PlayerRole.Villain);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("건강"))
            {
                _roundState.SetPlayerLifeStateForDevelopment(
                    targetClientId,
                    PlayerLifeState.AliveHealthy);
            }

            if (GUILayout.Button("감염"))
            {
                _roundState.SetPlayerLifeStateForDevelopment(
                    targetClientId,
                    PlayerLifeState.AliveInfected);
            }

            if (GUILayout.Button("유령"))
            {
                _roundState.SetPlayerLifeStateForDevelopment(
                    targetClientId,
                    PlayerLifeState.DeadGhost);
            }

            if (GUILayout.Button("해독제 지급"))
            {
                _roundState.GiveAntidoteForDevelopment(targetClientId);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDevelopmentUpgradeControls()
        {
            var upgrade = NetworkVillainUpgradeAuthority.Current;
            if (upgrade == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("괴물·강화 단계", _hudHeaderStyle);
            DrawUpgradeAxis(upgrade, UpgradeAxis.Scent, "후각");
            DrawUpgradeAxis(upgrade, UpgradeAxis.Population, "개체");
            DrawUpgradeAxis(upgrade, UpgradeAxis.Toxicity, "독성");
        }

        private void DrawUpgradeAxis(
            NetworkVillainUpgradeAuthority upgrade,
            UpgradeAxis axis,
            string label)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"{label}  현재 {upgrade.ServerGetLevel(axis)}",
                _hudBodyStyle,
                GUILayout.Width(150f));
            for (var level = VillainUpgradeState.MinimumLevel;
                 level <= VillainUpgradeState.MaximumLevel;
                 level++)
            {
                var requestedLevel = level;
                if (GUILayout.Button($"{level}단계"))
                {
                    upgrade.ServerSetLevelForDevelopment(
                        axis,
                        requestedLevel);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDevelopmentDiagnostics()
        {
            GUILayout.Space(8f);
            GUILayout.Label("진단", _hudHeaderStyle);
            var noiseService = NoiseService.Current;
            var noiseLabel = noiseService != null && noiseService.HasLastNoise
                ? $"마지막 소음: {noiseService.LastNoise.Intensity} / " +
                  $"{noiseService.LastNoise.PathRadius:0.#}m / " +
                  noiseService.LastNoise.RoomId
                : "마지막 소음: 없음";
            GUILayout.Label(noiseLabel, _hudBodyStyle);
            if (GUILayout.Button(
                    _showNoiseRadius
                        ? "소음 범위 숨기기"
                        : "소음 범위 표시하기"))
            {
                _showNoiseRadius = !_showNoiseRadius;
            }

            var monsterIndex = 1;
            foreach (var monster in NetworkMonsterAuthority.ActiveAuthorities)
            {
                if (monster == null || !monster.isActiveAndEnabled)
                {
                    continue;
                }

                var target = monster.TargetClientId ==
                             NetworkMonsterAuthority.NoTargetClientId
                    ? "없음"
                    : monster.TargetClientId.ToString();
                GUILayout.Label(
                    $"M{monsterIndex}  {monster.ReplicatedState}  " +
                    $"목표 client {target}",
                    _hudBodyStyle);
                monsterIndex++;
            }
        }

        private void DrawProgressButton(int percent)
        {
            if (GUILayout.Button($"{percent}%"))
            {
                _roundState.SetProjectProgressForDevelopment(percent);
            }
        }

        private void DrawTimeButton(string label, float seconds)
        {
            if (GUILayout.Button(label))
            {
                _roundState.SetRemainingRoundSecondsForDevelopment(seconds);
            }
        }

        private void RefreshDevelopmentPlayers()
        {
            _developmentClientIds.Clear();
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            foreach (var pair in networkManager.ConnectedClients)
            {
                if (pair.Value?.PlayerObject != null)
                {
                    _developmentClientIds.Add(pair.Key);
                }
            }

            _developmentClientIds.Sort();
            if (_developmentClientIds.Count > 0)
            {
                _developmentTargetIndex = Mathf.Clamp(
                    _developmentTargetIndex,
                    0,
                    _developmentClientIds.Count - 1);
            }
        }

        private void UpdateNoiseRadius()
        {
            var canShow = _showNoiseRadius &&
                          _roundState != null &&
                          _roundState.CanUseDevelopmentControls &&
                          _roundState.Phase != RoundPhase.RoundResult &&
                          NoiseService.Current != null &&
                          NoiseService.Current.HasLastNoise;
            if (!canShow)
            {
                if (_noiseRadiusLine != null)
                {
                    _noiseRadiusLine.enabled = false;
                }
                return;
            }

            EnsureNoiseRadiusLine();
            if (_noiseRadiusLine == null)
            {
                return;
            }

            var noise = NoiseService.Current.LastNoise;
            const int segmentCount = 64;
            _noiseRadiusLine.positionCount = segmentCount;
            _noiseRadiusLine.enabled = true;
            var color = noise.Intensity switch
            {
                NoiseIntensity.Large => new Color(1f, 0.1f, 0.08f, 0.85f),
                NoiseIntensity.Medium => new Color(1f, 0.58f, 0.08f, 0.85f),
                _ => new Color(0.3f, 0.85f, 1f, 0.85f)
            };
            _noiseRadiusLine.startColor = color;
            _noiseRadiusLine.endColor = color;
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / segmentCount;
                _noiseRadiusLine.SetPosition(
                    index,
                    noise.WorldPosition + new Vector3(
                        Mathf.Cos(angle) * noise.PathRadius,
                        Mathf.Sin(angle) * noise.PathRadius,
                        -0.5f));
            }
        }

        private void EnsureNoiseRadiusLine()
        {
            if (_noiseRadiusLine != null)
            {
                return;
            }

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return;
            }

            var lineObject = new GameObject("[Debug] NoiseRadius");
            lineObject.transform.SetParent(transform, false);
            _noiseRadiusMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };
            _noiseRadiusLine = lineObject.AddComponent<LineRenderer>();
            _noiseRadiusLine.sharedMaterial = _noiseRadiusMaterial;
            _noiseRadiusLine.useWorldSpace = true;
            _noiseRadiusLine.loop = true;
            _noiseRadiusLine.widthMultiplier = 0.08f;
            _noiseRadiusLine.numCapVertices = 2;
            _noiseRadiusLine.sortingOrder = short.MaxValue;
        }

        private void CleanupNoiseRadius()
        {
            if (_noiseRadiusLine != null)
            {
                Destroy(_noiseRadiusLine.gameObject);
                _noiseRadiusLine = null;
            }

            if (_noiseRadiusMaterial != null)
            {
                Destroy(_noiseRadiusMaterial);
                _noiseRadiusMaterial = null;
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
                alignment = TextAnchor.MiddleCenter
            };
            _hudStyle.normal.textColor = Color.white;
            _hudHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = LocalGameSettings.GetScaledFontSize(20),
                fontStyle = FontStyle.Bold
            };
            _hudBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = LocalGameSettings.GetScaledFontSize(14)
            };
            _hudBodyStyle.normal.textColor =
                new Color(0.78f, 0.88f, 0.92f);
            _progressLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(13),
                fontStyle = FontStyle.Bold
            };
            _progressLabelStyle.normal.textColor = Color.white;
            _resultTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(38),
                fontStyle = FontStyle.Bold
            };
            _resultTitleStyle.normal.textColor = Color.white;
            _resultBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(20)
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

            return $"개인 미션 {journal.CompletedCount}/" +
                   $"{journal.AssignedCount}";
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
