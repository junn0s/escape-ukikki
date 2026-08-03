using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 강화 미션과 개체 강화 예고 수치다.
    /// 필드 이름은 docs/balance-and-telemetry.md §6 표의 키와 맞춘다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Upgrade Balance Config",
        fileName = "SO_UpgradeBalance_Default")]
    public sealed class UpgradeBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "upgrade_default";
        [SerializeField, Min(0.1f)]
        private float _upgradeMissionMinimumSeconds = 12f;
        [SerializeField, Min(0.1f)]
        private float _upgradeMissionMaximumSeconds = 18f;
        [SerializeField, Min(0f)]
        private float _monsterSpawnWarningSeconds = 3f;
        [SerializeField, Range(3, 5)]
        private int _challengeItemCount = 3;
        [SerializeField, Range(0.1f, 0.8f)]
        private float _scentTargetMinimumNormalized = 0.45f;
        [SerializeField, Range(0.2f, 0.95f)]
        private float _scentTargetMaximumNormalized = 0.75f;
        [SerializeField, Range(0.02f, 0.25f)]
        private float _scentToleranceNormalized = 0.08f;
        [SerializeField, Min(0f)]
        private float _scentStabilizeSeconds = 1.5f;
        [SerializeField, Min(0f)]
        private float _scentNetworkToleranceSeconds = 0.12f;
        [SerializeField, Min(0.1f)]
        private float _toxicityCycleSeconds = 2f;
        [SerializeField, Range(0.05f, 0.45f)]
        private float _toxicitySuccessToleranceNormalized = 0.12f;
        [SerializeField, Min(0f)]
        private float _toxicityNetworkToleranceSeconds = 0.12f;

        public string Id => _id;

        public float UpgradeMissionMinimumSeconds =>
            Mathf.Max(0.1f, _upgradeMissionMinimumSeconds);

        public float UpgradeMissionMaximumSeconds =>
            Mathf.Max(
                UpgradeMissionMinimumSeconds,
                _upgradeMissionMaximumSeconds);

        public float MonsterSpawnWarningSeconds =>
            Mathf.Max(0f, _monsterSpawnWarningSeconds);
        public int ChallengeItemCount => Mathf.Clamp(
            _challengeItemCount,
            3,
            5);
        public float ScentTargetMinimumNormalized => Mathf.Clamp(
            _scentTargetMinimumNormalized,
            0.1f,
            0.8f);
        public float ScentTargetMaximumNormalized => Mathf.Clamp(
            _scentTargetMaximumNormalized,
            ScentTargetMinimumNormalized + 0.05f,
            0.95f);
        public float ScentToleranceNormalized => Mathf.Clamp(
            _scentToleranceNormalized,
            0.02f,
            0.25f);
        public float ScentStabilizeSeconds =>
            Mathf.Max(0f, _scentStabilizeSeconds);
        public float ScentNetworkToleranceSeconds =>
            Mathf.Max(0f, _scentNetworkToleranceSeconds);
        public float ScentServerStabilizeSeconds => Mathf.Max(
            0f,
            ScentStabilizeSeconds -
            ScentNetworkToleranceSeconds);
        public float ToxicityCycleSeconds =>
            Mathf.Max(0.1f, _toxicityCycleSeconds);
        public float ToxicitySuccessToleranceNormalized => Mathf.Clamp(
            _toxicitySuccessToleranceNormalized,
            0.05f,
            0.45f);
        public float ToxicityNetworkToleranceSeconds =>
            Mathf.Max(0f, _toxicityNetworkToleranceSeconds);
        public float ToxicityServerToleranceNormalized => Mathf.Clamp(
            ToxicitySuccessToleranceNormalized +
            ToxicityNetworkToleranceSeconds * 2f /
            ToxicityCycleSeconds,
            0.05f,
            0.45f);

        /// <summary>
        /// 강화 미션 1회의 소요 시간이다. 축마다 고정 시간을 쓰기 위해
        /// 축 인덱스를 최소~최대 구간에 균등 배치한다.
        /// </summary>
        public float GetUpgradeMissionSeconds(UpgradeAxis axis)
        {
            const int axisCount = 3;
            var step = (UpgradeMissionMaximumSeconds -
                        UpgradeMissionMinimumSeconds) /
                       (axisCount - 1);
            return UpgradeMissionMinimumSeconds + step * (int)axis;
        }
    }
}
