using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 실험용 쥐 케이지 잠그기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 자물쇠를 클릭할 때마다 서버가 배치를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(RatCageLockStation))]
    public sealed class NetworkRatCageLockAuthority : NetworkBehaviour
    {
        [SerializeField] private RatCageLockStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<int> _lockedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            RatCageLockStation station,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_station == null || _interactionConfig == null)
            {
                Debug.LogError(
                    "[Mission] Rat cage lock authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestLock);
            _lockedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _lockedMask.OnValueChanged -= HandleMaskChanged;
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

            return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private void RequestLock(GameObject interactor, int lockIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestLockRpc(lockIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestLockRpc(
            int lockIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                lockIndex < 0 || lockIndex >= _station.Rules.ItemCount)
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
            var squaredDistance = (
                (Vector2)playerObject.transform.position -
                (Vector2)_station.transform.position).sqrMagnitude;
            var range = _interactionConfig.GeneralInteractionRangeMeters;
            if (squaredDistance > range * range)
            {
                return;
            }

            var bit = 1 << lockIndex;
            if ((_lockedMask.Value & bit) != 0)
            {
                return;
            }

            _lockedMask.Value |= bit;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            var flags = new bool[_station.Rules.ItemCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_lockedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
