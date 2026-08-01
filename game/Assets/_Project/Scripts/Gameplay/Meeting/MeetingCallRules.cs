namespace MonkeyLab.Gameplay.Meeting
{
    /// <summary>
    /// 회의 호출 검증이다. docs/system-design-document.md §15.1의 순서를 따른다.
    /// 시간 조건은 모두 탐색 경과 시간(회의 중 정지) 기준이다.
    /// </summary>
    public static class MeetingCallRules
    {
        public static MeetingRejectionReason Validate(
            bool isExploring,
            bool isRoundEnded,
            bool isCallerAlive,
            float elapsedExplorationSeconds,
            float firstMeetingLockSeconds,
            float secondsSinceLastMeeting,
            float cooldownSeconds,
            int usedMeetingCount,
            int maximumMeetingCount)
        {
            if (isRoundEnded)
            {
                return MeetingRejectionReason.RoundAlreadyEnded;
            }

            if (!isExploring)
            {
                return MeetingRejectionReason.NotExploring;
            }

            if (!isCallerAlive)
            {
                return MeetingRejectionReason.CallerDead;
            }

            if (elapsedExplorationSeconds < firstMeetingLockSeconds)
            {
                return MeetingRejectionReason.FirstMeetingLocked;
            }

            if (usedMeetingCount >= maximumMeetingCount)
            {
                return MeetingRejectionReason.MeetingLimitReached;
            }

            // 첫 회의 전에는 쿨타임이 없다.
            if (usedMeetingCount > 0 &&
                secondsSinceLastMeeting < cooldownSeconds)
            {
                return MeetingRejectionReason.OnCooldown;
            }

            return MeetingRejectionReason.None;
        }
    }
}
