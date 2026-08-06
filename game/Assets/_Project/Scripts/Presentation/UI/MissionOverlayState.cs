namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 미션 화면이 화면을 덮고 있는지 알린다.
    ///
    /// 미션 중에는 이동과 조준이 잠기므로 방 배너·조작 도움말·해독제 상태처럼
    /// 월드를 보며 읽는 정보가 필요 없다. 그대로 두면 미션 안내문 위에 겹쳐
    /// 양쪽 다 읽히지 않는다.
    ///
    /// 판정에 관여하지 않는 로컬 표현 상태이며 클라이언트마다 독립이다.
    /// uGUI로 옮길 때는 화면 스택으로 대체한다.
    /// </summary>
    public static class MissionOverlayState
    {
        public static bool IsOpen { get; private set; }

        public static void SetOpen(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }
}
