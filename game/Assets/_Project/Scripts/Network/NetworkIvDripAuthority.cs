using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 수액 속도 조절 미션의 서버 권위 판정이다(GDD §10.2).
    /// 서버 시계로 왕복 위치를 계산하고, 정지 요청 시각을 기준으로 판정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(IvDripStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkIvDripAuthority : NetworkBehaviour
    {
        [SerializeField] private IvDripStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _serverElapsedSeconds;

        public void Configure(
            IvDripStation station,
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
                    "[Mission] IV drip authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestStop);
            _isCompleted.OnValueChanged += HandleCompletedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _isCompleted.OnValueChanged -= HandleCompletedChanged;
        }

        private void Update()
        {
            if (!IsServer || _isCompleted.Value)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                return;
            }

            _serverElapsedSeconds += Time.deltaTime;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestStop(GameObject interactor, float clientElapsed)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestStopRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestStopRpc(RpcParams rpcParams = default)
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

            if (_station.Rules.TryStop(_serverElapsedSeconds))
            {
                if (_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _isCompleted.Value = true;
                }
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

            _station.ApplyAuthoritativeState(_isCompleted.Value);
        }

        private void HandleCompletedChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
