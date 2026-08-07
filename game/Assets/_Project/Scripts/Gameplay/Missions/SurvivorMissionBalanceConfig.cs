using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 방별 생존자 미션(GDD §10.2)의 조작값이다.
    /// docs/balance-and-telemetry.md §7.2 표의 키와 필드 이름을 맞춘다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Survivor Mission",
        fileName = "SO_SurvivorMissionBalance_Default")]
    public sealed class SurvivorMissionBalanceConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)]
        private float _vaccineDataDownloadHoldSeconds = 8f;
        [SerializeField, Min(1)]
        private int _contaminatedSyringeCount = 3;

        /// <summary>백신 데이터 다운로드 — 손을 떼면 초기화되는 누르기 시간이다.</summary>
        public float VaccineDataDownloadHoldSeconds =>
            _vaccineDataDownloadHoldSeconds;

        /// <summary>오염된 주사기 폐기 — 휴지통으로 드래그할 주사기 수다.</summary>
        public int ContaminatedSyringeCount => _contaminatedSyringeCount;
    }
}
