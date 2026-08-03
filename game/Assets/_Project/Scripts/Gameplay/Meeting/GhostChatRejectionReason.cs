namespace MonkeyLab.Gameplay.Meeting
{
    public enum GhostChatRejectionReason : byte
    {
        None = 0,
        RoundNotActive = 1,
        NotGhost = 2,
        NotParticipant = 3,
        EmptyMessage = 4,
        TooFrequent = 5
    }
}
