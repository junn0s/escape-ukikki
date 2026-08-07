using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// ID 카드 긁기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 클라이언트가 보낸 드래그 소요 시간을 서버가 그대로 판정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(IdCardSwipeStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkIdCardSwipeAuthority : NetworkBehaviour
    {
        [SerializeField] private IdCardSwipeStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _failedAttemptCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            IdCardSwipeStation station,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority = GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _interactionConfig == null ||
                _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] ID card swipe authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestSwipe);
            _isCompleted.OnValueChanged += HandleStateChanged;
            _failedAttemptCount.OnValueChanged += HandleAttemptChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _isCompleted.OnValueChanged -= HandleStateChanged;
            _failedAttemptCount.OnValueChanged -= HandleAttemptChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestSwipe(GameObject interactor, float durationSeconds)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestSwipeRpc(durationSeconds);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestSwipeRpc(
            float durationSeconds,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isCompleted.Value)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

            if (_station.Rules.TrySwipe(durationSeconds))
            {
                if (_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _isCompleted.Value = true;
                }
            }
            else
            {
                _failedAttemptCount.Value = _station.Rules.FailedAttemptCount;
            }
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _isCompleted.Value,
                _failedAttemptCount.Value);
        }

        private void HandleStateChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }

        private void HandleAttemptChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
