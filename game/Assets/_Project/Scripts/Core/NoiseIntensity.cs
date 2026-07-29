namespace MonkeyLab.Core
{
    /// <summary>
    /// 소음 강도. 반경은 SO_GameBalance가 가지며 여기서는 단계만 정의한다.
    /// docs/balance-and-telemetry.md §5
    /// </summary>
    public enum NoiseIntensity
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }
}
