namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 스피커 사용 요청의 서버 검증 규칙이다.
    /// GDD §13.1: 빌런 전용이며 시작 보호 시간과 회의 중에는 사용할 수 없다.
    /// </summary>
    public static class SpeakerRules
    {
        public static SpeakerRejectionReason Validate(
            PlayerRole senderRole,
            bool isSenderAlive,
            bool allowsSpeakerUse,
            bool isKnownRoom,
            bool isCooldownReady)
        {
            if (senderRole != PlayerRole.Villain)
            {
                return SpeakerRejectionReason.NotVillain;
            }

            if (!isSenderAlive)
            {
                return SpeakerRejectionReason.VillainDead;
            }

            // 시작 보호 시간과 회의 중에는 탐색 단계가 아니므로 여기서 걸린다.
            if (!allowsSpeakerUse)
            {
                return SpeakerRejectionReason.RoundPhaseBlocked;
            }

            if (!isKnownRoom)
            {
                return SpeakerRejectionReason.UnknownRoom;
            }

            return isCooldownReady
                ? SpeakerRejectionReason.None
                : SpeakerRejectionReason.OnCooldown;
        }
    }
}
