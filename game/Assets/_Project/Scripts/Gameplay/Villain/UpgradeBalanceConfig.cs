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

        public string Id => _id;

        public float UpgradeMissionMinimumSeconds =>
            Mathf.Max(0.1f, _upgradeMissionMinimumSeconds);

        public float UpgradeMissionMaximumSeconds =>
            Mathf.Max(
                UpgradeMissionMinimumSeconds,
                _upgradeMissionMaximumSeconds);

        public float MonsterSpawnWarningSeconds =>
            Mathf.Max(0f, _monsterSpawnWarningSeconds);

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
