using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 차단기 올리기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 스위치를 클릭할 때마다 서버가 배치를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CircuitBreakerStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkCircuitBreakerAuthority : NetworkBehaviour
    {
        [SerializeField] private CircuitBreakerStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<int> _flippedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            CircuitBreakerStation station,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority =
                GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _interactionConfig == null ||
                _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] Circuit breaker authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestFlip);
            _flippedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _flippedMask.OnValueChanged -= HandleMaskChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestFlip(GameObject interactor, int switchIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestFlipRpc(switchIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestFlipRpc(
            int switchIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                switchIndex < 0 || switchIndex >= _station.Rules.ItemCount)
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

            var bit = 1 << switchIndex;
            if ((_flippedMask.Value & bit) != 0)
            {
                return;
            }

            var nextMask = _flippedMask.Value | bit;
            var completedMask = (1 << _station.Rules.ItemCount) - 1;
            if (nextMask == completedMask &&
                !_missionAuthority.ServerTryComplete(senderClientId))
            {
                return;
            }

            _flippedMask.Value = nextMask;
        }

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null)
            {
                return;
            }

            var flags = new bool[_station.Rules.ItemCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_flippedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
