using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Monster Tier", fileName = "SO_MonsterTier_Default")]
    public sealed class MonsterTierConfig : ScriptableObject
    {
        public const int MinimumTier = 0;
        public const int MaximumTier = 2;

        [SerializeField] private string _id = "monster_tier_default";
        [SerializeField, Min(0.1f)]
        private float _baseProximityDetectionRadius = 1.25f;
        [SerializeField, Min(0.1f)]
        private float _tierOneProximityDetectionRadius = 1.75f;
        [SerializeField, Min(0.1f)]
        private float _tierTwoProximityDetectionRadius = 2.25f;
        [SerializeField, Min(1)] private int _baseMonsterCount = 4;
        [SerializeField, Min(1)] private int _tierOneMonsterCount = 6;
        [SerializeField, Min(1)] private int _tierTwoMonsterCount = 8;
        [SerializeField, Min(1f)] private float _baseInfectionDurationSeconds = 90f;
        [SerializeField, Min(1f)] private float _tierOneInfectionDurationSeconds = 60f;
        [SerializeField, Min(1f)] private float _tierTwoInfectionDurationSeconds = 30f;

        public string Id => _id;

        public float GetProximityDetectionRadius(int tier)
        {
            ValidateTier(tier);
            return tier switch
            {
                0 => _baseProximityDetectionRadius,
                1 => _tierOneProximityDetectionRadius,
                _ => _tierTwoProximityDetectionRadius
            };
        }

        public int GetMonsterCount(int tier)
        {
            ValidateTier(tier);
            return tier switch
            {
                0 => _baseMonsterCount,
                1 => _tierOneMonsterCount,
                _ => _tierTwoMonsterCount
            };
        }

        public float GetInfectionDurationSeconds(int tier)
        {
            ValidateTier(tier);
            return tier switch
            {
                0 => _baseInfectionDurationSeconds,
                1 => _tierOneInfectionDurationSeconds,
                _ => _tierTwoInfectionDurationSeconds
            };
        }

        private static void ValidateTier(int tier)
        {
            if (tier < MinimumTier || tier > MaximumTier)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }
    }
}
