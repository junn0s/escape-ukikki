namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실 B의 냉동고 온도 조절 미션의 순수 판정이다(GDD §10.2). 위/아래
    /// 버튼으로 온도를 목표값에 맞추고, 목표값에서 벗어나지 않은 채로
    /// 일정 시간 유지해야 완료한다. 목표값을 벗어나면 유지 시간이 0으로
    /// 초기화된다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class FreezerTemperatureMissionRules
    {
        private readonly int _targetTemperature;
        private readonly int _minTemperature;
        private readonly int _maxTemperature;

        public FreezerTemperatureMissionRules(
            int targetTemperature,
            int minTemperature,
            int maxTemperature)
        {
            _targetTemperature = targetTemperature;
            _minTemperature = minTemperature;
            _maxTemperature = maxTemperature;
            CurrentTemperature = 0;
        }

        public int CurrentTemperature { get; private set; }
        public float HeldSecondsAtTarget { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsAtTarget => CurrentTemperature == _targetTemperature;

        public float GetProgressNormalized(float requiredSeconds)
        {
            return requiredSeconds > 0f
                ? UnityEngine.Mathf.Clamp01(
                    HeldSecondsAtTarget / requiredSeconds)
                : 0f;
        }

        public void Adjust(int deltaDegrees)
        {
            if (IsCompleted || deltaDegrees == 0)
            {
                return;
            }

            CurrentTemperature = UnityEngine.Mathf.Clamp(
                CurrentTemperature + deltaDegrees,
                _minTemperature,
                _maxTemperature);
            if (!IsAtTarget)
            {
                HeldSecondsAtTarget = 0f;
            }
        }

        /// <summary>목표값을 벗어나면 유지 시간이 0으로 초기화된다.</summary>
        public bool Tick(float deltaSeconds, float requiredSeconds)
        {
            if (IsCompleted || deltaSeconds <= 0f)
            {
                return false;
            }

            if (!IsAtTarget)
            {
                HeldSecondsAtTarget = 0f;
                return false;
            }

            HeldSecondsAtTarget += deltaSeconds;
            if (HeldSecondsAtTarget < requiredSeconds)
            {
                return false;
            }

            HeldSecondsAtTarget = requiredSeconds;
            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            CurrentTemperature = 0;
            HeldSecondsAtTarget = 0f;
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            int currentTemperature,
            float heldSecondsAtTarget,
            bool isCompleted)
        {
            CurrentTemperature = currentTemperature;
            HeldSecondsAtTarget = heldSecondsAtTarget;
            IsCompleted = isCompleted;
        }
    }
}
