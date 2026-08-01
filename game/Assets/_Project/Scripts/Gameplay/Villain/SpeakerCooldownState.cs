namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 스피커 공용 쿨타임이다. 방마다 따로 두지 않고 리모컨 하나에 하나만 둔다
    /// (GDD §13.1: 초기 쿨타임은 45초다).
    /// 서버 시각을 받아 판정하므로 클라이언트 시계에 의존하지 않는다.
    /// </summary>
    public sealed class SpeakerCooldownState
    {
        private double _readyAtServerTime;

        public double ReadyAtServerTime => _readyAtServerTime;

        public bool IsReady(double serverTime)
        {
            return serverTime >= _readyAtServerTime;
        }

        public float GetRemainingSeconds(double serverTime)
        {
            var remaining = _readyAtServerTime - serverTime;
            return remaining > 0d ? (float)remaining : 0f;
        }

        public void StartCooldown(double serverTime, float cooldownSeconds)
        {
            _readyAtServerTime = serverTime + cooldownSeconds;
        }

        public void Reset()
        {
            _readyAtServerTime = 0d;
        }
    }
}
