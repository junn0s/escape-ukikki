using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 강화 스테이션의 서버 권위 상호작용이다. 빌런만 사용할 수 있고,
    /// 서버가 같은 시드의 퍼즐에 개별 조작을 재현해 완료를 판정한다.
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

        private VillainUpgradeMissionSession _serverMission;
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
            _station.ChallengeInputSubmitted +=
                HandleChallengeInputSubmitted;
            _station.ChannelCancelled += HandleChannelCancelled;
            _occupantClientId.OnValueChanged += HandleOccupantChanged;
            _station.SetPublicActivity(IsOccupied);
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
                _station.ChallengeInputSubmitted -=
                    HandleChallengeInputSubmitted;
                _station.ChannelCancelled -= HandleChannelCancelled;
                _station.SetPublicActivity(false);
            }

            _occupantClientId.OnValueChanged -= HandleOccupantChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            _serverMission = null;
            _lastProcessedSequences.Clear();
        }

        private void Update()
        {
            if (!IsServer || !IsOccupied || NetworkManager == null)
            {
                return;
            }

            if (!TryGetValidOccupant(out var playerObject))
            {
                ServerReleaseOccupancy(cancelClientChallenge: true);
                return;
            }

            var range =
                _config.GeneralInteractionRangeMeters +
                DistanceReleaseToleranceMeters;
            var playerPosition =
                (Vector2)playerObject.transform.position;
            var stationPosition = (Vector2)_station.transform.position;
            if ((playerPosition - stationPosition).sqrMagnitude >
                range * range)
            {
                ServerReleaseOccupancy(cancelClientChallenge: true);
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
                avatar.Role != PlayerRole.Villain ||
                IsOccupied)
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            return roundState == null || roundState.AllowsVillainToolUse;
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
                IsOccupied,
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
                roundState == null || roundState.AllowsVillainToolUse,
                isOccupiedByOtherPlayer: false);
            if (upgradeRejection != UpgradeRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, upgradeRejection);
                return;
            }

            var challengeSeed = Random.Range(1, int.MaxValue);
            var challengeStartedAt = NetworkManager.ServerTime.Time;
            _serverMission = _station.CreateServerMission(
                challengeSeed,
                challengeStartedAt);
            playerObject
                .GetComponent<NetworkPlayerMissionJournal>()?
                .ServerSetMissionActivity(true);
            _occupantClientId.Value = senderClientId;
            ApproveUpgradeRpc(
                senderClientId,
                clientSequence,
                challengeSeed,
                challengeStartedAt);
        }

        [Rpc(SendTo.Server)]
        private void SubmitUpgradeInputRpc(
            VillainUpgradeInputAction action,
            int primaryValue,
            int secondaryValue,
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (_serverMission == null || !IsOccupied ||
                OccupantClientId != senderClientId ||
                !IsNewSequence(senderClientId, clientSequence) ||
                !TryGetValidOccupant(out var playerObject) ||
                !IsOccupantWithinReleaseRange(playerObject))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;
            var result = _serverMission.Validate(
                new VillainUpgradeMissionInputCommand(
                    action,
                    primaryValue,
                    secondaryValue),
                NetworkManager.ServerTime.Time);
            switch (result)
            {
                case FuseMissionInputResult.Ignored:
                    RejectChallengeRpc(senderClientId);
                    ServerReleaseOccupancy(
                        cancelClientChallenge: false);
                    break;
                case FuseMissionInputResult.Accepted
                    when action ==
                         VillainUpgradeInputAction.ToxicityDoseInjected:
                    ConfirmToxicityProgressRpc(
                        senderClientId,
                        _serverMission.ToxicityProgressIndex,
                        _serverMission.ToxicityStepStartedAt);
                    break;
                case FuseMissionInputResult.Failed:
                    RejectChallengeRpc(senderClientId);
                    ServerReleaseOccupancy(
                        cancelClientChallenge: false);
                    break;
                case FuseMissionInputResult.Completed:
                    ServerAcceptChallengeCompletion(senderClientId);
                    break;
            }
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
            ServerReleaseOccupancy(cancelClientChallenge: false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ApproveUpgradeRpc(
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
            if (localPlayer == null)
            {
                return;
            }

            var elapsedServerSeconds = System.Math.Max(
                0d,
                NetworkManager.ServerTime.Time - challengeStartedAt);
            _station.BeginApprovedInteraction(
                localPlayer.gameObject,
                challengeSeed,
                Time.unscaledTimeAsDouble - elapsedServerSeconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmUpgradeRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                _station.ApplyAuthoritativeCompletion();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RejectChallengeRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                _station.ApplyAuthoritativeFailure();
                Debug.LogWarning(
                    "[Upgrade] Challenge input failed; progress reset.",
                    this);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmToxicityProgressRpc(
            ulong targetClientId,
            int progressIndex,
            double serverStepStartedAt)
        {
            if (NetworkManager == null ||
                NetworkManager.LocalClientId != targetClientId)
            {
                return;
            }

            var elapsedServerSeconds = System.Math.Max(
                0d,
                NetworkManager.ServerTime.Time - serverStepStartedAt);
            _station.ApplyAuthoritativeToxicityProgress(
                progressIndex,
                Time.unscaledTimeAsDouble - elapsedServerSeconds);
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
        private void CancelChallengeRpc(ulong targetClientId)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId &&
                _station != null &&
                _station.IsChanneling)
            {
                _station.ApplyAuthoritativeFailure();
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

        private void ServerAcceptChallengeCompletion(ulong senderClientId)
        {
            var upgradeAuthority = NetworkVillainUpgradeAuthority.Current;
            var rejectionReason =
                UpgradeRejectionReason.RoundPhaseBlocked;
            if (upgradeAuthority != null &&
                upgradeAuthority.ServerTryApplyUpgrade(
                    senderClientId,
                    _station.Axis,
                    _station.RoomId,
                    out var newLevel,
                    out rejectionReason))
            {
                ConfirmUpgradeRpc(senderClientId);
                if (newLevel >= VillainUpgradeState.MaximumLevel)
                {
                    PublishAxisMaxedRpc(senderClientId);
                }
            }
            else
            {
                PublishRejectionRpc(
                    senderClientId,
                    rejectionReason);
                RejectChallengeRpc(senderClientId);
            }

            ServerReleaseOccupancy(cancelClientChallenge: false);
        }

        private bool TryGetValidOccupant(out NetworkObject playerObject)
        {
            playerObject = null;
            if (NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    OccupantClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject;
            var avatar = playerObject.GetComponent<NetworkPlayerAvatar>();
            var infection =
                playerObject.GetComponent<NetworkInfectionAuthority>();
            var roundState = NetworkRoundState.Current;
            return avatar != null &&
                   avatar.Role == PlayerRole.Villain &&
                   (infection == null ||
                    infection.LifeState != PlayerLifeState.DeadGhost) &&
                   (roundState == null ||
                    roundState.AllowsVillainToolUse);
        }

        private bool IsOccupantWithinReleaseRange(
            NetworkObject playerObject)
        {
            if (playerObject == null || _config == null ||
                _station == null)
            {
                return false;
            }

            var range =
                _config.GeneralInteractionRangeMeters +
                DistanceReleaseToleranceMeters;
            return Vector2.SqrMagnitude(
                       (Vector2)playerObject.transform.position -
                       (Vector2)_station.transform.position) <=
                   range * range;
        }

        private void HandleChallengeInputSubmitted(
            UpgradeStationPrototype station,
            VillainUpgradeMissionInputCommand command)
        {
            if (!IsLocalOccupant())
            {
                return;
            }

            SubmitUpgradeInputRpc(
                command.Action,
                command.PrimaryValue,
                command.SecondaryValue,
                NextLocalSequence());
        }

        private void HandleChannelCancelled(
            UpgradeStationPrototype station)
        {
            if (IsLocalOccupant())
            {
                CancelUpgradeRpc(NextLocalSequence());
            }
        }

        private void HandleOccupantChanged(
            ulong previousValue,
            ulong currentValue)
        {
            _station?.SetPublicActivity(
                currentValue != NoOccupantClientId);
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
                ServerReleaseOccupancy(cancelClientChallenge: false);
            }
        }

        private void ServerReleaseOccupancy(bool cancelClientChallenge)
        {
            if (!IsServer || !IsOccupied)
            {
                return;
            }

            var previousOccupant = OccupantClientId;
            _occupantClientId.Value = NoOccupantClientId;
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
            if (cancelClientChallenge)
            {
                CancelChallengeRpc(previousOccupant);
            }
        }
    }
}
