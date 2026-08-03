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
        private double _pausedAtServerTime;
        private bool _isPaused;

        public double ReadyAtServerTime => _readyAtServerTime;
        public bool IsPaused => _isPaused;

        public bool IsReady(double serverTime)
        {
            return GetEffectiveTime(serverTime) >= _readyAtServerTime;
        }

        public float GetRemainingSeconds(double serverTime)
        {
            var remaining = _readyAtServerTime - GetEffectiveTime(serverTime);
            return remaining > 0d ? (float)remaining : 0f;
        }

        public void StartCooldown(double serverTime, float cooldownSeconds)
        {
            _readyAtServerTime =
                GetEffectiveTime(serverTime) + cooldownSeconds;
        }

        /// <summary>
        /// 회의 중에는 스피커 쿨타임이 정지한다(GDD §16.2).
        /// 서버 시각을 기준으로 판정하므로, 회의가 끝나면 멈춰 있던 시간만큼
        /// 완료 시각을 뒤로 밀어 남은 값을 그대로 보존한다.
        /// </summary>
        public void SetPaused(bool isPaused, double serverTime)
        {
            if (_isPaused == isPaused)
            {
                return;
            }

            _isPaused = isPaused;
            if (isPaused)
            {
                _pausedAtServerTime = serverTime;
                return;
            }

            _readyAtServerTime += serverTime - _pausedAtServerTime;
        }

        public void Reset()
        {
            _readyAtServerTime = 0d;
            _pausedAtServerTime = 0d;
            _isPaused = false;
        }

        private double GetEffectiveTime(double serverTime)
        {
            return _isPaused ? _pausedAtServerTime : serverTime;
        }
    }
}
