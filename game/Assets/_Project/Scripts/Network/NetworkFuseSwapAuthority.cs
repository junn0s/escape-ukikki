using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 퓨즈 교체 미션의 서버 권위 판정이다(GDD §10.2).
    /// 탄 퓨즈를 먼저 뽑아야만 새 퓨즈를 꽂을 수 있다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FuseSwapStation))]
    public sealed class NetworkFuseSwapAuthority : NetworkBehaviour
    {
        [SerializeField] private FuseSwapStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<bool> _isOldFuseRemoved = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isNewFuseInstalled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            FuseSwapStation station,
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
                    "[Mission] Fuse swap authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestSwap);
            _isOldFuseRemoved.OnValueChanged += HandleStateChanged;
            _isNewFuseInstalled.OnValueChanged += HandleStateChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _isOldFuseRemoved.OnValueChanged -= HandleStateChanged;
            _isNewFuseInstalled.OnValueChanged -= HandleStateChanged;
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

        private void RequestSwap(GameObject interactor, bool isInstallingNew)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestSwapRpc(isInstallingNew);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestSwapRpc(
            bool isInstallingNew,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null)
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

            if (isInstallingNew)
            {
                if (!_isOldFuseRemoved.Value || _isNewFuseInstalled.Value)
                {
                    return;
                }

                _isNewFuseInstalled.Value = true;
            }
            else
            {
                if (_isOldFuseRemoved.Value)
                {
                    return;
                }

                _isOldFuseRemoved.Value = true;
            }
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _isOldFuseRemoved.Value,
                _isNewFuseInstalled.Value);
        }

        private void HandleStateChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
