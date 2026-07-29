namespace MonkeyLab.Gameplay.Monsters
{
    public static class MonsterAggroRules
    {
        public static bool ShouldReleaseTargetAfterBite(MonsterBiteResult result)
        {
            return result == MonsterBiteResult.Hit;
        }

        public static bool ShouldSuppressTargetDetection(
            MonsterState state,
            bool isPostBiteSearch)
        {
            return state == MonsterState.Search && isPostBiteSearch;
        }

        public static bool ShouldUseCloseDetectionOnly(MonsterState state)
        {
            return state == MonsterState.InvestigateNoise;
        }
    }
}
