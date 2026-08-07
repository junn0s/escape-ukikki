using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 왕복하는 슬라이더를 목표 구간에서 멈추는 미션의 순수 판정이다(GDD §10.2
    /// 수액 속도 조절). 0~1 사이를 왕복하는 위치가 목표 구간 안에서 멈춰야
    /// 완료한다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class TimingStopMissionRules
    {
        private readonly float _targetMinNormalized;
        private readonly float _targetMaxNormalized;
        private readonly float _cycleSeconds;

        public TimingStopMissionRules(
            float targetMinNormalized,
            float targetMaxNormalized,
            float cycleSeconds)
        {
            _targetMinNormalized = targetMinNormalized;
            _targetMaxNormalized = targetMaxNormalized;
            _cycleSeconds = cycleSeconds;
        }

        public bool IsCompleted { get; private set; }

        /// <summary>
        /// 주어진 시각에서 왕복 위치(0~1)를 계산한다. <c>cycleSeconds</c>는
        /// 0→1→0으로 돌아오는 전체 왕복 시간이다. <c>Mathf.PingPong</c>의
        /// 주기는 길이의 2배이므로 절반 값을 넘긴다.
        /// </summary>
        public float GetPositionNormalized(float elapsedSeconds)
        {
            if (_cycleSeconds <= 0f)
            {
                return 0f;
            }

            var halfCycle = _cycleSeconds * 0.5f;
            var phase = Mathf.PingPong(elapsedSeconds, halfCycle);
            return phase / halfCycle;
        }

        /// <summary>주어진 시각에 정지 요청이 들어오면 목표 구간 여부를 판정한다.</summary>
        public bool TryStop(float elapsedSeconds)
        {
            if (IsCompleted)
            {
                return false;
            }

            var position = GetPositionNormalized(elapsedSeconds);
            if (position < _targetMinNormalized ||
                position > _targetMaxNormalized)
            {
                return false;
            }

            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(bool isCompleted)
        {
            IsCompleted = isCompleted;
        }
    }
}
