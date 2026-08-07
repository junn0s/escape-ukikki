using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 시약병 분류 미션의 서버 권위 판정이다(GDD §10.2).
    /// 시약병을 목표 칸으로 드래그할 때마다 서버가 배치를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ReagentSortingStation))]
    public sealed class NetworkReagentSortingAuthority : NetworkBehaviour
    {
        [SerializeField] private ReagentSortingStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<int> _sortedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            ReagentSortingStation station,
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
                    "[Mission] Reagent sorting authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestSort);
            _sortedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _sortedMask.OnValueChanged -= HandleMaskChanged;
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

        private void RequestSort(
            GameObject interactor,
            int reagentIndex,
            int binIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestSortRpc(reagentIndex, binIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestSortRpc(
            int reagentIndex,
            int binIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                reagentIndex < 0 ||
                reagentIndex >= _station.Rules.ReagentCount)
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

            var bit = 1 << reagentIndex;
            if ((_sortedMask.Value & bit) != 0 ||
                _station.Rules.GetTargetBinIndex(reagentIndex) != binIndex)
            {
                return;
            }

            _sortedMask.Value |= bit;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            var flags = new bool[_station.Rules.ReagentCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_sortedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
