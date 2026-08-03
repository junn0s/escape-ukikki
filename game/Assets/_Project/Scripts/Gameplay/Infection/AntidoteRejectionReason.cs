namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 제작 시작·완성품 획득·보관 요청이 서버에서 거부된 이유다.
    /// docs/system-design-document.md §12.2와 §12.3에 대응한다.
    /// </summary>
    public enum AntidoteRejectionReason : byte
    {
        None = 0,
        RoundPhaseBlocked = 1,
        NotSurvivor = 2,
        NotAlive = 3,
        RecipeMissing = 4,
        FabricatorBusy = 5,
        NothingToCollect = 6,
        CarryLimitReached = 7,
        NotCarrying = 8,
        StorageFull = 9,
        StorageEmpty = 10,
        OutOfRange = 11
    }
}
