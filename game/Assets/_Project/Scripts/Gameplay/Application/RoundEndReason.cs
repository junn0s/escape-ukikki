namespace MonkeyLab.Gameplay.Application
{
    public enum RoundEndReason : byte
    {
        None = 0,
        VillainExiled = 1,
        ProjectCompleted = 2,
        AllRealSurvivorsLost = 3,
        TimeExpired = 4
    }
}
