using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 오염된 주사기 폐기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 주사기 하나를 드래그해 놓을 때마다 서버가 배치를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ContaminatedSyringeStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkContaminatedSyringeAuthority : NetworkBehaviour
    {
        private const int MaxItemCount = 8;

        [SerializeField] private ContaminatedSyringeStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<int> _placedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            ContaminatedSyringeStation station,
            SurvivorMissionBalanceConfig config,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _config = config;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority =
                GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _config == null ||
                _interactionConfig == null || _missionAuthority == null ||
                _config.ContaminatedSyringeCount > MaxItemCount)
            {
                Debug.LogError(
                    "[Mission] Contaminated syringe authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestPlaceItem);
            _placedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _placedMask.OnValueChanged -= HandleMaskChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestPlaceItem(GameObject interactor, int itemIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestPlaceItemRpc(itemIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestPlaceItemRpc(
            int itemIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                itemIndex < 0 || itemIndex >= _config.ContaminatedSyringeCount)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (_missionAuthority == null ||
                !_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

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

            var bit = 1 << itemIndex;
            if ((_placedMask.Value & bit) != 0)
            {
                return;
            }

            var nextMask = _placedMask.Value | bit;
            var completedMask =
                (1 << _config.ContaminatedSyringeCount) - 1;
            if (nextMask == completedMask &&
                !_missionAuthority.ServerTryComplete(senderClientId))
            {
                return;
            }

            _placedMask.Value = nextMask;
        }

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null || _config == null)
            {
                return;
            }

            var flags = new bool[_config.ContaminatedSyringeCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_placedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
