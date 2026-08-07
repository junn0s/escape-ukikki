namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실 B의 백신 샘플 스캔 미션의 순수 판정이다(GDD §10.2). 샘플을
    /// 반드시 0번부터 순서대로 스캔해야 한다 — 순서를 벗어난 스캔은
    /// 무시하고 실패로 취급한다. 서버에서만 갱신하고 테스트에서 직접
    /// 검증한다.
    /// </summary>
    public sealed class VaccineSampleScanMissionRules
    {
        public VaccineSampleScanMissionRules(int sampleCount)
        {
            SampleCount = sampleCount;
        }

        public int SampleCount { get; }
        public int ScannedCount { get; private set; }
        public bool IsCompleted => ScannedCount == SampleCount;

        public bool IsScanned(int sampleIndex)
        {
            return sampleIndex >= 0 && sampleIndex < ScannedCount;
        }

        /// <summary>다음 순서가 아닌 샘플을 스캔하면 무시하고 false를 반환한다.</summary>
        public bool TryScan(int sampleIndex)
        {
            if (IsCompleted || sampleIndex != ScannedCount)
            {
                return false;
            }

            ScannedCount++;
            return true;
        }

        public void Reset()
        {
            ScannedCount = 0;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(int scannedCount)
        {
            ScannedCount = scannedCount;
        }
    }
}
