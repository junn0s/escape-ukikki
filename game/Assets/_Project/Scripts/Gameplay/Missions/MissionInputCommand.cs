namespace MonkeyLab.Gameplay.Missions
{
    public enum MissionInputAction : byte
    {
        FuseDrop = 0,
        BreakerLever = 1,
        CctvConnection = 2,
        SamplePlacement = 3,
        BatteryDetach = 4,
        BatteryInsert = 5,
        BatteryDrop = 6,
        PressureValveAdjusted = 7,
        PressureLock = 8,
        SecurityCircuitRotate = 9,
        SecurityCircuitTest = 10,
        AntennaAdjust = 11,
        AntennaLock = 12,
        ServerLogKey = 13
    }

    /// <summary>클라이언트가 보낸 결과가 아니라 실제 조작 한 번을 표현한다.</summary>
    public readonly struct MissionInputCommand
    {
        public MissionInputCommand(
            MissionInputAction action,
            int primaryValue,
            int secondaryValue)
        {
            Action = action;
            PrimaryValue = primaryValue;
            SecondaryValue = secondaryValue;
        }

        public MissionInputAction Action { get; }
        public int PrimaryValue { get; }
        public int SecondaryValue { get; }
    }
}
