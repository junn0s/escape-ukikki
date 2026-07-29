using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Monster", fileName = "SO_MonsterBalance_Default")]
    public sealed class MonsterBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "monster_default";
        [SerializeField, Min(0.1f)] private float _patrolSpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float _chaseSpeed = 4.6f;
        [SerializeField, Min(0.1f)] private float _noiseInvestigateSpeed = 6f;
        [SerializeField, Min(0.1f)] private float _noiseAccelerationSeconds = 6f;
        [SerializeField, Min(0.1f)] private float _roomIdleSeconds = 6f;
        [SerializeField, Min(0f)] private float _roomIdleVariationSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _searchSeconds = 3f;
        [SerializeField, Min(1f)] private float _aiTicksPerSecond = 8f;

        public string Id => _id;
        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float NoiseInvestigateSpeed => _noiseInvestigateSpeed;
        public float NoiseAccelerationSeconds => _noiseAccelerationSeconds;
        public float RoomIdleSeconds => _roomIdleSeconds;
        public float RoomIdleVariationSeconds => _roomIdleVariationSeconds;
        public float SearchSeconds => _searchSeconds;
        public float AiTickIntervalSeconds => 1f / _aiTicksPerSecond;
    }
}
