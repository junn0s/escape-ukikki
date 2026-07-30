using System;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 강화 3축의 서버 권위 상태다.
    /// 강화 단계는 빌런에게만 알려야 하므로(docs/system-design-document.md §5)
    /// NetworkVariable로 브로드캐스트하지 않고 소유 클라이언트에게만 Rpc로 보낸다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkVillainUpgradeAuthority : NetworkBehaviour
    {
        [SerializeField] private MonsterTierRuntime _monsterTierRuntime;
        [SerializeField] private UpgradeBalanceConfig _config;
        [SerializeField] private NetworkMonsterPopulationSpawner _populationSpawner;

        private readonly VillainUpgradeState _serverState =
            new VillainUpgradeState();

        private int _localScentLevel;
        private int _localPopulationLevel;
        private int _localToxicityLevel;

        public static NetworkVillainUpgradeAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        /// <summary>빌런 본인 화면에서만 의미 있는 값이다.</summary>
        public event Action LocalUpgradeStateChanged;

        public UpgradeBalanceConfig Config => _config;
        public int LocalScentLevel => _localScentLevel;
        public int LocalPopulationLevel => _localPopulationLevel;
        public int LocalToxicityLevel => _localToxicityLevel;

        public int GetLocalLevel(UpgradeAxis axis)
        {
            return axis switch
            {
                UpgradeAxis.Scent => _localScentLevel,
                UpgradeAxis.Population => _localPopulationLevel,
                _ => _localToxicityLevel
            };
        }

        public void Configure(
            MonsterTierRuntime monsterTierRuntime,
            UpgradeBalanceConfig config,
            NetworkMonsterPopulationSpawner populationSpawner)
        {
            _monsterTierRuntime = monsterTierRuntime;
            _config = config;
            _populationSpawner = populationSpawner;
        }

        public override void OnNetworkSpawn()
        {
            if (_monsterTierRuntime == null || _config == null)
            {
                Debug.LogError(
                    "[Upgrade] Villain upgrade authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentChanged?.Invoke();

            if (IsServer)
            {
                _serverState.Reset();
                _monsterTierRuntime.SetProximityDetectionTier(
                    MonsterTierConfig.MinimumTier);
                _monsterTierRuntime.SetToxicityTier(
                    MonsterTierConfig.MinimumTier);
                _monsterTierRuntime.SetPopulationTier(
                    MonsterTierConfig.MinimumTier);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            _localScentLevel = 0;
            _localPopulationLevel = 0;
            _localToxicityLevel = 0;
        }

        public int ServerGetLevel(UpgradeAxis axis)
        {
            return _serverState.GetLevel(axis);
        }

        public bool ServerCanUpgrade(UpgradeAxis axis)
        {
            return _serverState.CanUpgrade(axis);
        }

        /// <summary>
        /// 강화 완료를 서버에서 확정한다.
        /// docs/system-design-document.md §11.2의 2~4단계를 수행한다.
        /// </summary>
        public bool ServerTryApplyUpgrade(
            ulong villainClientId,
            UpgradeAxis axis,
            out int newLevel,
            out UpgradeRejectionReason rejectionReason)
        {
            newLevel = _serverState.GetLevel(axis);
            rejectionReason = UpgradeRejectionReason.None;
            if (!IsServer)
            {
                return false;
            }

            if (!_serverState.TryUpgrade(axis, out newLevel))
            {
                rejectionReason = UpgradeRejectionReason.AxisAtMaximum;
                return false;
            }

            ApplyServerEffect(axis, newLevel);
            Debug.Log(
                $"[Upgrade] client {villainClientId} raised {axis} to level {newLevel}.",
                this);
            PublishUpgradeStateRpc(
                _serverState.ScentLevel,
                _serverState.PopulationLevel,
                _serverState.ToxicityLevel,
                RpcTarget.Single(villainClientId, RpcTargetUse.Temp));
            return true;
        }

        private void ApplyServerEffect(UpgradeAxis axis, int newLevel)
        {
            switch (axis)
            {
                case UpgradeAxis.Scent:
                    _monsterTierRuntime.SetProximityDetectionTier(newLevel);
                    break;
                case UpgradeAxis.Toxicity:
                    // 이미 감염된 생존자의 제한시간은 바꾸지 않는다(SDD §11.4).
                    // MonsterTierRuntime은 새 감염에만 참조된다.
                    _monsterTierRuntime.SetToxicityTier(newLevel);
                    break;
                case UpgradeAxis.Population:
                    _monsterTierRuntime.SetPopulationTier(newLevel);
                    _populationSpawner?.ServerBeginSpawnWave(
                        newLevel,
                        _config.MonsterSpawnWarningSeconds);
                    break;
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishUpgradeStateRpc(
            int scentLevel,
            int populationLevel,
            int toxicityLevel,
            RpcParams rpcParams = default)
        {
            _localScentLevel = scentLevel;
            _localPopulationLevel = populationLevel;
            _localToxicityLevel = toxicityLevel;
            LocalUpgradeStateChanged?.Invoke();
        }
    }
}
