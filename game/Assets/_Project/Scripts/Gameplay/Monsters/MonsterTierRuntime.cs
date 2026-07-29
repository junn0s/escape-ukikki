using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterTierRuntime : MonoBehaviour
    {
        [SerializeField] private MonsterTierConfig _config;
        [SerializeField, Range(MonsterTierConfig.MinimumTier, MonsterTierConfig.MaximumTier)]
        private int _smellTier;

        public event Action<int> SmellTierChanged;

        public MonsterTierConfig Config => _config;
        public int SmellTier => _smellTier;
        public float CurrentSmellRadius => _config.GetSmellRadius(_smellTier);

        public void Configure(MonsterTierConfig config)
        {
            _config = config;
            _smellTier = MonsterTierConfig.MinimumTier;
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

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[MonsterTier] MonsterTierConfig is missing.", this);
            }
        }
    }
}
