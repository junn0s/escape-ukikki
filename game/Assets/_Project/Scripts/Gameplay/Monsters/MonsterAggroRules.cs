namespace MonkeyLab.Gameplay.Monsters
{
    public static class MonsterAggroRules
    {
        public static bool ShouldReleaseTargetAfterBite(MonsterBiteResult result)
        {
            return result == MonsterBiteResult.Hit;
        }
    }
}
