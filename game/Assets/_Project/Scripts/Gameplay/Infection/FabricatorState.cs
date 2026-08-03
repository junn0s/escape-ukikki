namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 해독제 제작기의 상태다. docs/system-design-document.md §12.1을 따른다.
    /// Idle → Producing → Ready → Idle(누군가 획득) 순환이다.
    /// </summary>
    public enum FabricatorState : byte
    {
        Idle = 0,
        Producing = 1,
        Ready = 2
    }
}
