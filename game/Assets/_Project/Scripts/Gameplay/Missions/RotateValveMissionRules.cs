namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 밸브를 정해진 방향으로 여러 바퀴 돌리는 미션의 순수 판정이다(GDD §10.2
    /// 밸브 잠그기, §13.2 밸브 압력 풀기). 목표 방향과 반대로 돌리면 누적하지
    /// 않는다 — 시계 방향 잠그기와 반시계 방향 풀기가 같은 밸브에서 서로 다른
    /// 목적을 갖기 때문이다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class RotateValveMissionRules
    {
        private readonly float _requiredDegrees;
        private readonly bool _isClockwise;

        public RotateValveMissionRules(
            float requiredTurns,
            bool isClockwise)
        {
            _requiredDegrees = requiredTurns * 360f;
            _isClockwise = isClockwise;
        }

        public float AccumulatedDegrees { get; private set; }
        public bool IsCompleted { get; private set; }

        public float GetProgressNormalized()
        {
            return _requiredDegrees > 0f
                ? UnityEngine.Mathf.Clamp01(
                    AccumulatedDegrees / _requiredDegrees)
                : 0f;
        }

        /// <summary>
        /// 델타 각도를 더한다. 목표 방향과 다른 부호는 무시한다 — 반대로 돌려도
        /// 진행이 줄어들지 않고, 그저 진행되지 않을 뿐이다.
        /// </summary>
        public bool Rotate(float deltaDegrees)
        {
            if (IsCompleted)
            {
                return false;
            }

            var signedDelta = _isClockwise ? deltaDegrees : -deltaDegrees;
            if (signedDelta <= 0f)
            {
                return false;
            }

            AccumulatedDegrees += signedDelta;
            if (AccumulatedDegrees < _requiredDegrees)
            {
                return false;
            }

            AccumulatedDegrees = _requiredDegrees;
            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            AccumulatedDegrees = 0f;
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            float accumulatedDegrees,
            bool isCompleted)
        {
            AccumulatedDegrees = accumulatedDegrees;
            IsCompleted = isCompleted;
        }
    }
}
