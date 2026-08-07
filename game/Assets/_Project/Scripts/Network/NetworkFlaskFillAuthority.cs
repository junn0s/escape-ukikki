using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 플라스크 용액 채우기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 누르고 있는 동안 서버 시계로 게이지를 채우고, 손을 뗀 시점이 목표
    /// 구간 안이면 완료를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FlaskFillStation))]
    public sealed class NetworkFlaskFillAuthority : NetworkBehaviour
    {
        [SerializeField] private FlaskFillStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly NetworkVariable<float> _filledSeconds = new(
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
            FlaskFillStation station,
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
                    "[Mission] Flask fill authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestHold);
            _filledSeconds.OnValueChanged += HandleReplicatedChanged;
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

            _filledSeconds.OnValueChanged -= HandleReplicatedChanged;
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

            _station.Rules.Tick(Time.deltaTime);
            _filledSeconds.Value = _station.Rules.FilledSeconds;

            if (_station.Rules.IsOverfilled)
            {
                // 목표 구간을 넘겨 100%를 채우면 실패로 손을 뗀 것과 같다.
                ServerReleaseHold();
                _station.Rules.Reset();
                _filledSeconds.Value = 0f;
            }
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
                _station.Rules.BeginHold();
                _isHolding.Value = true;
            }
            else if (_holdingClientId == senderClientId)
            {
                ServerFinishRelease();
            }
        }

        private void ServerFinishRelease()
        {
            var succeeded = _station.Rules.ReleaseHold();
            _holdingClientId = ulong.MaxValue;
            _isHolding.Value = false;
            _filledSeconds.Value = _station.Rules.FilledSeconds;
            if (succeeded)
            {
                _isCompleted.Value = true;
            }
        }

        /// <summary>
        /// 거리 이탈이나 회의 시작처럼 의도치 않게 끊긴 경우다. 목표 구간
        /// 여부와 무관하게 항상 실패로 처리하고 게이지를 0으로 되돌린다.
        /// </summary>
        private void ServerReleaseHold()
        {
            _station.Rules.Reset();
            _holdingClientId = ulong.MaxValue;
            _isHolding.Value = false;
            _filledSeconds.Value = 0f;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _filledSeconds.Value,
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
