using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 에어록 압력 조절 미션의 서버 권위 판정이다(GDD §10.2).
    /// 다이얼 회전 델타를 받아 서버가 각도를 누적하고 완료를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AirlockDialStation))]
    public sealed class NetworkAirlockDialAuthority : NetworkBehaviour
    {
        [SerializeField] private AirlockDialStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<float> _angleDegrees = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            AirlockDialStation station,
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
                    "[Mission] Airlock dial authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            if (IsServer)
            {
                _angleDegrees.Value = _config.AirlockDialStartOffsetDegrees;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestRotate);
            _angleDegrees.OnValueChanged += HandleAngleChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _angleDegrees.OnValueChanged -= HandleAngleChanged;
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

        private void RequestRotate(GameObject interactor, float deltaDegrees)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestRotateRpc(deltaDegrees);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestRotateRpc(
            float deltaDegrees,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _station.Rules.IsCompleted)
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

            _angleDegrees.Value =
                Mathf.DeltaAngle(0f, _angleDegrees.Value + deltaDegrees);
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(_angleDegrees.Value);
        }

        private void HandleAngleChanged(float previous, float current)
        {
            ApplyReplicatedState();
        }
    }
}
