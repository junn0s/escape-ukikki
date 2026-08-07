namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 현장 단서 종류다. docs/game-design-document.md §15.1을 따른다.
    /// 각 빌런 전용 미션(§13.2)은 완료 시 자기 방에 고유한 영구 흔적을 남긴다.
    /// </summary>
    public enum ClueKind
    {
        /// <summary>배양액 오염시키기 흔적: 실험실 A 환풍구의 붉은 연기</summary>
        VentRedSmoke = 0,

        /// <summary>환풍구 역류 조작 흔적: 격리실 B의 파손된 잠금장치</summary>
        BrokenQuarantineLock = 1,

        /// <summary>독성 강화 흔적: 백신실 바닥의 빈 주사기</summary>
        EmptySyringe = 2,

        /// <summary>스피커 사용 흔적: 붉은 LED</summary>
        SpeakerRedLed = 3,

        /// <summary>투약 기록 삭제 흔적: 입원실 파쇄기 옆 종이 조각</summary>
        ShreddedMedicationRecord = 4,

        /// <summary>밸브 압력 풀기 흔적: 액체 보관실 바닥의 누출 자국</summary>
        LeakedCoolant = 5,

        /// <summary>보안 카메라 선 꼬기 흔적: 중앙 보안 광장의 꺼진 CCTV 채널</summary>
        SeveredCameraFeed = 6,

        /// <summary>메인 전력선 절단 흔적: 전력 복구실의 잘린 전선 다발</summary>
        CutPowerLine = 7
    }
}
