namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 빌런 전용 미션 누적 클리어 횟수를 몬스터 tier 효과로 변환한다(GDD §13.3).
    /// 1회 괴물 6마리, 2회 8마리, 3회 감염 40초, 4회 근접 감지 1.75m.
    /// </summary>
    public static class VillainMissionStackEffectRules
    {
        /// <summary>해당 클리어 횟수에서 적용할 개체 수 tier다(0/1/2).</summary>
        public static int GetPopulationTier(int clearCount)
        {
            return clearCount switch
            {
                <= 0 => 0,
                1 => 1,
                _ => 2
            };
        }

        /// <summary>해당 클리어 횟수에서 적용할 독성 tier다(0/1/2).</summary>
        public static int GetToxicityTier(int clearCount)
        {
            return clearCount >= 3 ? 1 : 0;
        }

        /// <summary>해당 클리어 횟수에서 적용할 근접 감지 tier다(0/1/2).</summary>
        public static int GetProximityDetectionTier(int clearCount)
        {
            return clearCount >= 4 ? 1 : 0;
        }
    }
}
