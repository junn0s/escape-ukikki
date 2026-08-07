namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 버튼을 누르면 정해진 시간 동안 진행되는 일회성 미션의 순수 판정이다
    /// (GDD §10.2 방호복 소독). 시작하면 중단 없이 끝까지 진행되며, 그동안
    /// 화면을 가리는 연출은 UI 레이어가 담당한다. 서버에서만 갱신하고
    /// 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class TimedBlindMissionRules
    {
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public float ElapsedSeconds { get; private set; }

        public float GetProgressNormalized(float durationSeconds)
        {
            return durationSeconds > 0f
                ? UnityEngine.Mathf.Clamp01(ElapsedSeconds / durationSeconds)
                : 0f;
        }

        public bool TryStart()
        {
            if (IsRunning || IsCompleted)
            {
                return false;
            }

            IsRunning = true;
            return true;
        }

        /// <summary>진행 중에만 시간을 더한다. 완료 시 true를 반환한다.</summary>
        public bool Tick(float deltaSeconds, float durationSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f)
            {
                return false;
            }

            ElapsedSeconds += deltaSeconds;
            if (ElapsedSeconds < durationSeconds)
            {
                return false;
            }

            ElapsedSeconds = durationSeconds;
            IsRunning = false;
            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            IsRunning = false;
            IsCompleted = false;
            ElapsedSeconds = 0f;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            float elapsedSeconds,
            bool isRunning,
            bool isCompleted)
        {
            ElapsedSeconds = elapsedSeconds;
            IsRunning = isRunning;
            IsCompleted = isCompleted;
        }
    }
}
