namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>GDD §10.2의 방별 생존자 미션 22종이다.</summary>
    public enum SurvivorMissionKind : byte
    {
        VaccineDataDownload = 0,
        ContaminatedSyringeDisposal = 1,
        FreezerTemperatureAdjustment = 2,
        VaccineSampleScan = 3,
        SlideGlassCleaning = 4,
        ReagentSorting = 5,
        MicroscopeFocus = 6,
        FlaskFill = 7,
        RatCageLock = 8,
        QuarantineAWireConnect = 9,
        AirlockPressureAdjustment = 10,
        HazmatDecontamination = 11,
        QuarantineBWireConnect = 12,
        AirFilterReplacement = 13,
        IvDripAdjustment = 14,
        PatientVitalsEntry = 15,
        StorageValveLock = 16,
        WasteCompactor = 17,
        IdCardSwipe = 18,
        CctvScreenCleaning = 19,
        CircuitBreakerReset = 20,
        FuseReplacement = 21
    }
}
