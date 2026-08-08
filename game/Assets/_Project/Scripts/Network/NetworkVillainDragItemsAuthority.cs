using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 빌런 전용 드래그 배치 미션의 서버 권위 판정이다(GDD §13.2).
    /// 투약 기록 삭제(입원실)가 이 조작을 쓴다. 빌런에게 이 미션이 배정됐는지,
    /// 아직 완료하지 않았는지를 서버가 확인한다. 완료 시 클리어 횟수를 올리고
    /// (§13.3) 현장 단서를 남긴다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(VillainDragItemsStation))]
    public sealed class NetworkVillainDragItemsAuthority : NetworkBehaviour
    {
        [SerializeField] private VillainDragItemsStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        [SerializeField] private ClueKind _clueKind;

        private readonly NetworkVariable<int> _placedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            VillainDragItemsStation station,
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
                    "[Villain] Drag items authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestPlaceItem);
            _placedMask.OnValueChanged += HandleMaskChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _placedMask.OnValueChanged -= HandleMaskChanged;
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

        private void RequestPlaceItem(GameObject interactor, int itemIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestPlaceItemRpc(itemIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestPlaceItemRpc(
            int itemIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                itemIndex < 0 || itemIndex >= _station.Rules.ItemCount)
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

            var bit = 1 << itemIndex;
            if ((_placedMask.Value & bit) != 0)
            {
                return;
            }

            _placedMask.Value |= bit;
            if (CountSetBits(_placedMask.Value) >= _station.Rules.ItemCount)
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

            var flags = new bool[_station.Rules.ItemCount];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = (_placedMask.Value & (1 << index)) != 0;
            }

            _station.ApplyAuthoritativeState(flags);
        }

        private void HandleMaskChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
