using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 지정 보관 칸의 서버 권위 판정이다.
    /// 보관 수량은 접근한 플레이어에게만 알려준다(docs/system-design-document.md §12.3).
    /// 전원에게 복제하면 누가 해독제를 숨겼는지가 그대로 드러나 선점 전략이 무의미해진다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AntidoteStorageLocker))]
    public sealed class NetworkStorageLockerAuthority : NetworkBehaviour
    {
        private static readonly List<NetworkStorageLockerAuthority> Instances =
            new();

        [SerializeField] private AntidoteStorageLocker _locker;
        [SerializeField] private AntidoteBalanceConfig _antidoteConfig;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private int _serverStoredCount;

        public AntidoteStorageLocker Locker => _locker;
        public int ServerStoredCount => _serverStoredCount;
        public int SlotCapacity =>
            _antidoteConfig != null ? _antidoteConfig.StorageLockerSlotCount : 0;

        public void Configure(
            AntidoteStorageLocker locker,
            AntidoteBalanceConfig antidoteConfig,
            InteractionBalanceConfig interactionConfig)
        {
            _locker = locker;
            _antidoteConfig = antidoteConfig;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_locker == null || _antidoteConfig == null ||
                _interactionConfig == null)
            {
                Debug.LogError(
                    "[Antidote] Storage locker authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            Instances.Add(this);
            _locker.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestInteraction);
        }

        public override void OnNetworkDespawn()
        {
            Instances.Remove(this);
            _locker?.ClearInteractionAuthority(this);
        }

        /// <summary>
        /// 감염 사망자의 소지 해독제를 가장 가까운 보관 칸에 넣는다(SDD §13.3의 2단계).
        /// 바닥 자유 드롭이 없으므로 보관 칸이 유일한 지정 드롭 지점이다.
        /// </summary>
        public static bool ServerDepositAtNearestLocker(Vector2 worldPosition)
        {
            NetworkStorageLockerAuthority nearest = null;
            var nearestSquaredDistance = float.MaxValue;
            foreach (var instance in Instances)
            {
                if (instance == null || !instance.IsServer ||
                    instance._serverStoredCount >= instance.SlotCapacity)
                {
                    continue;
                }

                var squaredDistance = (
                    worldPosition -
                    (Vector2)instance.transform.position).sqrMagnitude;
                if (squaredDistance >= nearestSquaredDistance)
                {
                    continue;
                }

                nearest = instance;
                nearestSquaredDistance = squaredDistance;
            }

            if (nearest == null)
            {
                return false;
            }

            nearest._serverStoredCount++;
            return true;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner)
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                return false;
            }

            // 유령은 보관함을 조작할 수 없다(GDD §17).
            return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private void RequestInteraction(GameObject interactor)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestLockerActionRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestLockerActionRpc(RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _locker == null)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var playerObject = client.PlayerObject;
            var inventory = playerObject
                .GetComponent<NetworkAntidoteInventoryAuthority>();
            if (inventory == null)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            var allowsInteraction =
                roundState == null || roundState.AllowsMissionInteraction;
            var lifeState =
                playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection)
                    ? infection.LifeState
                    : PlayerLifeState.AliveHealthy;
            var range = _interactionConfig.ItemPickupRangeMeters;
            var isWithinRange = (
                (Vector2)playerObject.transform.position -
                (Vector2)_locker.transform.position).sqrMagnitude <=
                range * range;

            // 소지 중이면 보관, 아니면 인출이다. 소지 한도가 1개라 둘이 겹치지 않는다.
            var rejection = inventory.CarriedCount > 0
                ? ServerTryStore(
                    inventory,
                    lifeState,
                    allowsInteraction,
                    isWithinRange)
                : ServerTryWithdraw(
                    inventory,
                    lifeState,
                    allowsInteraction,
                    isWithinRange);

            PublishLockerStateRpc(
                senderClientId,
                _serverStoredCount,
                rejection);
        }

        private AntidoteRejectionReason ServerTryStore(
            NetworkAntidoteInventoryAuthority inventory,
            PlayerLifeState lifeState,
            bool allowsInteraction,
            bool isWithinRange)
        {
            var rejection = AntidoteCraftRules.ValidateStore(
                lifeState,
                inventory.CarriedCount,
                _serverStoredCount,
                SlotCapacity,
                allowsInteraction,
                isWithinRange);
            if (rejection != AntidoteRejectionReason.None)
            {
                return rejection;
            }

            if (!inventory.ServerTryConsumeAntidote())
            {
                return AntidoteRejectionReason.NotCarrying;
            }

            _serverStoredCount++;
            return AntidoteRejectionReason.None;
        }

        private AntidoteRejectionReason ServerTryWithdraw(
            NetworkAntidoteInventoryAuthority inventory,
            PlayerLifeState lifeState,
            bool allowsInteraction,
            bool isWithinRange)
        {
            var rejection = AntidoteCraftRules.ValidateWithdraw(
                lifeState,
                inventory.CarriedCount,
                inventory.MaxCarryCount,
                _serverStoredCount,
                allowsInteraction,
                isWithinRange);
            if (rejection != AntidoteRejectionReason.None)
            {
                return rejection;
            }

            if (!inventory.ServerTryAddAntidote())
            {
                return AntidoteRejectionReason.CarryLimitReached;
            }

            _serverStoredCount--;
            return AntidoteRejectionReason.None;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishLockerStateRpc(
            ulong targetClientId,
            int storedCount,
            AntidoteRejectionReason rejectionReason)
        {
            if (NetworkManager == null ||
                NetworkManager.LocalClientId != targetClientId ||
                _locker == null)
            {
                return;
            }

            _locker.ApplyAuthoritativeStoredCount(storedCount);
            if (rejectionReason != AntidoteRejectionReason.None)
            {
                _locker.ApplyInteractionFeedback(rejectionReason);
                Debug.LogWarning(
                    $"[Antidote] Locker request rejected: {rejectionReason}.",
                    this);
            }
            else
            {
                _locker.ClearInteractionFeedback();
            }
        }
    }
}
