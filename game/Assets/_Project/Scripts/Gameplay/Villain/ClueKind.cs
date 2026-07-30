namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 현장 단서 종류다. docs/game-design-document.md §15.1을 따른다.
    /// </summary>
    public enum ClueKind
    {
        /// <summary>후각 강화 흔적: 실험실 환풍구의 붉은 연기</summary>
        VentRedSmoke = 0,

        /// <summary>개체 강화 흔적: 열린 격리실 문과 파손된 잠금장치</summary>
        BrokenQuarantineLock = 1,

        /// <summary>독성 강화 흔적: 백신실 바닥의 빈 주사기</summary>
        EmptySyringe = 2,

        /// <summary>스피커 사용 흔적: 붉은 LED</summary>
        SpeakerRedLed = 3
    }
}
