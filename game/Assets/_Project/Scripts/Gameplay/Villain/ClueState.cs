namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 단서 상태다. docs/system-design-document.md §14.1을 따른다.
    /// 조사 여부는 결과 통계용이며, 조사하지 않아도 단서는 보인다.
    /// </summary>
    public enum ClueState
    {
        /// <summary>생성 전</summary>
        Inactive = 0,

        /// <summary>월드에 존재하지만 아무도 조사하지 않음</summary>
        ActiveUninspected = 1,

        /// <summary>한 명 이상이 조사함</summary>
        ActiveInspected = 2
    }
}
