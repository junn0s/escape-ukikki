using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 빌런 전용 8초 누르기 미션의 서버 권위 판정이다(GDD §13.2).
    /// 빌런에게 이 미션이 배정됐는지, 아직 완료하지 않았는지를 서버가 확인한다.
    /// 완료 시 클리어 횟수를 올리고(§13.3) 현장 단서를 남긴다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(VillainHoldButtonStation))]
    public sealed class NetworkVillainHoldButtonAuthority : NetworkBehaviour
    {
        [SerializeField] private VillainHoldButtonStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        [SerializeField] private ClueKind _clueKind;

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
            VillainHoldButtonStation station,
            InteractionBalanceConfig interactionConfig,
            ClueKind clueKind)
        {
            _station = station;
            _interactionConfig = interactionConfig;
            _clueKind = clueKind;
        }

        public override void OnNetworkSpawn()
        {
            if (_station == null || _interactionConfig == null)
            {
                Debug.LogError(
                    "[Villain] Hold button authority references are missing.",
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
            var required = _station.RequiredHoldSeconds;
            if (next >= required)
            {
                _heldSeconds.Value = required;
                _isHolding.Value = false;
                _isCompleted.Value = true;
                ServerHandleCompleted(_holdingClientId);
                _holdingClientId = ulong.MaxValue;
                return;
            }

            _heldSeconds.Value = next;
        }

        /// <summary>
        /// 이 미션이 빌런에게 배정됐고 아직 완료하지 않았는지 확인한다.
        /// 배정되지 않은 빌런 미션이나 이미 완료한 미션은 상호작용해도 반응하지 않는다.
        /// </summary>
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

            if (!interactor.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Villain)
            {
                return false;
            }

            var missionStack = NetworkVillainMissionStackAuthority.Current;
            if (missionStack == null ||
                !missionStack.LocalIsMissionAssigned(_station.Kind) ||
                missionStack.LocalIsMissionCompleted(_station.Kind))
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
            var missionStack = NetworkVillainMissionStackAuthority.Current;
            if (missionStack == null ||
                !missionStack.ServerCanPerformMission(
                    senderClientId,
                    _station.Kind))
            {
                ServerReleaseHold();
                return;
            }

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

        private void ServerHandleCompleted(ulong villainClientId)
        {
            var stackAuthority = NetworkVillainMissionStackAuthority.Current;
            if (stackAuthority == null ||
                !stackAuthority.ServerTryRegisterClear(
                    villainClientId,
                    _station.Kind,
                    out _))
            {
                return;
            }

            var clueAuthority = NetworkClueAuthority.Current;
            clueAuthority?.ServerActivateUpgradeClue(
                _clueKind,
                _station.RoomId);
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
