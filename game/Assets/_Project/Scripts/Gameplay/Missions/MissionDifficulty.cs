namespace MonkeyLab.Gameplay.Missions
{
    public enum MissionDifficulty : byte
    {
        Easy = 0,
        Standard = 1,
        Hard = 2
    }

    public static class MissionDifficultyRules
    {
        public static MissionDifficulty Resolve(MissionPrototypeKind kind)
        {
            return kind switch
            {
                MissionPrototypeKind.CctvReboot => MissionDifficulty.Easy,
                MissionPrototypeKind.BreakerSequence => MissionDifficulty.Hard,
                MissionPrototypeKind.FuseSequence => MissionDifficulty.Standard,
                MissionPrototypeKind.SampleSorting => MissionDifficulty.Standard,
                MissionPrototypeKind.BatteryTransport => MissionDifficulty.Hard,
                MissionPrototypeKind.PressureValves => MissionDifficulty.Hard,
                MissionPrototypeKind.SecurityCircuit => MissionDifficulty.Standard,
                MissionPrototypeKind.AntennaAlignment => MissionDifficulty.Hard,
                MissionPrototypeKind.ServerLogRecovery => MissionDifficulty.Easy,
                _ => MissionDifficulty.Standard
            };
        }
    }
}
