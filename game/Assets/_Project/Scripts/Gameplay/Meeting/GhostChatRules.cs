using MonkeyLab.Gameplay.Infection;

namespace MonkeyLab.Gameplay.Meeting
{
    /// <summary>
    /// 유령 전용 채팅의 서버 검증 규칙이다. 문자열 정리는 회의 채팅과
    /// 동일하게 유지하기 위해 <see cref="MeetingChatRules.Sanitize"/>를 쓴다.
    /// </summary>
    public static class GhostChatRules
    {
        public static GhostChatRejectionReason Validate(
            bool isRoundActive,
            PlayerLifeState senderLifeState,
            bool isRegisteredParticipant,
            string sanitizedMessage,
            double serverTime,
            double lastSentServerTime,
            float minimumIntervalSeconds)
        {
            if (!isRoundActive)
            {
                return GhostChatRejectionReason.RoundNotActive;
            }

            if (senderLifeState != PlayerLifeState.DeadGhost)
            {
                return GhostChatRejectionReason.NotGhost;
            }

            if (!isRegisteredParticipant)
            {
                return GhostChatRejectionReason.NotParticipant;
            }

            if (string.IsNullOrEmpty(sanitizedMessage))
            {
                return GhostChatRejectionReason.EmptyMessage;
            }

            if (lastSentServerTime > 0d &&
                serverTime - lastSentServerTime < minimumIntervalSeconds)
            {
                return GhostChatRejectionReason.TooFrequent;
            }

            return GhostChatRejectionReason.None;
        }
    }
}
