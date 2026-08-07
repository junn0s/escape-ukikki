using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoundState : NetworkBehaviour
    {
        [SerializeField] private RoundBalanceConfig _config;
        [SerializeField] private LocalRoundPhasePrototype _localRoundPhase;
        [SerializeField] private NetworkFuseStationAuthority[] _missionStations =
            Array.Empty<NetworkFuseStationAuthority>();

        private readonly NetworkVariable<RoundPhase> _phase = new(
            RoundPhase.RoleReveal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _remainingPhaseSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _remainingRoundSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _projectPoints = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ProjectMilestone> _projectMilestone =
            new(
                ProjectMilestone.None,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<RoundOutcome> _outcome = new(
            RoundOutcome.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<RoundEndReason> _endReason = new(
            RoundEndReason.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<ulong> _recoveryMissionIds = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private RoundStateMachine _stateMachine;
        private ProjectProgressService _projectProgress;
        private readonly List<RecoveryMissionRecord> _recoveryMissions = new();
        /// <summary>결과 화면 문구를 "퇴출"과 "이탈"로 구분하기 위한 값이다.</summary>
        private readonly NetworkVariable<bool> _isVillainAbandoned = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _nextServerPublishTime;
        private bool _isReturningToLobby;
        private bool _isServerRoundInitialized;
        private bool _isVillainExiled;

        public static event Action CurrentChanged;
        public static NetworkRoundState Current { get; private set; }

        public event Action StateChanged;

        public RoundBalanceConfig Config => _config;
        public RoundPhase Phase => _phase.Value;
        public float RemainingPhaseSeconds => _remainingPhaseSeconds.Value;
        public float RemainingRoundSeconds => _remainingRoundSeconds.Value;
        public int ProjectPoints => _projectPoints.Value;
        public ProjectMilestone ProjectMilestone =>
            _projectMilestone.Value;
        public RoundOutcome Outcome => _outcome.Value;
        public RoundEndReason EndReason => _endReason.Value;
        public int MissionStationCount => _missionStations?.Length ?? 0;
        public int RecoveryMissionCount => _recoveryMissionIds.Count;
        public bool AllowsPlayerControl =>
            Phase is RoundPhase.GracePeriod or RoundPhase.Exploration;
        public bool AllowsMissionInteraction =>
            Phase == RoundPhase.Exploration &&
            Outcome == RoundOutcome.None;
        public bool CanUseDevelopmentControls =>
            IsSpawned &&
            IsServer &&
            (Application.isEditor || Debug.isDebugBuild);

        public void Configure(
            RoundBalanceConfig config,
            LocalRoundPhasePrototype localRoundPhase,
            NetworkFuseStationAuthority[] missionStations)
        {
            _config = config;
            _localRoundPhase = localRoundPhase;
            _missionStations =
                missionStations ??
                Array.Empty<NetworkFuseStationAuthority>();
        }

        public override void OnNetworkSpawn()
        {
            if (_config == null || _localRoundPhase == null)
            {
                Debug.LogError(
                    "[Round] Network round references are missing.",
                    this);
                enabled = false;
                return;
            }

            Subscribe();
            Current = this;
            CurrentChanged?.Invoke();

            if (IsServer)
            {
                TryInitializeServerRound();
            }
            else
            {
                ApplyReplicatedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            _localRoundPhase?.ClearAuthoritativePhase();
            _stateMachine = null;
            _projectProgress = null;
            _recoveryMissions.Clear();
            _isServerRoundInitialized = false;
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (!_isServerRoundInitialized)
            {
                TryInitializeServerRound();
                return;
            }

            if (_stateMachine == null ||
                _stateMachine.HasEnded)
            {
                return;
            }

            var previousPhase = _stateMachine.Phase;
            var phaseChanged = _stateMachine.Tick(
                Time.deltaTime,
                CreateWinSnapshot());
            if (phaseChanged &&
                previousPhase == RoundPhase.MeetingVote &&
                _stateMachine.Phase == RoundPhase.MeetingResult)
            {
                // 투표가 끝나는 순간 서버가 한 번만 집계한다.
                NetworkMeetingAuthority.Current?.ServerResolveMeeting();
            }

            if (phaseChanged &&
                previousPhase == RoundPhase.MeetingResult &&
                _stateMachine.Phase == RoundPhase.Exploration)
            {
                ServerApplyPostMeetingBiteProtection();
            }

            if (phaseChanged && _stateMachine.Phase == RoundPhase.RoundResult)
            {
                // 역할과 개인 미션 수는 여기서 처음 전원에게 공개된다(GDD §16.4, §20).
                NetworkRoundSummaryAuthority.Current?.ServerPublishSummary();
            }

            if (phaseChanged || Time.unscaledTime >= _nextServerPublishTime)
            {
                _nextServerPublishTime = Time.unscaledTime + 0.1f;
                PublishServerState();
            }
        }

        /// <summary>
        /// 회의가 끝나 탐색이 재개될 때 살아 있는 전원에게 물기 보호를 준다.
        /// 회의 직전 위치가 그대로 유지되므로, 보호가 없으면 옆에 있던 괴물이
        /// 재개 즉시 물어 회의가 사실상 사망 선고가 된다
        /// (밸런스 §2, docs/qa-and-playtest-plan.md §4.9).
        /// </summary>
        private void ServerApplyPostMeetingBiteProtection()
        {
            if (!IsServer || _config == null || NetworkManager == null)
            {
                return;
            }

            var protectionSeconds = _config.PostMeetingBiteProtectionSeconds;
            if (protectionSeconds <= 0f)
            {
                return;
            }

            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject != null &&
                    playerObject.TryGetComponent<MonsterTarget>(
                        out var target))
                {
                    target.ApplyBiteProtection(Time.time, protectionSeconds);
                }
            }
        }

        /// <summary>빌런 이탈로 끝났는지다. 결과 화면 문구에만 쓴다.</summary>
        public bool IsVillainAbandoned => _isVillainAbandoned.Value;

        public bool IsMeetingActive =>
            Phase is RoundPhase.MeetingDiscussion or
                RoundPhase.MeetingVote or
                RoundPhase.MeetingResult;

        public int UsedMeetingCount => _stateMachine?.UsedMeetingCount ?? 0;
        public float ElapsedExplorationSeconds =>
            _stateMachine?.ElapsedExplorationSeconds ?? 0f;
        public float SecondsSinceLastMeeting =>
            _stateMachine?.SecondsSinceLastMeeting ?? 0f;

        /// <summary>
        /// 빌런이 유예 시간 안에 돌아오지 않아 생존자 승리로 확정한다(GDD §19.2).
        /// 결과와 판정 우선순위는 빌런 퇴출과 같으므로 같은 입력을 재사용하고,
        /// 결과 화면 문구만 구분하기 위해 별도 플래그를 복제한다.
        /// </summary>
        public bool ServerApplyVillainAbandonment()
        {
            if (!IsServer || _stateMachine == null || _isVillainExiled)
            {
                return false;
            }

            _isVillainExiled = true;
            _isVillainAbandoned.Value = true;
            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
            return true;
        }

        /// <summary>참가자 이탈이 확정된 뒤 승패를 다시 확인한다.</summary>
        public void ServerReevaluateWinConditions()
        {
            if (!IsServer || _stateMachine == null || _stateMachine.HasEnded)
            {
                return;
            }

            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
        }

        /// <summary>
        /// 재접속 유예가 끝난 생존자의 미완료 개인 미션을 같은 스테이션의
        /// 공용 복구 미션으로 전환한다(GDD §19.2, SDD §17.2).
        /// 원래 소유자와 점수 예산은 서버에만 남기고, 클라이언트에는
        /// 상호작용에 필요한 스테이션 ID만 공개한다.
        /// </summary>
        public int ServerRegisterRecoveryMissions(
            ulong sourceClientId,
            IReadOnlyList<ulong> assignedMissionIds,
            IReadOnlyList<ulong> completedMissionIds)
        {
            if (!IsServer || _projectProgress == null ||
                assignedMissionIds == null ||
                assignedMissionIds.Count == 0 ||
                completedMissionIds == null)
            {
                return 0;
            }

            var completed = new HashSet<ulong>(completedMissionIds);
            var addedCount = 0;
            for (var index = 0; index < assignedMissionIds.Count; index++)
            {
                var missionId = assignedMissionIds[index];
                if (completed.Contains(missionId) ||
                    !IsRegisteredMissionStation(missionId))
                {
                    continue;
                }

                _recoveryMissions.Add(
                    new RecoveryMissionRecord(
                        sourceClientId,
                        missionId,
                        assignedMissionIds.Count));
                _recoveryMissionIds.Add(missionId);
                addedCount++;
            }

            return addedCount;
        }

        public bool HasRecoveryMission(ulong missionId)
        {
            return _recoveryMissionIds.Contains(missionId);
        }

        public ulong GetRecoveryMissionId(int index)
        {
            return index >= 0 && index < _recoveryMissionIds.Count
                ? _recoveryMissionIds[index]
                : 0UL;
        }

        /// <summary>
        /// 공용 복구 미션은 수행자의 개인 예산이 아니라 이탈한 원래 생존자의
        /// 남은 개인 예산으로 점수를 계산한다. 같은 스테이션이 여러 명에게서
        /// 넘어온 경우 한 번 완료할 때 가장 먼저 등록된 한 건만 소비한다.
        /// </summary>
        public bool ServerTryCompleteRecoveryMission(
            ulong completingClientId,
            ulong missionId,
            out int awardedPoints)
        {
            awardedPoints = 0;
            if (!IsServer || _stateMachine == null ||
                _projectProgress == null || !AllowsMissionInteraction ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    completingClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Survivor ||
                (client.PlayerObject.GetComponent<InfectionService>()?.State ??
                 PlayerLifeState.AliveHealthy) == PlayerLifeState.DeadGhost)
            {
                return false;
            }

            var recoveryIndex = -1;
            for (var index = 0; index < _recoveryMissions.Count; index++)
            {
                if (_recoveryMissions[index].MissionId == missionId)
                {
                    recoveryIndex = index;
                    break;
                }
            }

            if (recoveryIndex < 0)
            {
                return false;
            }

            var recovery = _recoveryMissions[recoveryIndex];
            if (!_projectProgress.TryCompleteMission(
                    recovery.SourceClientId,
                    recovery.MissionId,
                    recovery.AssignedMissionCount,
                    out awardedPoints))
            {
                return false;
            }

            _recoveryMissions.RemoveAt(recoveryIndex);
            _recoveryMissionIds.RemoveAt(recoveryIndex);
            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
            return true;
        }

        private bool IsRegisteredMissionStation(ulong missionId)
        {
            if (_missionStations == null)
            {
                return false;
            }

            for (var index = 0; index < _missionStations.Length; index++)
            {
                var station = _missionStations[index];
                if (station != null && station.NetworkObjectId == missionId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Unity PlayerId는 같지만 NGO clientId가 달라진 재접속자의 서버 전용 이력을
        /// 한 번에 새 ID로 이전한다. 역할·감염 등 PlayerObject 상태 복원 직후 호출한다.
        /// </summary>
        public void ServerRebindPlayer(
            ulong previousClientId,
            ulong currentClientId)
        {
            if (!IsServer || previousClientId == currentClientId)
            {
                return;
            }

            _projectProgress?.RebindPlayer(previousClientId, currentClientId);
            if (_missionStations != null)
            {
                foreach (var station in _missionStations)
                {
                    station?.ServerRebindPlayer(
                        previousClientId,
                        currentClientId);
                }
            }

            // 배합 코드는 저장하지 않는 개인 기억 정보라 재접속 시 이어받지 않는다
            // (NetworkAntidoteInventoryAuthority.ServerRestoreReconnectSnapshot).
            NetworkMeetingAuthority.Current?.ServerRebindPlayer(
                previousClientId,
                currentClientId);
        }

        /// <summary>
        /// 결과 화면에서 호스트가 로비 복귀를 요청한다(mvp-scope §3.2, GDD §20).
        /// 이 경로가 없으면 한 판이 끝난 뒤 세션을 다시 만들어야 하므로
        /// "연속 3판 완주"(mvp-scope §8)를 만족할 수 없다.
        /// </summary>
        public void RequestReturnToLobby()
        {
            if (IsSpawned && Phase == RoundPhase.RoundResult)
            {
                RequestReturnToLobbyRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestReturnToLobbyRpc(RpcParams rpcParams = default)
        {
            // 호스트만 판을 끝낼 수 있다. 참가자가 임의로 로비로 되돌리면
            // 다른 사람의 결과 화면이 사라진다.
            if (rpcParams.Receive.SenderClientId !=
                    NetworkManager.ServerClientId ||
                Phase != RoundPhase.RoundResult ||
                _isReturningToLobby)
            {
                return;
            }

            var sceneManager = NetworkManager?.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogError(
                    "[Round] Scene management is disabled; cannot return to the lobby.",
                    this);
                return;
            }

            _isReturningToLobby = true;
            var status = sceneManager.LoadScene(
                NetworkPlayerAvatar.MainMenuSceneName,
                LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
            {
                return;
            }

            _isReturningToLobby = false;
            Debug.LogError(
                $"[Round] Returning to the lobby failed: {status}.",
                this);
        }

        /// <summary>회의 호출을 서버에서 확정한다.</summary>
        public bool ServerTryBeginMeeting()
        {
            if (!IsServer || _stateMachine == null ||
                !_stateMachine.TryBeginMeeting())
            {
                return false;
            }

            PublishServerState();
            return true;
        }

        /// <summary>
        /// 전원 투표 완료로 결과 단계를 앞당긴다.
        /// 집계는 호출한 쪽(NetworkMeetingAuthority)이 이어서 수행한다.
        /// </summary>
        public bool ServerTryFinishVoteEarly()
        {
            if (!IsServer || _stateMachine == null ||
                !_stateMachine.TryFinishVoteEarly())
            {
                return false;
            }

            PublishServerState();
            return true;
        }

        /// <summary>
        /// 퇴출을 확정한다. 빌런이 퇴출되면 즉시 생존자 승리다(GDD §16.4).
        /// 역할은 라운드 종료 전까지 공개하지 않으므로 여기서 역할을 방송하지 않는다.
        /// </summary>
        public bool ServerApplyExile(ulong exiledClientId)
        {
            if (!IsServer || _stateMachine == null ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    exiledClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar))
            {
                return false;
            }

            if (avatar.Role == PlayerRole.Villain)
            {
                _isVillainExiled = true;
            }
            else
            {
                // 생존자는 유령이 되어 자기 미션만 계속한다(GDD §16.4).
                client.PlayerObject
                    .GetComponent<NetworkInfectionAuthority>()?
                    .ServerForceGhost();
            }

            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
            return true;
        }

        /// <summary>
        /// 빌런의 위장 미션 완료다. 개인 목록에는 완료로 남기지만
        /// 프로젝트 진행률은 절대 올리지 않는다(GDD §9.1).
        /// 겉보기 연출은 생존자와 같아야 위장이 성립하므로 완료 자체는 승인한다.
        /// </summary>
        public bool ServerTryCompleteFakeMission(
            ulong playerClientId,
            ulong missionId)
        {
            if (!IsServer || _stateMachine == null ||
                !AllowsMissionInteraction ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    playerClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Villain ||
                !client.PlayerObject.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                !journal.IsAssigned(missionId) ||
                journal.IsCompleted(missionId))
            {
                return false;
            }

            // ProjectProgressService를 건드리지 않는 것이 이 메서드의 핵심이다.
            return journal.ServerMarkCompleted(missionId);
        }

        public bool ServerTryCompleteMission(
            ulong playerClientId,
            ulong missionId,
            out int awardedPoints)
        {
            awardedPoints = 0;
            if (!IsServer || _stateMachine == null ||
                _projectProgress == null ||
                !AllowsMissionInteraction ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    playerClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                // 이 검사가 진행률을 지키는 마지막 방어선이다. 빌런은 여기서 막힌다.
                avatar.Role != PlayerRole.Survivor ||
                !client.PlayerObject.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                !journal.IsAssigned(missionId) ||
                journal.IsCompleted(missionId))
            {
                return false;
            }

            if (!journal.ServerMarkCompleted(missionId))
            {
                return false;
            }

            if (!_projectProgress.TryCompleteMission(
                    playerClientId,
                    missionId,
                    journal.AssignedCount,
                    out awardedPoints))
            {
                return false;
            }

            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
            return true;
        }

        public void SkipToExplorationForDevelopment()
        {
            if (!CanUseDevelopmentControls || _stateMachine == null)
            {
                return;
            }

            _stateMachine.SkipToExplorationForDevelopment();
            PublishServerState();
        }

        public void AddQuarterProgressForDevelopment()
        {
            if (!CanUseDevelopmentControls || _stateMachine == null ||
                _projectProgress == null)
            {
                return;
            }

            _projectProgress.AddDevelopmentPoints(
                _config.ProjectMaximumPoints / 4);
            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
        }

        public void SetFiveSecondTimeoutForDevelopment()
        {
            if (!CanUseDevelopmentControls || _stateMachine == null)
            {
                return;
            }

            _stateMachine.SkipToExplorationForDevelopment();
            _stateMachine.SetRemainingRoundSecondsForDevelopment(5f);
            PublishServerState();
        }

        public void SetProjectProgressForDevelopment(int percent)
        {
            if (!CanUseDevelopmentControls || _stateMachine == null ||
                _projectProgress == null)
            {
                return;
            }

            var clampedPercent = Mathf.Clamp(percent, 0, 100);
            _projectProgress.SetDevelopmentPoints(
                Mathf.RoundToInt(
                    _config.ProjectMaximumPoints * clampedPercent / 100f));
            _stateMachine.EvaluateWinConditions(CreateWinSnapshot());
            PublishServerState();
        }

        public void SetRemainingRoundSecondsForDevelopment(float seconds)
        {
            if (!CanUseDevelopmentControls || _stateMachine == null)
            {
                return;
            }

            _stateMachine.SkipToExplorationForDevelopment();
            _stateMachine.SetRemainingRoundSecondsForDevelopment(seconds);
            PublishServerState();
        }

        public void ResetMeetingCooldownForDevelopment()
        {
            if (!CanUseDevelopmentControls || _stateMachine == null)
            {
                return;
            }

            _stateMachine.SkipToExplorationForDevelopment();
            _stateMachine.ResetMeetingCooldownForDevelopment();
            PublishServerState();
        }

        public bool SetPlayerRoleForDevelopment(
            ulong targetClientId,
            PlayerRole role)
        {
            if (!CanUseDevelopmentControls || NetworkManager == null ||
                (role != PlayerRole.Survivor &&
                 role != PlayerRole.Villain) ||
                !TryGetDevelopmentPlayer(
                    targetClientId,
                    out var targetPlayer))
            {
                return false;
            }

            if (role == PlayerRole.Villain)
            {
                foreach (var pair in NetworkManager.ConnectedClients)
                {
                    var playerObject = pair.Value?.PlayerObject;
                    if (playerObject == null || playerObject == targetPlayer ||
                        !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                            out var otherAvatar) ||
                        otherAvatar.Role != PlayerRole.Villain)
                    {
                        continue;
                    }

                    otherAvatar.ServerAssignRole(PlayerRole.Survivor);
                }
            }

            var changed = targetPlayer
                .GetComponent<NetworkPlayerAvatar>()
                .ServerAssignRole(role);
            NetworkVillainUpgradeAuthority.Current?
                .ServerPublishCurrentStateToVillain();
            return changed;
        }

        public bool SetPlayerLifeStateForDevelopment(
            ulong targetClientId,
            PlayerLifeState lifeState)
        {
            if (!CanUseDevelopmentControls ||
                !TryGetDevelopmentPlayer(targetClientId, out var player) ||
                !player.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection))
            {
                return false;
            }

            return lifeState switch
            {
                PlayerLifeState.AliveHealthy =>
                    infection.ServerCureForDevelopment(),
                PlayerLifeState.AliveInfected =>
                    infection.ServerInfectForDevelopment(),
                PlayerLifeState.DeadGhost => infection.ServerForceGhost(),
                _ => false
            };
        }

        public bool GiveAntidoteForDevelopment(ulong targetClientId)
        {
            if (!CanUseDevelopmentControls ||
                !TryGetDevelopmentPlayer(targetClientId, out var player) ||
                !player.TryGetComponent<NetworkAntidoteInventoryAuthority>(
                    out var inventory))
            {
                return false;
            }

            return inventory.ServerTryAddAntidote();
        }

        public void ForceOutcomeForDevelopment(RoundOutcome outcome)
        {
            if (!CanUseDevelopmentControls || _stateMachine == null)
            {
                return;
            }

            var reason = outcome == RoundOutcome.SurvivorsWin
                ? RoundEndReason.ProjectCompleted
                : RoundEndReason.TimeExpired;
            _stateMachine.ForceOutcomeForDevelopment(outcome, reason);
            PublishServerState();
        }

        private bool TryGetDevelopmentPlayer(
            ulong clientId,
            out GameObject playerObject)
        {
            playerObject = null;
            if (NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject.gameObject;
            return true;
        }

        private RoundWinSnapshot CreateWinSnapshot()
        {
            return new RoundWinSnapshot(
                _isVillainExiled,
                _projectProgress?.Points ?? 0,
                _config.ProjectMaximumPoints,
                CountRealSurvivors(),
                _stateMachine?.RemainingRoundSeconds ??
                _config.ExplorationDurationSeconds);
        }

        private bool TryInitializeServerRound()
        {
            if (!IsServer || _config == null ||
                _missionStations == null ||
                _missionStations.Length <
                _config.DefaultAssignedMissionCount)
            {
                return false;
            }

            var missionCandidates =
                new MissionAssignmentCandidate[_missionStations.Length];
            for (var index = 0; index < _missionStations.Length; index++)
            {
                var station = _missionStations[index];
                if (station == null || !station.IsSpawned)
                {
                    return false;
                }

                missionCandidates[index] =
                    new MissionAssignmentCandidate(
                        station.NetworkObjectId,
                        station.transform.position,
                        station.Station.Kind);
            }

            if (NetworkManager == null)
            {
                return false;
            }

            foreach (var client in NetworkManager.ConnectedClients.Values)
            {
                var playerObject = client.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    !avatar.HasAssignedRole ||
                    !playerObject.TryGetComponent<
                        NetworkPlayerMissionJournal>(out var journal))
                {
                    return false;
                }

                // 빌런에게도 같은 방식으로 목록을 배정한다. 겉보기로는 생존자와
                // 구분되지 않아야 위장이 성립한다(GDD §9.1). 진행률 반영은
                // ServerTryCompleteMission에서 역할로 막는다.
                var startPosition =
                    (Vector2)playerObject.transform.position;
                if (NetworkPlayerSpawnLayout.TryGetLaboratoryPosition(
                        avatar.SlotIndex,
                        out var configuredStartPosition))
                {
                    startPosition = configuredStartPosition;
                }

                var assignedMissionIds =
                    MissionAssignmentOrderService
                        .SelectDifficultyAdjustedAssignments(
                        startPosition,
                        missionCandidates,
                        _config.DifficultAssignedMissionCount,
                        _config.DefaultAssignedMissionCount,
                        _config.MinimumMissionKindCount);
                if (!journal.ServerAssignMissions(assignedMissionIds))
                {
                    return false;
                }
            }

            _stateMachine = new RoundStateMachine(_config);
            _projectProgress = new ProjectProgressService(
                _config.ProjectMaximumPoints,
                _config.SurvivorPersonalBudgetPoints);
            _recoveryMissions.Clear();
            _recoveryMissionIds.Clear();
            _isServerRoundInitialized = true;
            PublishServerState();
            return true;
        }

        private readonly struct RecoveryMissionRecord
        {
            public RecoveryMissionRecord(
                ulong sourceClientId,
                ulong missionId,
                int assignedMissionCount)
            {
                SourceClientId = sourceClientId;
                MissionId = missionId;
                AssignedMissionCount = assignedMissionCount;
            }

            public ulong SourceClientId { get; }
            public ulong MissionId { get; }
            public int AssignedMissionCount { get; }
        }

        private int CountRealSurvivors()
        {
            if (NetworkManager == null)
            {
                return 0;
            }

            // 유예를 기다리는 생존자는 아직 살아 있는 것으로 센다(GDD §19.2).
            // 그러지 않으면 마지막 생존자가 잠깐 끊겼을 때 즉시 빌런 승리가 된다.
            var survivorCount =
                NetworkDisconnectPolicyAuthority.Current?
                    .PendingSurvivorCount ?? 0;
            foreach (var client in NetworkManager.ConnectedClients.Values)
            {
                var playerObject = client.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    avatar.Role != PlayerRole.Survivor)
                {
                    continue;
                }

                var infection =
                    playerObject.GetComponent<InfectionService>();
                if (infection == null ||
                    infection.State != PlayerLifeState.DeadGhost)
                {
                    survivorCount++;
                }
            }

            return survivorCount;
        }

        private void PublishServerState()
        {
            if (!IsServer || _stateMachine == null)
            {
                return;
            }

            _phase.Value = _stateMachine.Phase;
            _remainingPhaseSeconds.Value =
                _stateMachine.RemainingPhaseSeconds;
            _remainingRoundSeconds.Value =
                _stateMachine.RemainingRoundSeconds;
            _projectPoints.Value = _projectProgress?.Points ?? 0;
            _projectMilestone.Value =
                _projectProgress?.Milestone ?? ProjectMilestone.None;
            _outcome.Value = _stateMachine.Outcome;
            _endReason.Value = _stateMachine.EndReason;
            ApplyReplicatedState();
            if (_stateMachine.Phase == RoundPhase.RoundResult)
            {
                NetworkRoundSummaryAuthority.Current?
                    .ServerPublishSummary();
            }
        }

        private void ApplyReplicatedState()
        {
            _localRoundPhase?.ApplyAuthoritativePhase(
                Phase,
                RemainingPhaseSeconds);
            StateChanged?.Invoke();
        }

        private void Subscribe()
        {
            _phase.OnValueChanged += HandlePhaseChanged;
            _remainingPhaseSeconds.OnValueChanged += HandleFloatChanged;
            _remainingRoundSeconds.OnValueChanged += HandleFloatChanged;
            _projectPoints.OnValueChanged += HandleIntChanged;
            _projectMilestone.OnValueChanged += HandleMilestoneChanged;
            _outcome.OnValueChanged += HandleOutcomeChanged;
            _endReason.OnValueChanged += HandleEndReasonChanged;
        }

        private void Unsubscribe()
        {
            _phase.OnValueChanged -= HandlePhaseChanged;
            _remainingPhaseSeconds.OnValueChanged -= HandleFloatChanged;
            _remainingRoundSeconds.OnValueChanged -= HandleFloatChanged;
            _projectPoints.OnValueChanged -= HandleIntChanged;
            _projectMilestone.OnValueChanged -= HandleMilestoneChanged;
            _outcome.OnValueChanged -= HandleOutcomeChanged;
            _endReason.OnValueChanged -= HandleEndReasonChanged;
        }

        private void HandlePhaseChanged(
            RoundPhase previousValue,
            RoundPhase currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleFloatChanged(
            float previousValue,
            float currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleIntChanged(int previousValue, int currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleMilestoneChanged(
            ProjectMilestone previousValue,
            ProjectMilestone currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleOutcomeChanged(
            RoundOutcome previousValue,
            RoundOutcome currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleEndReasonChanged(
            RoundEndReason previousValue,
            RoundEndReason currentValue)
        {
            ApplyReplicatedState();
        }
    }
}
