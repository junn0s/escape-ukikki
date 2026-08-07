using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 공기 필터 교체 미션의 서버 권위 판정이다(GDD §10.2).
    /// 낡은 필터를 먼저 빼야만 새 필터를 꽂을 수 있다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SwapFilterStation))]
    public sealed class NetworkSwapFilterAuthority : NetworkBehaviour
    {
        [SerializeField] private SwapFilterStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<bool> _isOldFilterRemoved = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isNewFilterInstalled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            SwapFilterStation station,
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
                    "[Mission] Swap filter authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestSwap);
            _isOldFilterRemoved.OnValueChanged += HandleStateChanged;
            _isNewFilterInstalled.OnValueChanged += HandleStateChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _isOldFilterRemoved.OnValueChanged -= HandleStateChanged;
            _isNewFilterInstalled.OnValueChanged -= HandleStateChanged;
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
                if (!_isOldFilterRemoved.Value || _isNewFilterInstalled.Value)
                {
                    return;
                }

                _isNewFilterInstalled.Value = true;
            }
            else
            {
                if (_isOldFilterRemoved.Value)
                {
                    return;
                }

                _isOldFilterRemoved.Value = true;
            }
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _isOldFilterRemoved.Value,
                _isNewFilterInstalled.Value);
        }

        private void HandleStateChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
