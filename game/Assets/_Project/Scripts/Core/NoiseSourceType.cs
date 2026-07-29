namespace MonkeyLab.Core
{
    /// <summary>
    /// 소음 발생원. docs/game-design-document.md §11.1
    /// 일반 걷기와 UI 조작은 MVP에서 소음을 만들지 않으므로 항목이 없다.
    /// </summary>
    public enum NoiseSourceType
    {
        MissionFailure = 0,
        Speaker = 1,
        DoorOrAlarm = 2,
        BatteryDrop = 3,
        QuarantineOpen = 4
    }
}
