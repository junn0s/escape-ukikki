using System;
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

        private RoundStateMachine _stateMachine;
        private ProjectProgressService _projectProgress;
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
                    _config.DefaultAssignedMissionCount,
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
                _missionStations.Length !=
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
                        station.transform.position);
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

                var orderedMissionIds =
                    MissionAssignmentOrderService.OrderByDistance(
                        startPosition,
                        missionCandidates);
                if (!journal.ServerAssignMissions(orderedMissionIds))
                {
                    return false;
                }
            }

            _stateMachine = new RoundStateMachine(_config);
            _projectProgress = new ProjectProgressService(
                _config.ProjectMaximumPoints,
                _config.SurvivorPersonalBudgetPoints);
            _isServerRoundInitialized = true;
            PublishServerState();
            return true;
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
