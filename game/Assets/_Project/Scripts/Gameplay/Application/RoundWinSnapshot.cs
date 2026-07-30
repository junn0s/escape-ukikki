namespace MonkeyLab.Gameplay.Application
{
    public readonly struct RoundWinSnapshot
    {
        public RoundWinSnapshot(
            bool isVillainExiled,
            int projectPoints,
            int projectMaximumPoints,
            int realSurvivorCount,
            float remainingRoundSeconds)
        {
            IsVillainExiled = isVillainExiled;
            ProjectPoints = projectPoints;
            ProjectMaximumPoints = projectMaximumPoints;
            RealSurvivorCount = realSurvivorCount;
            RemainingRoundSeconds = remainingRoundSeconds;
        }

        public bool IsVillainExiled { get; }
        public int ProjectPoints { get; }
        public int ProjectMaximumPoints { get; }
        public int RealSurvivorCount { get; }
        public float RemainingRoundSeconds { get; }
    }
}
