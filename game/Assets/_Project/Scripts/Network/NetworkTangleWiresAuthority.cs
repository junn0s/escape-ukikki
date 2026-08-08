using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 보안 카메라 선 꼬기 미션의 서버 권위 판정이다(GDD §13.2). 빌런에게
    /// 이 미션이 배정됐는지, 아직 완료하지 않았는지를 서버가 확인한다.
    /// 완료 시 클리어 횟수를 올리고(§13.3) 현장 단서를 남긴다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(TangleWiresStation))]
    public sealed class NetworkTangleWiresAuthority : NetworkBehaviour
    {
        [SerializeField] private TangleWiresStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        [SerializeField] private ClueKind _clueKind;

        private readonly NetworkVariable<int> _pluggedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            TangleWiresStation station,
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
                    "[Villain] Tangle wires authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestPlugWire);
            _pluggedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _pluggedMask.OnValueChanged -= HandleMaskChanged;
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

        private void RequestPlugWire(GameObject interactor, int wireIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestPlugWireRpc(wireIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestPlugWireRpc(
            int wireIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                wireIndex < 0 || wireIndex >= _station.Rules.WireCount)
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
                return;
            }

            var bit = 1 << wireIndex;
            if ((_pluggedMask.Value & bit) != 0)
            {
                return;
            }

            _pluggedMask.Value |= bit;
            if (CountSetBits(_pluggedMask.Value) >= _station.Rules.WireCount)
            {
                ServerHandleCompleted(senderClientId);
            }
        }

        private static int CountSetBits(int mask)
        {
            var count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
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

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null)
            {
                return;
            }

            var flags = new bool[_station.Rules.WireCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_pluggedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
