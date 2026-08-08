using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 방호복 소독 미션의 서버 권위 판정이다(GDD §10.2).
    /// 시작하면 서버가 6초를 진행하며 중단 없이 완료까지 이어간다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(HazmatDecontaminationStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkHazmatDecontaminationAuthority :
        NetworkBehaviour
    {
        [SerializeField] private HazmatDecontaminationStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<float> _elapsedSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isRunning = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            HazmatDecontaminationStation station,
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
                    "[Mission] Hazmat decontamination authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestStart);
            _elapsedSeconds.OnValueChanged += HandleReplicatedChanged;
            _isRunning.OnValueChanged += HandleReplicatedChanged;
            _isCompleted.OnValueChanged += HandleReplicatedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _elapsedSeconds.OnValueChanged -= HandleReplicatedChanged;
            _isRunning.OnValueChanged -= HandleReplicatedChanged;
            _isCompleted.OnValueChanged -= HandleReplicatedChanged;
        }

        private void Update()
        {
            if (!IsServer || _station == null || !_isRunning.Value ||
                _isCompleted.Value)
            {
                return;
            }

            // 시작하면 회의 여부와 무관하게 6초까지 그대로 진행한다.
            // 시야 차단 연출이 도중에 멈추면 방금 왜 멈췄는지 혼란만 준다.
            var next = _elapsedSeconds.Value + Time.deltaTime;
            var required = _station.RequiredSeconds;
            if (next >= required)
            {
                _isRunning.Value = false;
                if (_missionAuthority.ServerTryCompleteLastInteractor())
                {
                    _elapsedSeconds.Value = required;
                    _isCompleted.Value = true;
                }
                else
                {
                    _elapsedSeconds.Value = 0f;
                }

                return;
            }

            _elapsedSeconds.Value = next;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestStart(GameObject interactor)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestStartRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestStartRpc(RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isRunning.Value || _isCompleted.Value)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

            _isRunning.Value = true;
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
                _elapsedSeconds.Value,
                _isRunning.Value,
                _isCompleted.Value);
        }

        private void HandleReplicatedChanged(float previous, float current)
        {
            ApplyReplicatedState();
        }

        private void HandleReplicatedChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
