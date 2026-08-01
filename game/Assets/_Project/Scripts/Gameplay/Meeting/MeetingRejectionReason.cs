namespace MonkeyLab.Gameplay.Meeting
{
    public enum MeetingRejectionReason
    {
        None = 0,
        NotExploring = 1,
        CallerDead = 2,
        FirstMeetingLocked = 3,
        OnCooldown = 4,
        MeetingLimitReached = 5,
        RoundAlreadyEnded = 6
    }
}
