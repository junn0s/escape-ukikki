using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    [CreateAssetMenu(menuName = "Monkey Lab/Missions/Fuse Mission Config", fileName = "SO_FuseMission_Default")]
    public sealed class FuseMissionConfig : ScriptableObject
    {
        [SerializeField] private string _id = "fuse_default";

        [SerializeField, Range(FuseMissionInstance.MinimumFuseCount, FuseMissionInstance.MaximumFuseCount)]
        private int _fuseCount = FuseMissionInstance.MinimumFuseCount;

        [SerializeField, Min(0.1f)]
        private float _breakerCycleSeconds = 2f;

        [SerializeField, Range(0.05f, 0.45f)]
        private float _breakerSuccessToleranceNormalized = 0.12f;

        [SerializeField, Min(0f)]
        private float _breakerNetworkToleranceSeconds = 0.12f;

        [SerializeField, Range(2, FuseMissionInstance.MaximumFuseCount)]
        private int _sampleCategoryCount = 3;

        [SerializeField, Range(0.05f, 0.95f)]
        private float _pressureTargetNormalized = 0.65f;

        [SerializeField, Range(0.02f, 0.25f)]
        private float _pressureToleranceNormalized = 0.08f;

        [SerializeField, Min(0f)]
        private float _pressureStabilizeSeconds = 2f;

        [SerializeField, Min(0f)]
        private float _pressureNetworkToleranceSeconds = 0.12f;

        public string Id => _id;
        public int FuseCount => Mathf.Clamp(
            _fuseCount,
            FuseMissionInstance.MinimumFuseCount,
            FuseMissionInstance.MaximumFuseCount);
        public float BreakerCycleSeconds =>
            Mathf.Max(0.1f, _breakerCycleSeconds);
        public float BreakerSuccessToleranceNormalized => Mathf.Clamp(
            _breakerSuccessToleranceNormalized,
            0.05f,
            0.45f);
        public float BreakerServerToleranceNormalized => Mathf.Clamp(
            BreakerSuccessToleranceNormalized +
            Mathf.Max(0f, _breakerNetworkToleranceSeconds) * 2f /
            BreakerCycleSeconds,
            0.05f,
            0.45f);
        public int SampleCategoryCount => Mathf.Clamp(
            _sampleCategoryCount,
            2,
            FuseCount);
        public float PressureTargetNormalized => Mathf.Clamp(
            _pressureTargetNormalized,
            0.05f,
            0.95f);
        public float PressureToleranceNormalized => Mathf.Clamp(
            _pressureToleranceNormalized,
            0.02f,
            0.25f);
        public float PressureStabilizeSeconds =>
            Mathf.Max(0f, _pressureStabilizeSeconds);
        public float PressureServerStabilizeSeconds => Mathf.Max(
            0f,
            PressureStabilizeSeconds -
            Mathf.Max(0f, _pressureNetworkToleranceSeconds));
    }
}
