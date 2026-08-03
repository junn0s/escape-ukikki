namespace MonkeyLab.Gameplay.Meeting
{
    /// <summary>
    /// 토론 채팅 전송이 서버에서 거부된 이유다.
    /// docs/system-design-document.md §11.5의 "채팅 채널을 회의 참가자로 제한한다"에 대응한다.
    /// </summary>
    public enum ChatRejectionReason : byte
    {
        None = 0,

        /// <summary>토론 단계가 아니다. 탐색 중 일반 채팅은 MVP 범위가 아니다(GDD §16.2).</summary>
        NotDiscussionPhase = 1,

        /// <summary>유령과 퇴출자는 살아 있는 플레이어와 대화할 수 없다(GDD §17).</summary>
        NotAlive = 2,

        /// <summary>회의 참가자로 등록되지 않았다.</summary>
        NotParticipant = 3,

        /// <summary>정리 후 남은 내용이 없다.</summary>
        EmptyMessage = 4,

        /// <summary>연속 전송 최소 간격을 지키지 않았다.</summary>
        TooFrequent = 5
    }
}
