namespace MonkeyLab.Core
{
    /// <summary>
    /// 괴물 AI 상태. docs/system-design-document.md §10.1
    /// M1에서는 Patrol, RoomIdle, InvestigateNoise, Chase, Bite, Search까지 구현하고
    /// RecoverPath는 끼임 복구와 함께 M3에서 다룬다.
    /// </summary>
    public enum MonsterState
    {
        Patrol = 0,
        RoomIdle = 1,
        InvestigateNoise = 2,
        Chase = 3,
        Bite = 4,
        Search = 5,
        RecoverPath = 6
    }
}
