using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterTierRuntime : MonoBehaviour
    {
        [SerializeField] private MonsterTierConfig _config;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _proximityDetectionTier;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _toxicityTier;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _populationTier;

        public event Action<int> ProximityDetectionTierChanged;
        public event Action<int> ToxicityTierChanged;
        public event Action<int> PopulationTierChanged;

        public MonsterTierConfig Config => _config;
        public int ProximityDetectionTier => _proximityDetectionTier;
        public int ToxicityTier => _toxicityTier;
        public int PopulationTier => _populationTier;
        public float CurrentProximityDetectionRadius =>
            _config.GetProximityDetectionRadius(_proximityDetectionTier);
        public float CurrentInfectionDurationSeconds =>
            _config.GetInfectionDurationSeconds(_toxicityTier);
        public int CurrentMonsterCount =>
            _config.GetMonsterCount(_populationTier);

        public void Configure(MonsterTierConfig config)
        {
            _config = config;
            _proximityDetectionTier = MonsterTierConfig.MinimumTier;
            _toxicityTier = MonsterTierConfig.MinimumTier;
            _populationTier = MonsterTierConfig.MinimumTier;
        }

        public void SetProximityDetectionTier(int tier)
        {
            _config.GetProximityDetectionRadius(tier);
            if (_proximityDetectionTier == tier)
            {
                return;
            }

            _proximityDetectionTier = tier;
            ProximityDetectionTierChanged?.Invoke(tier);
        }

        public void SetToxicityTier(int tier)
        {
            _config.GetInfectionDurationSeconds(tier);
            if (_toxicityTier == tier)
            {
                return;
            }

            _toxicityTier = tier;
            ToxicityTierChanged?.Invoke(tier);
        }

        public void SetPopulationTier(int tier)
        {
            _config.GetMonsterCount(tier);
            if (_populationTier == tier)
            {
                return;
            }

            _populationTier = tier;
            PopulationTierChanged?.Invoke(tier);
        }

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[MonsterTier] MonsterTierConfig is missing.", this);
            }
        }
    }
}
