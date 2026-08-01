using System;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

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
        private float _nextServerPublishTime;
        private bool _isServerRoundInitialized;

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

            var phaseChanged = _stateMachine.Tick(
                Time.deltaTime,
                CreateWinSnapshot());
            if (phaseChanged || Time.unscaledTime >= _nextServerPublishTime)
            {
                _nextServerPublishTime = Time.unscaledTime + 0.1f;
                PublishServerState();
            }
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
                isVillainExiled: false,
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

            var survivorCount = 0;
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
