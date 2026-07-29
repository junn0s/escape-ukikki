using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterTierRuntime : MonoBehaviour
    {
        [SerializeField] private MonsterTierConfig _config;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _smellTier;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _toxicityTier;

        public event Action<int> SmellTierChanged;
        public event Action<int> ToxicityTierChanged;

        public MonsterTierConfig Config => _config;
        public int SmellTier => _smellTier;
        public int ToxicityTier => _toxicityTier;
        public float CurrentSmellRadius => _config.GetSmellRadius(_smellTier);
        public float CurrentInfectionDurationSeconds =>
            _config.GetInfectionDurationSeconds(_toxicityTier);

        public void Configure(MonsterTierConfig config)
        {
            _config = config;
            _smellTier = MonsterTierConfig.MinimumTier;
            _toxicityTier = MonsterTierConfig.MinimumTier;
        }

        public void SetSmellTier(int tier)
        {
            _config.GetSmellRadius(tier);
            if (_smellTier == tier)
            {
                return;
            }

            _smellTier = tier;
            SmellTierChanged?.Invoke(tier);
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

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[MonsterTier] MonsterTierConfig is missing.", this);
            }
        }
    }
}
