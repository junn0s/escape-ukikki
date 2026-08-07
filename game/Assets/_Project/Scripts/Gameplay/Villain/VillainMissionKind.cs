namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 빌런 전용 미션 6종이다(GDD §13.2). 빌런은 이 중 4개를 무작위 배정받는다.
    /// </summary>
    public enum VillainMissionKind
    {
        CultureContamination = 0, // 실험실 A — 배양액 오염시키기
        VentBackflow = 1, // 격리실 B — 환풍구 역류 조작
        MedicationRecordWipe = 2, // 입원실 — 투약 기록 삭제
        ValvePressureRelease = 3, // 액체 보관실 — 밸브 압력 풀기
        SecurityWireTangle = 4, // 중앙 보안 광장 — 보안 카메라 선 꼬기
        MainPowerLineCut = 5 // 전력 복구실 — 메인 전력선 절단
    }
}
