using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 현미경 렌즈 초점 미션의 서버 권위 판정이다(GDD §10.2).
    /// 슬라이더 델타를 누적하고, 확정 요청 시점의 위치로 목표 구간 여부를
    /// 판정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(MicroscopeFocusStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkMicroscopeFocusAuthority : NetworkBehaviour
    {
        [SerializeField] private MicroscopeFocusStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<float> _positionNormalized = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            MicroscopeFocusStation station,
            SurvivorMissionBalanceConfig config,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _config = config;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority = GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _config == null ||
                _interactionConfig == null || _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] Microscope focus authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestPush,
                RequestConfirm);
            _positionNormalized.OnValueChanged += HandlePositionChanged;
            _isCompleted.OnValueChanged += HandleCompletedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _positionNormalized.OnValueChanged -= HandlePositionChanged;
            _isCompleted.OnValueChanged -= HandleCompletedChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestPush(GameObject interactor, float deltaNormalized)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestPushRpc(deltaNormalized);
            }
        }

        private void RequestConfirm(GameObject interactor)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestConfirmRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestPushRpc(
            float deltaNormalized,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isCompleted.Value || deltaNormalized <= 0f)
            {
                return;
            }

            if (!IsRequestWithinRange(rpcParams))
            {
                return;
            }

            _station.Rules.Push(deltaNormalized);
            _positionNormalized.Value = _station.Rules.PositionNormalized;
        }

        [Rpc(SendTo.Server)]
        private void RequestConfirmRpc(RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isCompleted.Value)
            {
                return;
            }

            if (!IsRequestWithinRange(rpcParams))
            {
                return;
            }

            if (_station.Rules.TryConfirm())
            {
                var senderClientId = rpcParams.Receive.SenderClientId;
                if (_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _isCompleted.Value = true;
                }
            }
        }

        private bool IsRequestWithinRange(RpcParams rpcParams)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            return _missionAuthority.ServerCanProcess(senderClientId);
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _positionNormalized.Value,
                _isCompleted.Value);
        }

        private void HandlePositionChanged(float previous, float current)
        {
            ApplyReplicatedState();
        }

        private void HandleCompletedChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
