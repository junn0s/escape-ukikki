using MonkeyLab.Gameplay.Noise;
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
        [SerializeField, Min(0.1f)] private float _searchSeconds = 5f;
        [SerializeField, Min(0.1f)]
        private float _missionFailureAmbushRadius = 5.333333f;
        [SerializeField, Min(0.1f)] private float _speakerAmbushRadius = 8f;
        [SerializeField, Min(0.1f)] private float _forcedNoiseRoamSeconds = 10f;
        [SerializeField, Min(0.1f)] private float _biteDistance = 0.2f;
        [SerializeField, Min(0f)] private float _biteWindupSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _biteRecoverySeconds = 1.2f;
        [SerializeField, Min(0f)] private float _biteProtectionSeconds = 1.5f;
        [SerializeField, Min(1f)] private float _aiTicksPerSecond = 8f;
        [SerializeField, Min(0f)]
        private float _footstepMinimumSpeedMetersPerSecond = 0.15f;
        [SerializeField, Min(0f)]
        private float _footstepReleaseDelaySeconds = 0.2f;
        [SerializeField, Range(1, 6)]
        private int _patrolRecentDestinationCount = 3;
        [SerializeField, Min(0f)]
        private float _patrolDestinationSeparationMeters = 8f;
        [SerializeField, Min(0f)]
        private float _movementSeparationRadiusMeters = 2.2f;
        [SerializeField, Range(0f, 0.95f)]
        private float _movementSeparationWeight = 0.55f;
        [SerializeField, Min(0.1f)]
        private float _pathStallSeconds = 2f;
        [SerializeField, Range(1, 5)]
        private int _pathRecoveryAttemptLimit = 3;

        public string Id => _id;
        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float NoiseInvestigateSpeed => _noiseInvestigateSpeed;
        public float NoiseAccelerationSeconds => _noiseAccelerationSeconds;
        public float RoomIdleSeconds => _roomIdleSeconds;
        public float RoomIdleVariationSeconds => _roomIdleVariationSeconds;
        public float SearchSeconds => _searchSeconds;
        public float MissionFailureAmbushRadius =>
            _missionFailureAmbushRadius;
        public float SpeakerAmbushRadius => _speakerAmbushRadius;
        public float ForcedNoiseRoamSeconds => _forcedNoiseRoamSeconds;
        public float BiteDistance => _biteDistance;
        public float BiteWindupSeconds => _biteWindupSeconds;
        public float BiteRecoverySeconds => _biteRecoverySeconds;
        public float BiteProtectionSeconds => _biteProtectionSeconds;
        public float AiTickIntervalSeconds => 1f / _aiTicksPerSecond;
        public float FootstepMinimumSpeedMetersPerSecond =>
            _footstepMinimumSpeedMetersPerSecond;
        public float FootstepReleaseDelaySeconds =>
            _footstepReleaseDelaySeconds;
        public int PatrolRecentDestinationCount =>
            Mathf.Clamp(_patrolRecentDestinationCount, 1, 6);
        public float PatrolDestinationSeparationMeters =>
            Mathf.Max(0f, _patrolDestinationSeparationMeters);
        public float MovementSeparationRadiusMeters =>
            Mathf.Max(0f, _movementSeparationRadiusMeters);
        public float MovementSeparationWeight =>
            Mathf.Clamp(_movementSeparationWeight, 0f, 0.95f);
        public float PathStallSeconds => Mathf.Max(0.1f, _pathStallSeconds);
        public int PathRecoveryAttemptLimit =>
            Mathf.Clamp(_pathRecoveryAttemptLimit, 1, 5);

        public float GetForcedNoiseAmbushRadius(
            NoiseSourceType sourceType)
        {
            return sourceType switch
            {
                NoiseSourceType.MissionFailure =>
                    MissionFailureAmbushRadius,
                NoiseSourceType.Speaker => SpeakerAmbushRadius,
                _ => 0f
            };
        }
    }
}
