using System;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 빌런 전용 미션 누적 클리어 횟수의 서버 권위 상태다(GDD §13.2~13.3).
    /// 클리어 횟수는 빌런에게만 알려야 하므로 NetworkVariable로 브로드캐스트하지 않고
    /// 소유 클라이언트에게만 Rpc로 보낸다. 몬스터 tier 효과는 전원에게 즉시 적용된다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkVillainMissionStackAuthority : NetworkBehaviour
    {
        [SerializeField] private MonsterTierRuntime _monsterTierRuntime;
        [SerializeField] private NetworkMonsterPopulationSpawner _populationSpawner;
        [SerializeField, Min(0.1f)] private float _spawnWarningSeconds = 3f;

        private readonly VillainMissionClearState _serverState = new();
        private ulong _serverVillainClientId = ulong.MaxValue;

        public static NetworkVillainMissionStackAuthority Current
        {
            get;
            private set;
        }
        public static event Action CurrentChanged;

        /// <summary>빌런 본인 화면에서만 의미 있는 값이다.</summary>
        public event Action LocalClearCountChanged;
        public event Action LocalMissionStateChanged;

        public int LocalClearCount { get; private set; }
        public int LocalAssignedMissionMask { get; private set; }
        public int LocalCompletedMissionMask { get; private set; }
        public int LocalAssignedMissionCount =>
            CountSetBits(LocalAssignedMissionMask);
        public MonsterTierConfig TierConfig =>
            _monsterTierRuntime != null ? _monsterTierRuntime.Config : null;

        public void Configure(
            MonsterTierRuntime monsterTierRuntime,
            NetworkMonsterPopulationSpawner populationSpawner)
        {
            _monsterTierRuntime = monsterTierRuntime;
            _populationSpawner = populationSpawner;
        }

        public override void OnNetworkSpawn()
        {
            if (_monsterTierRuntime == null)
            {
                Debug.LogError(
                    "[Villain] Mission stack authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentChanged?.Invoke();

            if (IsServer)
            {
                _serverState.Reset();
                _serverVillainClientId = ulong.MaxValue;
                ApplyServerEffect(0);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            LocalClearCount = 0;
            LocalAssignedMissionMask = 0;
            LocalCompletedMissionMask = 0;
        }

        public int ServerGetClearCount()
        {
            return _serverState.ClearCount;
        }

        public bool LocalIsMissionAssigned(VillainMissionKind kind)
        {
            return (LocalAssignedMissionMask & GetMissionBit(kind)) != 0;
        }

        public bool LocalIsMissionCompleted(VillainMissionKind kind)
        {
            return (LocalCompletedMissionMask & GetMissionBit(kind)) != 0;
        }

        /// <summary>라운드 시작 때 서버가 빌런 한 명에게만 6종 중 4종을 배정한다.</summary>
        public bool ServerAssignMissions(ulong villainClientId, int seed)
        {
            if (!IsServer || NetworkManager == null ||
                !IsConnectedVillain(villainClientId))
            {
                return false;
            }

            var assigned = VillainMissionAssignmentService.Assign(seed);
            _serverState.Assign(assigned);
            _serverVillainClientId = villainClientId;
            ApplyServerEffect(0);
            PublishMissionStateRpc(
                _serverState.AssignedMissionMask,
                _serverState.CompletedMissionMask,
                _serverState.ClearCount,
                RpcTarget.Single(villainClientId, RpcTargetUse.Temp));
            return true;
        }

        public bool ServerCanPerformMission(
            ulong villainClientId,
            VillainMissionKind missionKind)
        {
            return IsServer &&
                   villainClientId == _serverVillainClientId &&
                   IsConnectedVillain(villainClientId) &&
                   _serverState.IsAssigned(missionKind) &&
                   !_serverState.IsCompleted(missionKind);
        }

        /// <summary>
        /// 빌런 미션 완료를 서버에서 확정한다. 클리어 횟수를 1 올리고 새 단계의
        /// 몬스터 효과를 즉시 적용한다(SDD §11.2).
        /// </summary>
        public bool ServerTryRegisterClear(
            ulong villainClientId,
            VillainMissionKind missionKind,
            out int newClearCount)
        {
            newClearCount = _serverState.ClearCount;
            if (!ServerCanPerformMission(villainClientId, missionKind) ||
                !_serverState.TryComplete(missionKind, out newClearCount))
            {
                return false;
            }

            ApplyServerEffect(newClearCount);
            Debug.Log(
                $"[Villain] client {villainClientId} raised mission clear count to {newClearCount}.",
                this);
            PublishMissionStateRpc(
                _serverState.AssignedMissionMask,
                _serverState.CompletedMissionMask,
                newClearCount,
                RpcTarget.Single(villainClientId, RpcTargetUse.Temp));
            return true;
        }

        /// <summary>개발 패널에서 미션 없이 클리어 횟수만 즉시 적용한다.</summary>
        public bool ServerSetClearCountForDevelopment(int clearCount)
        {
            if (!IsServer ||
                (!Application.isEditor && !Debug.isDebugBuild) ||
                clearCount < 0 ||
                clearCount > VillainMissionClearState.MaximumClearCount)
            {
                return false;
            }

            _serverState.SetClearCount(clearCount);
            ApplyServerEffect(clearCount);
            ServerPublishCurrentStateToVillain();
            return true;
        }

        public void ServerPublishCurrentStateToVillain()
        {
            if (!IsServer || NetworkManager == null ||
                _serverVillainClientId == ulong.MaxValue ||
                !NetworkManager.ConnectedClients.ContainsKey(
                    _serverVillainClientId))
            {
                return;
            }

            PublishMissionStateRpc(
                _serverState.AssignedMissionMask,
                _serverState.CompletedMissionMask,
                _serverState.ClearCount,
                RpcTarget.Single(
                    _serverVillainClientId,
                    RpcTargetUse.Temp));
        }

        private void ApplyServerEffect(int clearCount)
        {
            var populationTier =
                VillainMissionStackEffectRules.GetPopulationTier(clearCount);
            var toxicityTier =
                VillainMissionStackEffectRules.GetToxicityTier(clearCount);
            var proximityTier =
                VillainMissionStackEffectRules.GetProximityDetectionTier(
                    clearCount);

            _monsterTierRuntime.SetPopulationTier(populationTier);
            _monsterTierRuntime.SetToxicityTier(toxicityTier);
            _monsterTierRuntime.SetProximityDetectionTier(proximityTier);

            if (populationTier > 0)
            {
                _populationSpawner?.ServerBeginSpawnWave(
                    populationTier,
                    _spawnWarningSeconds);
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishMissionStateRpc(
            int assignedMissionMask,
            int completedMissionMask,
            int clearCount,
            RpcParams rpcParams = default)
        {
            LocalAssignedMissionMask = assignedMissionMask;
            LocalCompletedMissionMask = completedMissionMask;
            LocalClearCount = clearCount;
            LocalClearCountChanged?.Invoke();
            LocalMissionStateChanged?.Invoke();
        }

        private static int GetMissionBit(VillainMissionKind kind)
        {
            return 1 << (int)kind;
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

        private bool IsConnectedVillain(ulong clientId)
        {
            return NetworkManager != null &&
                   NetworkManager.ConnectedClients.TryGetValue(
                       clientId,
                       out var client) &&
                   client.PlayerObject != null &&
                   client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                       out var avatar) &&
                   avatar.Role == PlayerRole.Villain;
        }
    }
}
