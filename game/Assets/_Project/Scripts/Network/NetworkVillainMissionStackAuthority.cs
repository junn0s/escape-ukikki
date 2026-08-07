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

        public static NetworkVillainMissionStackAuthority Current
        {
            get;
            private set;
        }
        public static event Action CurrentChanged;

        /// <summary>빌런 본인 화면에서만 의미 있는 값이다.</summary>
        public event Action LocalClearCountChanged;

        public int LocalClearCount { get; private set; }

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
        }

        public int ServerGetClearCount()
        {
            return _serverState.ClearCount;
        }

        /// <summary>
        /// 빌런 미션 완료를 서버에서 확정한다. 클리어 횟수를 1 올리고 새 단계의
        /// 몬스터 효과를 즉시 적용한다(SDD §11.2).
        /// </summary>
        public bool ServerTryRegisterClear(
            ulong villainClientId,
            out int newClearCount)
        {
            newClearCount = _serverState.ClearCount;
            if (!IsServer || !_serverState.TryIncrement(out newClearCount))
            {
                return false;
            }

            ApplyServerEffect(newClearCount);
            Debug.Log(
                $"[Villain] client {villainClientId} raised mission clear count to {newClearCount}.",
                this);
            PublishClearCountRpc(
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
            return true;
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
        private void PublishClearCountRpc(
            int clearCount,
            RpcParams rpcParams = default)
        {
            LocalClearCount = clearCount;
            LocalClearCountChanged?.Invoke();
        }
    }
}
