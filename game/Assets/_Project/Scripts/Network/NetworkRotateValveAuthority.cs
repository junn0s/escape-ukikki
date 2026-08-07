using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 액체 보관실 밸브의 서버 권위 판정이다(GDD §10.2, §13.2). 생존자는
    /// 시계 방향으로 잠그고, 빌런은 반시계 방향으로 푼다 — 같은 오브젝트를
    /// 반대 방향으로 조작하는 유일한 미션 쌍이다. 요청자 역할에 따라 서버가
    /// 어느 진행치에 반영할지 결정한다. 빌런 완료 시 클리어 횟수를 올리고
    /// (§13.3) 현장 단서를 남긴다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(RotateValveStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkRotateValveAuthority : NetworkBehaviour
    {
        [SerializeField] private RotateValveStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        [SerializeField] private ClueKind _villainClueKind;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<float> _lockedDegrees = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isLocked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _loosenedDegrees = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isLoosened = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            RotateValveStation station,
            InteractionBalanceConfig interactionConfig,
            ClueKind villainClueKind)
        {
            _station = station;
            _interactionConfig = interactionConfig;
            _villainClueKind = villainClueKind;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority = GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _interactionConfig == null ||
                _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] Rotate valve authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestRotate);
            _lockedDegrees.OnValueChanged += HandleFloatChanged;
            _isLocked.OnValueChanged += HandleBoolChanged;
            _loosenedDegrees.OnValueChanged += HandleFloatChanged;
            _isLoosened.OnValueChanged += HandleBoolChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _lockedDegrees.OnValueChanged -= HandleFloatChanged;
            _isLocked.OnValueChanged -= HandleBoolChanged;
            _loosenedDegrees.OnValueChanged -= HandleFloatChanged;
            _isLoosened.OnValueChanged -= HandleBoolChanged;
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
                    out var avatar))
            {
                return false;
            }

            if (avatar.Role == PlayerRole.Villain)
            {
                var missionStack =
                    NetworkVillainMissionStackAuthority.Current;
                if (missionStack == null ||
                    !missionStack.LocalIsMissionAssigned(
                        VillainMissionKind.ValvePressureRelease) ||
                    missionStack.LocalIsMissionCompleted(
                        VillainMissionKind.ValvePressureRelease))
                {
                    return false;
                }
                return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                           out var villainInfection) ||
                       villainInfection.LifeState != PlayerLifeState.DeadGhost;
            }

            return _missionAuthority.CanLocalPlayerRequestInteraction(
                interactor);
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
            if (NetworkManager == null || _station == null)
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

            var isVillain =
                playerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) &&
                avatar.Role == PlayerRole.Villain;

            if (isVillain)
            {
                var missionStack =
                    NetworkVillainMissionStackAuthority.Current;
                if (missionStack == null ||
                    !missionStack.ServerCanPerformMission(
                        senderClientId,
                        VillainMissionKind.ValvePressureRelease))
                {
                    return;
                }

                if (_isLoosened.Value ||
                    !_station.LoosenRules.Rotate(deltaDegrees))
                {
                    return;
                }

                _loosenedDegrees.Value = _station.LoosenRules.AccumulatedDegrees;
                if (_station.LoosenRules.IsCompleted)
                {
                    _isLoosened.Value = true;
                    ServerHandleVillainCompleted(senderClientId);
                }

                return;
            }

            if (!_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

            if (_isLocked.Value || !_station.LockRules.Rotate(deltaDegrees))
            {
                return;
            }

            _lockedDegrees.Value = _station.LockRules.AccumulatedDegrees;
            if (_station.LockRules.IsCompleted)
            {
                if (_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _isLocked.Value = true;
                }
                else
                {
                    _station.LockRules.Reset();
                    _lockedDegrees.Value = 0f;
                }
            }
        }

        private void ServerHandleVillainCompleted(ulong villainClientId)
        {
            var stackAuthority = NetworkVillainMissionStackAuthority.Current;
            if (stackAuthority == null ||
                !stackAuthority.ServerTryRegisterClear(
                    villainClientId,
                    VillainMissionKind.ValvePressureRelease,
                    out _))
            {
                return;
            }

            var clueAuthority = NetworkClueAuthority.Current;
            clueAuthority?.ServerActivateUpgradeClue(
                _villainClueKind,
                _station.RoomId);
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _lockedDegrees.Value,
                _isLocked.Value,
                _loosenedDegrees.Value,
                _isLoosened.Value);
        }

        private void HandleFloatChanged(float previous, float current)
        {
            ApplyReplicatedState();
        }

        private void HandleBoolChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
