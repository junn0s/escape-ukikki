using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 냉동고 온도 조절 미션의 서버 권위 판정이다(GDD §10.2). 목표 온도에
    /// 도달한 뒤 서버 시계로 유지 시간을 채우며, 온도가 벗어나면 매 프레임
    /// 판정에서 유지 시간이 0으로 되돌아간다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FreezerTemperatureStation))]
    public sealed class NetworkFreezerTemperatureAuthority : NetworkBehaviour
    {
        [SerializeField] private FreezerTemperatureStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<int> _currentTemperature = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _heldSecondsAtTarget = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            FreezerTemperatureStation station,
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
                    "[Mission] Freezer temperature authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestAdjust);
            _currentTemperature.OnValueChanged += HandleReplicatedChanged;
            _heldSecondsAtTarget.OnValueChanged += HandleReplicatedChanged;
            _isCompleted.OnValueChanged += HandleReplicatedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _currentTemperature.OnValueChanged -= HandleReplicatedChanged;
            _heldSecondsAtTarget.OnValueChanged -= HandleReplicatedChanged;
            _isCompleted.OnValueChanged -= HandleReplicatedChanged;
        }

        private void Update()
        {
            if (!IsServer || _station == null || _isCompleted.Value)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                return;
            }

            if (_station.Rules.Tick(
                    Time.deltaTime,
                    _config.FreezerHoldSeconds))
            {
                _isCompleted.Value = true;
            }

            _heldSecondsAtTarget.Value = _station.Rules.HeldSecondsAtTarget;
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

        private void RequestAdjust(GameObject interactor, int deltaDegrees)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestAdjustRpc(deltaDegrees);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestAdjustRpc(
            int deltaDegrees,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isCompleted.Value || deltaDegrees == 0)
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

            _station.Rules.Adjust(deltaDegrees);
            _currentTemperature.Value = _station.Rules.CurrentTemperature;
            _heldSecondsAtTarget.Value = _station.Rules.HeldSecondsAtTarget;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _currentTemperature.Value,
                _heldSecondsAtTarget.Value,
                _isCompleted.Value);
        }

        private void HandleReplicatedChanged(int previous, int current)
        {
            ApplyReplicatedState();
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
