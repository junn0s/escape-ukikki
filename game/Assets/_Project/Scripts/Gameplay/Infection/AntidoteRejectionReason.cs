namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 코드 발급·제작 시작·완성품 획득 요청이 서버에서 거부된 이유다.
    /// docs/system-design-document.md §12.1~§12.5에 대응한다.
    /// </summary>
    public enum AntidoteRejectionReason : byte
    {
        None = 0,
        RoundPhaseBlocked = 1,
        NotAlive = 2,
        CodeMissing = 3,
        FabricatorBusy = 4,
        NothingToCollect = 5,
        CarryLimitReached = 6,
        OutOfRange = 7,
        WrongCode = 8,
        CodeInvalidated = 9
    }
}
