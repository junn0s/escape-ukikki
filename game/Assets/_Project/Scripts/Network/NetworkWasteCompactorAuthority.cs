using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 폐기물 통 압축 미션의 서버 권위 판정이다(GDD §10.2).
    /// 백신 데이터 다운로드와 같은 누르고 있기 판정을 공유한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(WasteCompactorStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkWasteCompactorAuthority : NetworkBehaviour
    {
        [SerializeField] private WasteCompactorStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<float> _heldSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isHolding = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ulong _holdingClientId = ulong.MaxValue;

        public void Configure(
            WasteCompactorStation station,
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
                    "[Mission] Waste compactor authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestHold);
            _heldSeconds.OnValueChanged += HandleReplicatedChanged;
            _isHolding.OnValueChanged += HandleReplicatedChanged;
            _isCompleted.OnValueChanged += HandleReplicatedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _heldSeconds.OnValueChanged -= HandleReplicatedChanged;
            _isHolding.OnValueChanged -= HandleReplicatedChanged;
            _isCompleted.OnValueChanged -= HandleReplicatedChanged;
        }

        private void Update()
        {
            if (!IsServer || _station == null || !_isHolding.Value ||
                _isCompleted.Value)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                ServerReleaseHold();
                return;
            }

            var next = _heldSeconds.Value + Time.deltaTime;
            var required = _station.RequiredSeconds;
            if (next >= required)
            {
                if (_missionAuthority.ServerTryCompleteLastInteractor())
                {
                    _heldSeconds.Value = required;
                    _isHolding.Value = false;
                    _isCompleted.Value = true;
                    _holdingClientId = ulong.MaxValue;
                }
                else
                {
                    ServerReleaseHold();
                }

                return;
            }

            _heldSeconds.Value = next;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestHold(GameObject interactor, bool isHolding)
        {
            if (isHolding && !CanLocalPlayerRequestInteraction(interactor))
            {
                return;
            }

            RequestHoldRpc(isHolding);
        }

        [Rpc(SendTo.Server)]
        private void RequestHoldRpc(
            bool isHolding,
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
                ServerReleaseHold();
                return;
            }

            if (isHolding)
            {
                if (_holdingClientId != ulong.MaxValue &&
                    _holdingClientId != senderClientId)
                {
                    return;
                }

                _holdingClientId = senderClientId;
                _isHolding.Value = true;
            }
            else if (_holdingClientId == senderClientId)
            {
                ServerReleaseHold();
            }
        }

        private void ServerReleaseHold()
        {
            _holdingClientId = ulong.MaxValue;
            _isHolding.Value = false;
            _heldSeconds.Value = 0f;
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
                _heldSeconds.Value,
                _isHolding.Value,
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
