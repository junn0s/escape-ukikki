namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 버튼을 일정 시간 누르고 있는 미션의 순수 판정 로직이다(GDD §10.2 백신 데이터 다운로드).
    /// 손을 떼면 진행률이 0으로 초기화된다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class HoldButtonMissionRules
    {
        public float HeldSeconds { get; private set; }
        public bool IsHolding { get; private set; }
        public bool IsCompleted { get; private set; }

        public float GetProgressNormalized(float requiredSeconds)
        {
            return requiredSeconds > 0f
                ? UnityEngine.Mathf.Clamp01(HeldSeconds / requiredSeconds)
                : 0f;
        }

        public void BeginHold()
        {
            if (IsCompleted)
            {
                return;
            }

            IsHolding = true;
        }

        /// <summary>손을 떼면 진행률이 0으로 초기화된다.</summary>
        public void ReleaseHold()
        {
            if (!IsHolding)
            {
                return;
            }

            IsHolding = false;
            HeldSeconds = 0f;
        }

        public bool Tick(float deltaSeconds, float requiredSeconds)
        {
            if (!IsHolding || IsCompleted || deltaSeconds <= 0f)
            {
                return false;
            }

            HeldSeconds += deltaSeconds;
            if (HeldSeconds < requiredSeconds)
            {
                return false;
            }

            HeldSeconds = requiredSeconds;
            IsHolding = false;
            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            HeldSeconds = 0f;
            IsHolding = false;
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            float heldSeconds,
            bool isHolding,
            bool isCompleted)
        {
            HeldSeconds = heldSeconds;
            IsHolding = isHolding;
            IsCompleted = isCompleted;
        }
    }
}
