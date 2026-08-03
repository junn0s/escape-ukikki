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
        /// 개발 패널에서 단서나 채널링 없이 강화 결과만 즉시 적용한다.
        /// 릴리스 빌드에서는 호출을 거부한다.
        /// </summary>
        public bool ServerSetLevelForDevelopment(UpgradeAxis axis, int level)
        {
            if (!IsServer ||
                (!Application.isEditor && !Debug.isDebugBuild) ||
                level < VillainUpgradeState.MinimumLevel ||
                level > VillainUpgradeState.MaximumLevel)
            {
                return false;
            }

            _serverState.SetLevel(axis, level);
            switch (axis)
            {
                case UpgradeAxis.Scent:
                    _monsterTierRuntime.SetProximityDetectionTier(level);
                    break;
                case UpgradeAxis.Toxicity:
                    _monsterTierRuntime.SetToxicityTier(level);
                    break;
                case UpgradeAxis.Population:
                    _monsterTierRuntime.SetPopulationTier(level);
                    _populationSpawner?
                        .ServerSetPopulationTierForDevelopment(level);
                    break;
            }

            ServerPublishCurrentStateToVillain();
            return true;
        }

        public void ServerPublishCurrentStateToVillain()
        {
            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    avatar.Role != PlayerRole.Villain)
                {
                    continue;
                }

                PublishUpgradeStateRpc(
                    _serverState.ScentLevel,
                    _serverState.PopulationLevel,
                    _serverState.ToxicityLevel,
                    RpcTarget.Single(pair.Key, RpcTargetUse.Temp));
            }
        }

        /// <summary>
        /// 강화 완료를 서버에서 확정한다.
        /// docs/system-design-document.md §11.2의 2~4단계를 수행한다.
        /// </summary>
        public bool ServerTryApplyUpgrade(
            ulong villainClientId,
            UpgradeAxis axis,
            string roomId,
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
            // SDD §11.2의 5단계: 현장 단서를 생성한다.
            ServerLeaveClue(axis, roomId);
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

        /// <summary>
        /// 강화 축에 대응하는 현장 단서를 남긴다.
        /// 단서 위치는 실제 강화를 수행한 방과 일치시킨다(GDD §13.2~13.4).
        /// 개체 강화만 예외로, 조작은 보안실에서 하고 흔적은 격리실 문에 남는다.
        /// </summary>
        private void ServerLeaveClue(UpgradeAxis axis, string roomId)
        {
            var clueAuthority = NetworkClueAuthority.Current;
            if (clueAuthority == null)
            {
                return;
            }

            switch (axis)
            {
                case UpgradeAxis.Scent:
                    clueAuthority.ServerActivateUpgradeClue(
                        ClueKind.VentRedSmoke,
                        roomId);
                    break;
                case UpgradeAxis.Toxicity:
                    clueAuthority.ServerActivateUpgradeClue(
                        ClueKind.EmptySyringe,
                        roomId);
                    break;
                case UpgradeAxis.Population:
                    // 잠금이 파손되는 곳은 조작 패널이 아니라 격리실이다.
                    clueAuthority.ServerActivateUpgradeClue(
                        ClueKind.BrokenQuarantineLock,
                        preferredRoomId: null);
                    break;
            }
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
