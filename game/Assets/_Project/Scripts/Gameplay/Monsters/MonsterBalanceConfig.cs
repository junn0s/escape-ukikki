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
        [SerializeField, Min(0.1f)] private float _visionDistance = 7f;
        [SerializeField, Range(1f, 360f)] private float _visionAngleDegrees = 100f;
        [SerializeField, Min(0.1f)] private float _biteDistance = 0.9f;
        [SerializeField, Min(0f)] private float _biteWindupSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _biteRecoverySeconds = 1.2f;
        [SerializeField, Min(0f)] private float _biteProtectionSeconds = 1.5f;
        [SerializeField, Min(1f)] private float _aiTicksPerSecond = 8f;

        public string Id => _id;
        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float NoiseInvestigateSpeed => _noiseInvestigateSpeed;
        public float NoiseAccelerationSeconds => _noiseAccelerationSeconds;
        public float RoomIdleSeconds => _roomIdleSeconds;
        public float RoomIdleVariationSeconds => _roomIdleVariationSeconds;
        public float SearchSeconds => _searchSeconds;
        public float VisionDistance => _visionDistance;
        public float VisionAngleDegrees => _visionAngleDegrees;
        public float BiteDistance => _biteDistance;
        public float BiteWindupSeconds => _biteWindupSeconds;
        public float BiteRecoverySeconds => _biteRecoverySeconds;
        public float BiteProtectionSeconds => _biteProtectionSeconds;
        public float AiTickIntervalSeconds => 1f / _aiTicksPerSecond;
    }
}
