namespace MonkeyLab.Core
{
    /// <summary>
    /// 플레이어 한 명의 감염 상태. 순수 로직이라 Unity 없이 테스트한다.
    ///
    /// 규칙 (docs/game-design-document.md §14.1):
    /// - 물린 시점의 독성 강화 단계로 제한시간을 고정한다.
    /// - 이미 감염된 동안 다시 물려도 타이머를 추가하거나 초기화하지 않는다.
    /// - 해독 후에는 다시 물려 감염될 수 있다.
    /// - 회의 중에는 타이머가 정지한다 (Tick을 호출하지 않는 쪽에서 처리).
    /// </summary>
    public sealed class InfectionState
    {
        private bool _isInfected;
        private float _remainingSeconds;

        public bool IsInfected => _isInfected;

        /// <summary>감염 중이 아니면 0.</summary>
        public float RemainingSeconds => _isInfected ? _remainingSeconds : 0f;

        /// <summary>타이머가 0에 도달해 사망 처리된 적이 있는지.</summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// 물렸을 때 호출한다. 이미 감염 중이면 아무 일도 일어나지 않는다.
        /// </summary>
        /// <returns>이번 호출로 새 감염이 시작됐으면 true</returns>
        public bool TryInfect(float durationSeconds)
        {
            if (_isInfected || IsDead)
            {
                return false;
            }

            if (durationSeconds <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(durationSeconds), durationSeconds, "감염 제한시간은 0보다 커야 한다.");
            }

            _isInfected = true;
            _remainingSeconds = durationSeconds;
            return true;
        }

        /// <summary>
        /// 감염 타이머를 진행한다. 회의 중에는 호출하지 않는다.
        /// </summary>
        /// <returns>이번 호출로 사망했으면 true</returns>
        public bool Tick(float deltaSeconds)
        {
            if (!_isInfected || IsDead)
            {
                return false;
            }

            _remainingSeconds -= deltaSeconds;

            if (_remainingSeconds > 0f)
            {
                return false;
            }

            _remainingSeconds = 0f;
            _isInfected = false;
            IsDead = true;
            return true;
        }

        /// <summary>
        /// 해독제를 사용해 감염을 해제한다.
        /// </summary>
        /// <returns>실제로 치료됐으면 true</returns>
        public bool TryCure()
        {
            if (!_isInfected || IsDead)
            {
                return false;
            }

            _isInfected = false;
            _remainingSeconds = 0f;
            return true;
        }
    }
}
