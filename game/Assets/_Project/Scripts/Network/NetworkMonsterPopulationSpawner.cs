using System;
using System.Collections;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Monsters;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 개체 강화로 추가되는 괴물을 활성화한다.
    /// docs/system-design-document.md §11.3에 따라 1단계는 격리실 A,
    /// 2단계는 격리실 B에서 두 마리씩 3초 예고 후 활성화한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMonsterPopulationSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkMonsterAuthority[] _baseMonsters =
            Array.Empty<NetworkMonsterAuthority>();
        [SerializeField] private NetworkMonsterAuthority[] _tierOneMonsters =
            Array.Empty<NetworkMonsterAuthority>();
        [SerializeField] private NetworkMonsterAuthority[] _tierTwoMonsters =
            Array.Empty<NetworkMonsterAuthority>();
        [SerializeField] private MonsterTierConfig _tierConfig;

        private readonly HashSet<int> _activatedTiers = new();

        /// <summary>경고 재생 시작을 모든 클라이언트에 알린다(위치는 공개 정보다).</summary>
        public event Action<Vector2, float> SpawnWarningStarted;
        public event Action SpawnWaveActivated;

        public int BaseMonsterCount => _baseMonsters?.Length ?? 0;
        public int TierOneMonsterCount => _tierOneMonsters?.Length ?? 0;
        public int TierTwoMonsterCount => _tierTwoMonsters?.Length ?? 0;
        public MonsterTierConfig TierConfig => _tierConfig;

        public void Configure(
            NetworkMonsterAuthority[] baseMonsters,
            NetworkMonsterAuthority[] tierOneMonsters,
            NetworkMonsterAuthority[] tierTwoMonsters,
            MonsterTierConfig tierConfig)
        {
            _baseMonsters =
                baseMonsters ?? Array.Empty<NetworkMonsterAuthority>();
            _tierOneMonsters =
                tierOneMonsters ?? Array.Empty<NetworkMonsterAuthority>();
            _tierTwoMonsters =
                tierTwoMonsters ?? Array.Empty<NetworkMonsterAuthority>();
            _tierConfig = tierConfig;
        }

        /// <summary>
        /// 배치된 괴물 수가 밸런스 표(4/6/8)와 맞는지 확인한다.
        /// 씬 구성과 SO 값이 어긋나면 조용히 넘어가지 않고 알린다.
        /// </summary>
        public bool MatchesBalanceTable(int populationTier)
        {
            if (_tierConfig == null)
            {
                return false;
            }

            var activeCount = BaseMonsterCount;
            if (populationTier >= 1)
            {
                activeCount += TierOneMonsterCount;
            }

            if (populationTier >= 2)
            {
                activeCount += TierTwoMonsterCount;
            }

            return activeCount == _tierConfig.GetMonsterCount(populationTier);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            _activatedTiers.Clear();
            SetWaveActive(_tierOneMonsters, false);
            SetWaveActive(_tierTwoMonsters, false);
        }

        public override void OnNetworkDespawn()
        {
            _activatedTiers.Clear();
        }

        /// <summary>
        /// 해당 단계의 추가 괴물을 예고 후 활성화한다. 같은 단계는 한 번만 처리한다.
        /// </summary>
        public void ServerBeginSpawnWave(int populationTier, float warningSeconds)
        {
            if (!IsServer || !_activatedTiers.Add(populationTier))
            {
                return;
            }

            var wave = GetWave(populationTier);
            if (wave.Length == 0)
            {
                Debug.LogWarning(
                    $"[Upgrade] Population tier {populationTier} has no monsters assigned.",
                    this);
                return;
            }

            if (!MatchesBalanceTable(populationTier))
            {
                Debug.LogWarning(
                    $"[Upgrade] Population tier {populationTier} monster count " +
                    "does not match SO_MonsterTier. Check the scene setup.",
                    this);
            }

            var warningPosition = (Vector2)wave[0].transform.position;
            PublishSpawnWarningRpc(warningPosition, warningSeconds);
            StartCoroutine(ActivateAfterWarning(wave, warningSeconds));
        }

        /// <summary>개발 패널에서 4/6/8마리 단계를 즉시 오가며 확인한다.</summary>
        public bool ServerSetPopulationTierForDevelopment(int populationTier)
        {
            if (!IsServer ||
                (!Application.isEditor && !Debug.isDebugBuild) ||
                populationTier < MonsterTierConfig.MinimumTier ||
                populationTier > MonsterTierConfig.MaximumTier)
            {
                return false;
            }

            StopAllCoroutines();
            _activatedTiers.Clear();
            SetWaveActive(_tierOneMonsters, populationTier >= 1);
            SetWaveActive(_tierTwoMonsters, populationTier >= 2);
            if (populationTier >= 1)
            {
                _activatedTiers.Add(1);
            }

            if (populationTier >= 2)
            {
                _activatedTiers.Add(2);
            }

            PublishSpawnActivatedRpc();
            return true;
        }

        private IEnumerator ActivateAfterWarning(
            NetworkMonsterAuthority[] wave,
            float warningSeconds)
        {
            if (warningSeconds > 0f)
            {
                yield return new WaitForSeconds(warningSeconds);
            }

            SetWaveActive(wave, true);
            PublishSpawnActivatedRpc();
            Debug.Log(
                $"[Upgrade] Activated {wave.Length} additional monsters.",
                this);
        }

        private NetworkMonsterAuthority[] GetWave(int populationTier)
        {
            return populationTier switch
            {
                1 => _tierOneMonsters,
                2 => _tierTwoMonsters,
                _ => Array.Empty<NetworkMonsterAuthority>()
            };
        }

        private static void SetWaveActive(
            NetworkMonsterAuthority[] wave,
            bool isActive)
        {
            if (wave == null)
            {
                return;
            }

            for (var index = 0; index < wave.Length; index++)
            {
                var monster = wave[index];
                if (monster == null)
                {
                    continue;
                }

                var monsterObject = monster.gameObject;
                if (monsterObject.activeSelf != isActive)
                {
                    monsterObject.SetActive(isActive);
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishSpawnWarningRpc(
            Vector2 warningPosition,
            float warningSeconds)
        {
            SpawnWarningStarted?.Invoke(warningPosition, warningSeconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishSpawnActivatedRpc()
        {
            SpawnWaveActivated?.Invoke();
        }
    }
}
