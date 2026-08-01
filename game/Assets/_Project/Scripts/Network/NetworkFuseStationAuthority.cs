using System.Collections.Generic;
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

            var range =
                _config.GeneralInteractionRangeMeters +
                DistanceReleaseToleranceMeters;
            var playerPosition =
                (Vector2)client.PlayerObject.transform.position;
            var stationPosition = (Vector2)_station.transform.position;
            if ((playerPosition - stationPosition).sqrMagnitude >
                range * range)
            {
                ServerReleaseOccupancy(cancelClientMission: true);
                return;
            }

            if (Time.unscaledTimeAsDouble - _lastServerActivityTime >=
                _config.ExclusiveOccupancyTimeoutSeconds)
            {
                ServerReleaseOccupancy(cancelClientMission: true);
            }
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || IsCompleted || _station == null ||
                interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner ||
                !interactor.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                !avatar.HasAssignedRole ||
                !interactor.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                !journal.IsAssigned(NetworkObjectId))
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null &&
                !roundState.AllowsMissionInteraction)
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
            var roundState = NetworkRoundState.Current;
            var canUseMission =
                !_completedClientIds.Contains(senderClientId) &&
                (roundState == null ||
                 roundState.AllowsMissionInteraction) &&
                (roundState == null ||
                 (avatar != null && avatar.HasAssignedRole));
            var rejectionReason = NetworkInteractionRules.Validate(
                isOwnedBySender,
                clientSequence,
                lastSequence,
                _station.isActiveAndEnabled && canUseMission,
                IsOccupied && OccupantClientId != senderClientId,
                (playerPosition - stationPosition).sqrMagnitude,
                _config.GeneralInteractionRangeMeters,
                playerObject != null &&
                HasUnblockedPath(playerObject.transform));

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
            playerObject
                .GetComponent<NetworkPlayerMissionJournal>()?
                .ServerSetMissionActivity(true);
            _lastServerActivityTime = Time.unscaledTimeAsDouble;
            ApproveInteractionRpc(senderClientId, clientSequence);
        }

        [Rpc(SendTo.Server)]
        private void RefreshOccupancyRpc(
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsOccupied || OccupantClientId != senderClientId ||
                !IsNewSequence(senderClientId, clientSequence))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;
            _lastServerActivityTime = Time.unscaledTimeAsDouble;
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
            if (completed)
            {
                var roundState = NetworkRoundState.Current;
                var isVillain =
                    NetworkManager.ConnectedClients.TryGetValue(
                        senderClientId,
                        out var completingClient) &&
                    completingClient.PlayerObject != null &&
                    completingClient.PlayerObject
                        .TryGetComponent<NetworkPlayerAvatar>(
                            out var completingAvatar) &&
                    completingAvatar.Role == PlayerRole.Villain;
                // 빌런은 위장 경로로 보내 진행률을 올리지 않는다(GDD §9.1).
                var accepted =
                    !_completedClientIds.Contains(senderClientId) &&
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
                    _completedClientIds.Add(senderClientId);
                    PublishCompletionVisualRpc();
                    ConfirmCompletionRpc(senderClientId);
                }
            }
            else if (failed &&
                     senderClientId !=
                     NetworkManager.ServerClientId)
            {
                GetComponent<FuseFailureNoiseEmitter>()?
                    .EmitFailureNoise();
            }

            ServerReleaseOccupancy(cancelClientMission: false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ApproveInteractionRpc(
            ulong targetClientId,
            uint approvedSequence)
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
                _station.BeginApprovedInteraction(localPlayer.gameObject);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CancelInteractionRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId &&
                _station != null &&
                _station.IsMissionActive)
            {
                _station.CancelMission();
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

        private bool HasUnblockedPath(Transform player)
        {
            var playerPosition = (Vector2)player.position;
            var stationPosition = (Vector2)_station.transform.position;
            var hitCount = Physics2D.LinecastNonAlloc(
                playerPosition,
                stationPosition,
                _pathHits);
            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = _pathHits[index].collider;
                if (hitCollider == null || hitCollider.isTrigger ||
                    hitCollider.transform == player ||
                    hitCollider.transform.IsChildOf(player) ||
                    hitCollider.transform == _station.transform ||
                    hitCollider.transform.IsChildOf(_station.transform))
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
            if (IsLocalOccupant())
            {
                RefreshOccupancyRpc(NextLocalSequence());
            }
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
            ReleaseLocalOccupancy(
                completed: false,
                failed: true);
        }

        private void HandleMissionCompleted(FuseStationPrototype station)
        {
            ReleaseLocalOccupancy(
                completed: true,
                failed: false);
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
                CancelInteractionRpc(previousOccupant);
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

        public bool ServerHasCompleted(ulong clientId)
        {
            return IsServer &&
                   _completedClientIds.Contains(clientId);
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
