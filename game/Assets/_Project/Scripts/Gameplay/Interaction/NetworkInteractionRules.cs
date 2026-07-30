namespace MonkeyLab.Gameplay.Interaction
{
    public static class NetworkInteractionRules
    {
        public static InteractionRejectionReason Validate(
            bool isOwnedBySender,
            uint clientSequence,
            uint lastAcceptedSequence,
            bool isTargetActive,
            bool isOccupiedByOtherPlayer,
            float squaredDistanceMeters,
            float interactionRangeMeters,
            bool hasUnblockedPath)
        {
            if (!isOwnedBySender)
            {
                return InteractionRejectionReason.InvalidOwner;
            }

            if (clientSequence <= lastAcceptedSequence)
            {
                return InteractionRejectionReason.StaleSequence;
            }

            if (!isTargetActive)
            {
                return InteractionRejectionReason.TargetInactive;
            }

            if (isOccupiedByOtherPlayer)
            {
                return InteractionRejectionReason.TargetOccupied;
            }

            var safeRange = interactionRangeMeters > 0f
                ? interactionRangeMeters
                : 0f;
            if (squaredDistanceMeters > safeRange * safeRange)
            {
                return InteractionRejectionReason.OutOfRange;
            }

            return hasUnblockedPath
                ? InteractionRejectionReason.None
                : InteractionRejectionReason.PathBlocked;
        }
    }
}
