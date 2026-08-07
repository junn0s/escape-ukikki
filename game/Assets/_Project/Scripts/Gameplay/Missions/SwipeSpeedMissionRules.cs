namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 카드를 적정 속도로 긁는 미션의 순수 판정이다(GDD §10.2 ID 카드 긁기).
    /// 드래그에 걸린 시간이 너무 빠르거나 느리면 실패한다. 서버에서만
    /// 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class SwipeSpeedMissionRules
    {
        private readonly float _minDurationSeconds;
        private readonly float _maxDurationSeconds;

        public SwipeSpeedMissionRules(
            float minDurationSeconds,
            float maxDurationSeconds)
        {
            _minDurationSeconds = minDurationSeconds;
            _maxDurationSeconds = maxDurationSeconds;
        }

        public bool IsCompleted { get; private set; }
        public int FailedAttemptCount { get; private set; }

        /// <summary>드래그 시작부터 끝까지 걸린 시간으로 판정한다.</summary>
        public bool TrySwipe(float durationSeconds)
        {
            if (IsCompleted)
            {
                return false;
            }

            if (durationSeconds < _minDurationSeconds ||
                durationSeconds > _maxDurationSeconds)
            {
                FailedAttemptCount++;
                return false;
            }

            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            IsCompleted = false;
            FailedAttemptCount = 0;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            bool isCompleted,
            int failedAttemptCount)
        {
            IsCompleted = isCompleted;
            FailedAttemptCount = failedAttemptCount;
        }
    }
}
