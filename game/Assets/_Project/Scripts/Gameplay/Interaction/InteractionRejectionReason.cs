namespace MonkeyLab.Gameplay.Interaction
{
    public enum InteractionRejectionReason : byte
    {
        None = 0,
        InvalidOwner = 1,
        StaleSequence = 2,
        TargetInactive = 3,
        TargetOccupied = 4,
        OutOfRange = 5,
        PathBlocked = 6
    }
}
