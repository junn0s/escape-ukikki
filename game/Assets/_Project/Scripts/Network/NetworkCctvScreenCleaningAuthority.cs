using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// CCTV 화면 닦기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 문지를 때마다 서버가 누적 진행률을 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CctvScreenCleaningStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkCctvScreenCleaningAuthority : NetworkBehaviour
    {
        [SerializeField] private CctvScreenCleaningStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<int> _scrubCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            CctvScreenCleaningStation station,
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
                    "[Mission] CCTV screen cleaning authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestScrub);
            _scrubCount.OnValueChanged += HandleCountChanged;
            _isCompleted.OnValueChanged += HandleCompletedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _scrubCount.OnValueChanged -= HandleCountChanged;
            _isCompleted.OnValueChanged -= HandleCompletedChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestScrub(GameObject interactor)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestScrubRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestScrubRpc(RpcParams rpcParams = default)
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

            var isCompleted = _station.Rules.TryScrub();
            _scrubCount.Value = _station.Rules.ScrubCount;
            if (isCompleted &&
                _missionAuthority.ServerTryComplete(senderClientId))
            {
                _isCompleted.Value = true;
            }
        }

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _scrubCount.Value,
                _isCompleted.Value);
        }

        private void HandleCountChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }

        private void HandleCompletedChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
