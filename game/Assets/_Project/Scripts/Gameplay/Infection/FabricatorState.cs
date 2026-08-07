namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 해독제 제작대의 상태다. docs/system-design-document.md §12.2를 따른다.
    /// Idle → AwaitingCode(코드 입력) → Synthesizing(합성) → Ready → Idle(누군가 획득) 순환이다.
    /// </summary>
    public enum FabricatorState : byte
    {
        Idle = 0,
        AwaitingCode = 1,
        Synthesizing = 2,
        Ready = 3
    }
}
