using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FuseStationPrototype))]
    public sealed class NetworkFuseStationAuthority : NetworkBehaviour
    {
        public const ulong NoOccupantClientId = ulong.MaxValue;
        private const float DistanceReleaseToleranceMeters = 0.25f;
        private const float CarryMovementActivitySqrThreshold = 0.0025f;
        private const int MaxPathHits = 16;

        [SerializeField] private FuseStationPrototype _station;
        [SerializeField] private InteractionBalanceConfig _config;

        private readonly NetworkVariable<ulong> _occupantClientId = new(
            NoOccupantClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<ulong, uint> _lastProcessedSequences =
            new();
        private readonly HashSet<ulong> _completedClientIds = new();
        private readonly RaycastHit2D[] _pathHits = new RaycastHit2D[MaxPathHits];

        private uint _localSequence;
        private double _lastServerActivityTime;
        private bool _isLocallyCompleted;
        private bool _occupantIsRecoveryMission;
        private MissionValidationSession _serverMission;
        private Vector2 _lastOccupantPosition;

        public FuseStationPrototype Station => _station;
        public InteractionBalanceConfig Config => _config;
        public ulong OccupantClientId => _occupantClientId.Value;
        public bool IsOccupied =>
            OccupantClientId != NoOccupantClientId;
        public bool IsCompleted => _isLocallyCompleted;
        public event System.Action PublicVisualStateChanged;
        public event System.Action PublicMissionCompleted;

        public void Configure(
            FuseStationPrototype station,
            InteractionBalanceConfig config)
        {
            _station = station;
            _config = config;
        }

        public override void OnNetworkSpawn()
        {
            if (_station == null || _config == null)
            {
                Debug.LogError(
                    "[Interaction] Fuse station authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                CanLocalPlayerRequestInteraction,
                RequestInteraction);
            _station.ProgressChanged += HandleMissionProgressChanged;
            _station.MissionCancelled += HandleMissionCancelled;
            _station.MissionFailed += HandleMissionFailed;
            _station.MissionCompleted += HandleMissionCompleted;
            _station.MissionInputSubmitted += HandleMissionInputSubmitted;
            _occupantClientId.OnValueChanged += HandleOccupantChanged;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback +=
                    HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
                _station.ProgressChanged -= HandleMissionProgressChanged;
                _station.MissionCancelled -= HandleMissionCancelled;
                _station.MissionFailed -= HandleMissionFailed;
                _station.MissionCompleted -= HandleMissionCompleted;
                _station.MissionInputSubmitted -= HandleMissionInputSubmitted;
            }

            _occupantClientId.OnValueChanged -= HandleOccupantChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            _lastProcessedSequences.Clear();
            _completedClientIds.Clear();
            _isLocallyCompleted = false;
            _occupantIsRecoveryMission = false;
            _serverMission = null;
        }

        private void Update()
        {
            if (!IsServer || !IsOccupied || NetworkManager == null)
            {
                return;
            }

            if (!NetworkManager.ConnectedClients.TryGetValue(
                    OccupantClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                ServerReleaseOccupancy(cancelClientMission: false);
                return;
            }

            var isCarryingBattery =
                _serverMission?.IsCarryingBattery == true;
            var infection =
                client.PlayerObject.GetComponent<InfectionService>();
            if (infection != null &&
                infection.State == PlayerLifeState.DeadGhost &&
                _occupantIsRecoveryMission)
            {
                // 유령은 자기 개인 미션은 계속할 수 있지만 공용 복구 목록은
                // 공용 패널 취급이라 조작할 수 없다(GDD §17).
                if (isCarryingBattery)
                {
                    ServerInterruptBatteryCarry(OccupantClientId);
                }
                else
                {
                    ServerReleaseOccupancy(cancelClientMission: true);
                }

                return;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                ServerReleaseOccupancy(cancelClientMission: true);
                return;
            }

            var range =
                _config.GeneralInteractionRangeMeters +
                DistanceReleaseToleranceMeters;
            var playerPosition =
                (Vector2)client.PlayerObject.transform.position;
            if (isCarryingBattery)
            {
                if ((playerPosition - _lastOccupantPosition).sqrMagnitude >=
                    CarryMovementActivitySqrThreshold)
                {
                    _lastOccupantPosition = playerPosition;
                    _lastServerActivityTime = Time.unscaledTimeAsDouble;
                }
            }
            else
            {
                var stationPosition =
                    (Vector2)_station.transform.position;
                if ((playerPosition - stationPosition).sqrMagnitude >
                    range * range)
                {
                    ServerReleaseOccupancy(cancelClientMission: true);
                    return;
                }
            }

            if (Time.unscaledTimeAsDouble - _lastServerActivityTime >=
                _config.ExclusiveOccupancyTimeoutSeconds)
            {
                if (isCarryingBattery)
                {
                    ServerInterruptBatteryCarry(OccupantClientId);
                }
                else
                {
                    ServerReleaseOccupancy(cancelClientMission: true);
                }
            }
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || _station == null ||
                interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner ||
                !interactor.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                !avatar.HasAssignedRole ||
                !interactor.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal))
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null &&
                !roundState.AllowsMissionInteraction)
            {
                return false;
            }

            var hasPersonalMission =
                journal.IsAssigned(NetworkObjectId) &&
                !journal.IsCompleted(NetworkObjectId);
            var hasRecoveryMission =
                avatar.Role == PlayerRole.Survivor &&
                (interactor.GetComponent<InfectionService>()?.State ??
                 PlayerLifeState.AliveHealthy) != PlayerLifeState.DeadGhost &&
                roundState != null &&
                roundState.HasRecoveryMission(NetworkObjectId);
            if (!hasPersonalMission && !hasRecoveryMission)
            {
                return false;
            }

            return !IsOccupied ||
                   OccupantClientId == playerNetworkObject.OwnerClientId;
        }

        private void RequestInteraction(GameObject interactor)
        {
            if (!CanLocalPlayerRequestInteraction(interactor) ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject))
            {
                return;
            }

            RequestInteractionRpc(
                playerNetworkObject.NetworkObjectId,
                NextLocalSequence());
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractionRpc(
            ulong playerNetworkObjectId,
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _config == null)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            var hasPlayer =
                NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) &&
                client.PlayerObject != null;
            var playerObject = hasPlayer ? client.PlayerObject : null;
            var isOwnedBySender =
                playerObject != null &&
                playerObject.NetworkObjectId == playerNetworkObjectId &&
                playerObject.OwnerClientId == senderClientId;
            var lastSequence = _lastProcessedSequences.TryGetValue(
                senderClientId,
                out var storedSequence)
                ? storedSequence
                : 0u;
            var playerPosition = playerObject != null
                ? (Vector2)playerObject.transform.position
                : new Vector2(float.MaxValue, float.MaxValue);
            var stationPosition = (Vector2)_station.transform.position;
            var avatar = playerObject != null
                ? playerObject.GetComponent<NetworkPlayerAvatar>()
                : null;
            var journal = playerObject != null
                ? playerObject.GetComponent<NetworkPlayerMissionJournal>()
                : null;
            var roundState = NetworkRoundState.Current;
            var canUsePersonalMission =
                !_completedClientIds.Contains(senderClientId) &&
                journal != null &&
                journal.IsAssigned(NetworkObjectId) &&
                !journal.IsCompleted(NetworkObjectId);
            var canUseRecoveryMission =
                avatar != null &&
                avatar.Role == PlayerRole.Survivor &&
                (playerObject.GetComponent<InfectionService>()?.State ??
                 PlayerLifeState.AliveHealthy) != PlayerLifeState.DeadGhost &&
                roundState != null &&
                roundState.HasRecoveryMission(NetworkObjectId);
            var canUseMission =
                (canUsePersonalMission || canUseRecoveryMission) &&
                (roundState == null ||
                 roundState.AllowsMissionInteraction) &&
                (roundState == null ||
                 (avatar != null && avatar.HasAssignedRole));
            var rejectionReason = NetworkInteractionRules.Validate(
                isOwnedBySender,
                clientSequence,
                lastSequence,
                _station.isActiveAndEnabled &&
                _station.IsMissionConfigured && canUseMission,
                // 같은 클라이언트의 중복 요청도 서버 퍼즐 시드를 바꾸므로 막는다.
                IsOccupied,
                (playerPosition - stationPosition).sqrMagnitude,
                _config.GeneralInteractionRangeMeters,
                playerObject != null &&
                HasUnblockedPath(
                    playerObject.transform,
                    _station.transform));

            if (rejectionReason != InteractionRejectionReason.InvalidOwner &&
                rejectionReason != InteractionRejectionReason.StaleSequence)
            {
                _lastProcessedSequences[senderClientId] = clientSequence;
            }

            if (rejectionReason != InteractionRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, rejectionReason);
                return;
            }

            _occupantClientId.Value = senderClientId;
            _occupantIsRecoveryMission =
                !canUsePersonalMission && canUseRecoveryMission;
            playerObject
                .GetComponent<NetworkPlayerMissionJournal>()?
                .ServerSetMissionActivity(true);
            _lastServerActivityTime = Time.unscaledTimeAsDouble;
            _lastOccupantPosition = playerPosition;
            var challengeSeed = Random.Range(1, int.MaxValue);
            var challengeStartedAt = NetworkManager.ServerTime.Time;
            _serverMission = new MissionValidationSession(
                _station.Kind,
                _station.Config.FuseCount,
                _station.Config.SampleCategoryCount,
                _station.Config.BreakerCycleSeconds,
                _station.Config.BreakerServerToleranceNormalized,
                _station.Config.PressureTargetNormalized,
                _station.Config.PressureToleranceNormalized,
                _station.Config.PressureServerStabilizeSeconds,
                challengeSeed,
                challengeStartedAt);
            ApproveInteractionRpc(
                senderClientId,
                clientSequence,
                challengeSeed,
                challengeStartedAt);
        }

        [Rpc(SendTo.Server)]
        private void SubmitMissionInputRpc(
            MissionInputAction action,
            int primaryValue,
            int secondaryValue,
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsOccupied || OccupantClientId != senderClientId ||
                _serverMission == null ||
                !IsNewSequence(senderClientId, clientSequence) ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;
            var playerObject = client.PlayerObject;
            var range = _config.GeneralInteractionRangeMeters +
                        DistanceReleaseToleranceMeters;
            var requiresTargetRange =
                action != MissionInputAction.BatteryDrop;
            var validationTarget =
                action == MissionInputAction.BatteryInsert
                    ? _station.BatteryReceiverTransform
                    : _station.transform;
            var isInRange = !requiresTargetRange ||
                            (validationTarget != null &&
                             ((Vector2)playerObject.transform.position -
                              (Vector2)validationTarget.position)
                             .sqrMagnitude <= range * range);
            var hasUnblockedPath = !requiresTargetRange ||
                                   (validationTarget != null &&
                                    HasUnblockedPath(
                                        playerObject.transform,
                                        validationTarget));
            var roundState = NetworkRoundState.Current;
            if (!isInRange || !hasUnblockedPath ||
                (roundState != null && !roundState.AllowsMissionInteraction))
            {
                ServerReleaseOccupancy(cancelClientMission: true);
                return;
            }

            _lastServerActivityTime = Time.unscaledTimeAsDouble;
            var result = _serverMission.Validate(
                new MissionInputCommand(action, primaryValue, secondaryValue),
                NetworkManager.ServerTime.Time);
            switch (result)
            {
                case FuseMissionInputResult.Failed:
                    ServerHandleMissionFailure(senderClientId, action);
                    break;
                case FuseMissionInputResult.Completed:
                    ServerAcceptMissionCompletion(senderClientId);
                    break;
            }
        }

        [Rpc(SendTo.Server)]
        private void ReleaseOccupancyRpc(
            uint clientSequence,
            bool completed,
            bool failed,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsOccupied || OccupantClientId != senderClientId ||
                !IsNewSequence(senderClientId, clientSequence))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;
            // 성공·실패 bool은 더 이상 신뢰하지 않는다. 실제 조작 입력은
            // SubmitMissionInputRpc에서 서버 퍼즐 인스턴스로 검증한다.
            if (completed || failed)
            {
                return;
            }

            ServerReleaseOccupancy(cancelClientMission: false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ApproveInteractionRpc(
            ulong targetClientId,
            uint approvedSequence,
            int challengeSeed,
            double challengeStartedAt)
        {
            if (NetworkManager == null ||
                NetworkManager.LocalClientId != targetClientId ||
                approvedSequence != _localSequence)
            {
                return;
            }

            var localPlayer = NetworkManager.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                var elapsedServerSeconds = System.Math.Max(
                    0d,
                    NetworkManager.ServerTime.Time - challengeStartedAt);
                _station.BeginApprovedNetworkInteraction(
                    localPlayer.gameObject,
                    challengeSeed,
                    Time.unscaledTimeAsDouble - elapsedServerSeconds);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CancelInteractionRpc(
            ulong targetClientId,
            bool batteryDropped)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId &&
                _station != null &&
                _station.IsMissionActive)
            {
                _station.ApplyAuthoritativeInterruption(batteryDropped);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            InteractionRejectionReason rejectionReason)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                Debug.LogWarning(
                    $"[Interaction] Request rejected: {rejectionReason}.",
                    this);
            }
        }

        private bool HasUnblockedPath(
            Transform player,
            Transform target)
        {
            var playerPosition = (Vector2)player.position;
            var targetPosition = (Vector2)target.position;
            var hitCount = Physics2D.Linecast(
                playerPosition,
                targetPosition,
                ContactFilter2D.noFilter,
                _pathHits);
            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = _pathHits[index].collider;
                if (hitCollider == null || hitCollider.isTrigger ||
                    hitCollider.transform == player ||
                    hitCollider.transform.IsChildOf(player) ||
                    hitCollider.transform == target ||
                    hitCollider.transform.IsChildOf(target))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsNewSequence(
            ulong clientId,
            uint clientSequence)
        {
            return !_lastProcessedSequences.TryGetValue(
                       clientId,
                       out var previousSequence) ||
                   clientSequence > previousSequence;
        }

        private uint NextLocalSequence()
        {
            _localSequence++;
            if (_localSequence == 0)
            {
                _localSequence = 1;
            }

            return _localSequence;
        }

        private void HandleMissionProgressChanged(
            FuseStationPrototype station)
        {
            // 입력 RPC 자체가 점유 시간을 갱신한다.
        }

        private void HandleMissionCancelled(FuseStationPrototype station)
        {
            ReleaseLocalOccupancy(
                completed: false,
                failed: false);
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedFuseId,
            int expectedFuseId)
        {
            // 서버 입력 검증 결과가 실패와 점유 해제를 확정한다.
        }

        private void HandleMissionCompleted(FuseStationPrototype station)
        {
            // 서버 입력 검증 결과가 완료와 프로젝트 점수를 확정한다.
        }

        private void HandleMissionInputSubmitted(
            FuseStationPrototype station,
            MissionInputCommand command)
        {
            if (!IsLocalOccupant())
            {
                return;
            }

            SubmitMissionInputRpc(
                command.Action,
                command.PrimaryValue,
                command.SecondaryValue,
                NextLocalSequence());
        }

        private void ReleaseLocalOccupancy(
            bool completed,
            bool failed)
        {
            if (IsLocalOccupant())
            {
                ReleaseOccupancyRpc(
                    NextLocalSequence(),
                    completed,
                    failed);
            }
        }

        private bool IsLocalOccupant()
        {
            return IsSpawned &&
                   NetworkManager != null &&
                   IsOccupied &&
                   OccupantClientId == NetworkManager.LocalClientId;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _lastProcessedSequences.Remove(clientId);
            if (IsOccupied && OccupantClientId == clientId)
            {
                ServerReleaseOccupancy(cancelClientMission: false);
            }
        }

        private void ServerReleaseOccupancy(bool cancelClientMission)
        {
            if (!IsServer || !IsOccupied)
            {
                return;
            }

            var previousOccupant = OccupantClientId;
            _occupantClientId.Value = NoOccupantClientId;
            _occupantIsRecoveryMission = false;
            _serverMission = null;
            if (NetworkManager != null &&
                NetworkManager.ConnectedClients.TryGetValue(
                    previousOccupant,
                    out var client) &&
                client.PlayerObject != null)
            {
                client.PlayerObject
                    .GetComponent<NetworkPlayerMissionJournal>()?
                    .ServerSetMissionActivity(false);
            }

            if (cancelClientMission)
            {
                CancelInteractionRpc(
                    previousOccupant,
                    batteryDropped: false);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmCompletionRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                _isLocallyCompleted = true;
                _station.ApplyAuthoritativeCompletion();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmRecoveryCompletionRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                // 공용 미션 완료는 개인 완료 상태를 덮어쓰지 않는다.
                // 같은 스테이션에 중복 복구 건이 남아 있어도 다시 조작할 수 있다.
                _station.ApplyAuthoritativeRecoveryCompletion(
                    _isLocallyCompleted);
            }
        }

        public bool ServerHasCompleted(ulong clientId)
        {
            return IsServer &&
                   _completedClientIds.Contains(clientId);
        }

        /// <summary>재접속 전 완료한 개인 미션 판정을 새 clientId로 옮긴다.</summary>
        public bool ServerRebindPlayer(
            ulong previousClientId,
            ulong currentClientId)
        {
            if (!IsServer || previousClientId == currentClientId ||
                !_completedClientIds.Remove(previousClientId))
            {
                return false;
            }

            _completedClientIds.Add(currentClientId);
            _lastProcessedSequences.Remove(previousClientId);
            return true;
        }

        private void ServerHandleMissionFailure(
            ulong senderClientId,
            MissionInputAction action)
        {
            ServerEmitMissionFailureNoise(senderClientId, action);

            ServerReleaseOccupancy(cancelClientMission: false);
        }

        private void ServerInterruptBatteryCarry(ulong senderClientId)
        {
            ServerEmitMissionFailureNoise(
                senderClientId,
                MissionInputAction.BatteryDrop);
            ServerReleaseOccupancy(cancelClientMission: false);
            CancelInteractionRpc(
                senderClientId,
                batteryDropped: true);
        }

        private void ServerEmitMissionFailureNoise(
            ulong senderClientId,
            MissionInputAction action)
        {
            if (senderClientId ==
                Unity.Netcode.NetworkManager.ServerClientId)
            {
                return;
            }

            var emitter = GetComponent<FuseFailureNoiseEmitter>();
            if (action == MissionInputAction.BatteryDrop &&
                NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) &&
                client.PlayerObject != null)
            {
                emitter?.EmitFailureNoise(
                    client.PlayerObject.transform.position);
                return;
            }

            emitter?.EmitFailureNoise();
        }

        private void ServerAcceptMissionCompletion(ulong senderClientId)
        {
            var roundState = NetworkRoundState.Current;
            var isVillain =
                NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var completingClient) &&
                completingClient.PlayerObject != null &&
                completingClient.PlayerObject.TryGetComponent<
                    NetworkPlayerAvatar>(out var completingAvatar) &&
                completingAvatar.Role == PlayerRole.Villain;
            var isRecoveryMission = _occupantIsRecoveryMission;
            var accepted = isRecoveryMission
                ? roundState != null &&
                  roundState.ServerTryCompleteRecoveryMission(
                      senderClientId,
                      NetworkObjectId,
                      out _)
                : !_completedClientIds.Contains(senderClientId) &&
                  (roundState == null ||
                   (isVillain
                       ? roundState.ServerTryCompleteFakeMission(
                           senderClientId,
                           NetworkObjectId)
                       : roundState.ServerTryCompleteMission(
                           senderClientId,
                           NetworkObjectId,
                           out _)));
            if (accepted)
            {
                if (isRecoveryMission)
                {
                    ConfirmRecoveryCompletionRpc(senderClientId);
                }
                else
                {
                    _completedClientIds.Add(senderClientId);
                    ConfirmCompletionRpc(senderClientId);
                }

                PublishCompletionVisualRpc();
            }

            ServerReleaseOccupancy(cancelClientMission: false);
        }

        private void HandleOccupantChanged(
            ulong previousValue,
            ulong currentValue)
        {
            PublicVisualStateChanged?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishCompletionVisualRpc()
        {
            PublicMissionCompleted?.Invoke();
        }
    }
}
