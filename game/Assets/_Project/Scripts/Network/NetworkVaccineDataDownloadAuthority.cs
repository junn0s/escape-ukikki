using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 백신 데이터 다운로드 미션의 서버 권위 판정이다(GDD §10.2).
    /// 누르고 있는 동안만 진행하고 손을 떼면 0으로 초기화한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(VaccineDataDownloadStation))]
    public sealed class NetworkVaccineDataDownloadAuthority : NetworkBehaviour
    {
        [SerializeField] private VaccineDataDownloadStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

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
            VaccineDataDownloadStation station,
            SurvivorMissionBalanceConfig config,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _config = config;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_station == null || _config == null ||
                _interactionConfig == null)
            {
                Debug.LogError(
                    "[Mission] Vaccine data download authority references are missing.",
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
            var required = _config.VaccineDataDownloadHoldSeconds;
            if (next >= required)
            {
                _heldSeconds.Value = required;
                _isHolding.Value = false;
                _isCompleted.Value = true;
                _holdingClientId = ulong.MaxValue;
                return;
            }

            _heldSeconds.Value = next;
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
            if (IsServer || _station == null)
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
