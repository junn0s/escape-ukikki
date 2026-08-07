using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 버튼을 누르고 있으면 게이지가 차오르고, 목표 구간에서 손을 떼야
    /// 완료하는 미션의 순수 판정이다(GDD §10.2 플라스크 용액 채우기).
    /// 목표 구간을 벗어나 채워지면 실패로 초기화된다. 서버에서만 갱신하고
    /// 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class FillGaugeMissionRules
    {
        private readonly float _targetMinNormalized;
        private readonly float _targetMaxNormalized;
        private readonly float _fillDurationSeconds;

        public FillGaugeMissionRules(
            float targetMinNormalized,
            float targetMaxNormalized,
            float fillDurationSeconds)
        {
            _targetMinNormalized = targetMinNormalized;
            _targetMaxNormalized = targetMaxNormalized;
            _fillDurationSeconds = fillDurationSeconds;
        }

        public bool IsHolding { get; private set; }
        public float FilledSeconds { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsOverfilled { get; private set; }

        public float GetProgressNormalized()
        {
            return _fillDurationSeconds > 0f
                ? Mathf.Clamp01(FilledSeconds / _fillDurationSeconds)
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

        public void Tick(float deltaSeconds)
        {
            if (!IsHolding || IsCompleted || deltaSeconds <= 0f)
            {
                return;
            }

            FilledSeconds =
                Mathf.Min(_fillDurationSeconds, FilledSeconds + deltaSeconds);
            if (FilledSeconds >= _fillDurationSeconds)
            {
                IsOverfilled = true;
            }
        }

        /// <summary>
        /// 손을 뗀 시점의 진행률이 목표 구간 안이면 완료한다. 벗어났으면
        /// 실패로 간주해 진행률을 0으로 되돌린다.
        /// </summary>
        public bool ReleaseHold()
        {
            if (!IsHolding || IsCompleted)
            {
                return false;
            }

            IsHolding = false;
            var progress = GetProgressNormalized();
            if (progress < _targetMinNormalized ||
                progress > _targetMaxNormalized)
            {
                FilledSeconds = 0f;
                IsOverfilled = false;
                return false;
            }

            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            IsHolding = false;
            FilledSeconds = 0f;
            IsCompleted = false;
            IsOverfilled = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            float filledSeconds,
            bool isHolding,
            bool isCompleted)
        {
            FilledSeconds = filledSeconds;
            IsHolding = isHolding;
            IsCompleted = isCompleted;
        }
    }
}
