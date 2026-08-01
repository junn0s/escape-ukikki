using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 강화 스테이션의 서버 권위 상호작용이다.
    /// 빌런만 사용할 수 있고, 거리와 순서 번호를 서버에서 다시 검증한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(UpgradeStationPrototype))]
    public sealed class NetworkUpgradeStationAuthority : NetworkBehaviour
    {
        public const ulong NoOccupantClientId = ulong.MaxValue;
        private const float DistanceReleaseToleranceMeters = 0.25f;

        [SerializeField] private UpgradeStationPrototype _station;
        [SerializeField] private InteractionBalanceConfig _config;

        private readonly NetworkVariable<ulong> _occupantClientId = new(
            NoOccupantClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<ulong, uint> _lastProcessedSequences =
            new();

        private uint _localSequence;

        public UpgradeStationPrototype Station => _station;
        public ulong OccupantClientId => _occupantClientId.Value;
        public bool IsOccupied => OccupantClientId != NoOccupantClientId;

        public void Configure(
            UpgradeStationPrototype station,
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
                    "[Upgrade] Station authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                CanLocalPlayerRequestInteraction,
                RequestInteraction);
            _station.ChannelCompleted += HandleChannelCompleted;
            _station.ChannelCancelled += HandleChannelCancelled;
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
                _station.ChannelCompleted -= HandleChannelCompleted;
                _station.ChannelCancelled -= HandleChannelCancelled;
            }

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            _lastProcessedSequences.Clear();
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
                ServerReleaseOccupancy(cancelClientChannel: false);
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
                ServerReleaseOccupancy(cancelClientChannel: true);
            }
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || _station == null || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner ||
                !interactor.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Villain)
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

            RequestUpgradeRpc(
                playerNetworkObject.NetworkObjectId,
                NextLocalSequence());
        }

        [Rpc(SendTo.Server)]
        private void RequestUpgradeRpc(
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
            var interactionRejection = NetworkInteractionRules.Validate(
                isOwnedBySender,
                clientSequence,
                lastSequence,
                _station.isActiveAndEnabled,
                IsOccupied && OccupantClientId != senderClientId,
                (playerPosition - stationPosition).sqrMagnitude,
                _config.GeneralInteractionRangeMeters,
                hasUnblockedPath: true);

            if (interactionRejection !=
                    InteractionRejectionReason.InvalidOwner &&
                interactionRejection !=
                    InteractionRejectionReason.StaleSequence)
            {
                _lastProcessedSequences[senderClientId] = clientSequence;
            }

            if (interactionRejection != InteractionRejectionReason.None)
            {
                PublishRejectionRpc(
                    senderClientId,
                    UpgradeRejectionReason.StationBusy);
                return;
            }

            var avatar = playerObject.GetComponent<NetworkPlayerAvatar>();
            var upgradeAuthority = NetworkVillainUpgradeAuthority.Current;
            var roundState = NetworkRoundState.Current;
            // 유령은 공용 패널을 조작할 수 없다(GDD §17).
            var infection =
                playerObject.GetComponent<NetworkInfectionAuthority>();
            if (infection != null &&
                infection.LifeState == PlayerLifeState.DeadGhost)
            {
                PublishRejectionRpc(
                    senderClientId,
                    UpgradeRejectionReason.NotVillain);
                return;
            }

            var upgradeRejection = VillainUpgradeRules.Validate(
                avatar != null ? avatar.Role : PlayerRole.Unassigned,
                upgradeAuthority == null ||
                upgradeAuthority.ServerCanUpgrade(_station.Axis),
                roundState == null || roundState.AllowsMissionInteraction,
                isOccupiedByOtherPlayer: false);
            if (upgradeRejection != UpgradeRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, upgradeRejection);
                return;
            }

            _occupantClientId.Value = senderClientId;
            ApproveUpgradeRpc(senderClientId, clientSequence);
        }

        [Rpc(SendTo.Server)]
        private void CompleteUpgradeRpc(
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

            var upgradeAuthority = NetworkVillainUpgradeAuthority.Current;
            if (upgradeAuthority != null)
            {
                if (upgradeAuthority.ServerTryApplyUpgrade(
                        senderClientId,
                        _station.Axis,
                        _station.RoomId,
                        out var newLevel,
                        out var rejectionReason))
                {
                    ConfirmUpgradeRpc(senderClientId, newLevel);
                    if (newLevel >= VillainUpgradeState.MaximumLevel)
                    {
                        PublishAxisMaxedRpc(senderClientId);
                    }
                }
                else
                {
                    PublishRejectionRpc(senderClientId, rejectionReason);
                }
            }

            ServerReleaseOccupancy(cancelClientChannel: false);
        }

        [Rpc(SendTo.Server)]
        private void CancelUpgradeRpc(
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
            ServerReleaseOccupancy(cancelClientChannel: false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ApproveUpgradeRpc(
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
        private void ConfirmUpgradeRpc(ulong targetClientId, int newLevel)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                _station.ApplyAuthoritativeCompletion();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishAxisMaxedRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                _station.ApplyAxisMaxed();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CancelChannelRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId &&
                _station != null &&
                _station.IsChanneling)
            {
                _station.CancelChannel();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            UpgradeRejectionReason rejectionReason)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                Debug.LogWarning(
                    $"[Upgrade] Request rejected: {rejectionReason}.",
                    this);
            }
        }

        private void HandleChannelCompleted(
            UpgradeStationPrototype station)
        {
            if (IsLocalOccupant())
            {
                CompleteUpgradeRpc(NextLocalSequence());
            }
        }

        private void HandleChannelCancelled(
            UpgradeStationPrototype station)
        {
            if (IsLocalOccupant())
            {
                CancelUpgradeRpc(NextLocalSequence());
            }
        }

        private bool IsLocalOccupant()
        {
            return IsSpawned &&
                   NetworkManager != null &&
                   IsOccupied &&
                   OccupantClientId == NetworkManager.LocalClientId;
        }

        private bool IsNewSequence(ulong clientId, uint clientSequence)
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

        private void HandleClientDisconnected(ulong clientId)
        {
            _lastProcessedSequences.Remove(clientId);
            if (IsOccupied && OccupantClientId == clientId)
            {
                ServerReleaseOccupancy(cancelClientChannel: false);
            }
        }

        private void ServerReleaseOccupancy(bool cancelClientChannel)
        {
            if (!IsServer || !IsOccupied)
            {
                return;
            }

            var previousOccupant = OccupantClientId;
            _occupantClientId.Value = NoOccupantClientId;
            if (cancelClientChannel)
            {
                CancelChannelRpc(previousOccupant);
            }
        }
    }
}
