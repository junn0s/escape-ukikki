namespace MonkeyLab.Network
{
    /// <summary>
    /// 클라이언트가 화면에 그리기 위해 들고 있는 토론 채팅 한 줄이다.
    /// 서버가 살아 있는 참가자에게만 보내므로 유령 클라이언트에는 쌓이지 않는다.
    /// 원문은 텔레메트리에 남기지 않는다(docs/balance-and-telemetry.md §11).
    /// </summary>
    public readonly struct MeetingChatEntry
    {
        /// <summary>로비 슬롯 번호다. 색상과 표시 번호를 여기서 찾는다.</summary>
        public readonly byte SlotIndex;

        public readonly string Text;

        public MeetingChatEntry(byte slotIndex, string text)
        {
            SlotIndex = slotIndex;
            Text = text;
        }
    }
}
