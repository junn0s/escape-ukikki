using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 슬라이더를 밀어 올려 목표 구간에 고정하는 미션의 순수 판정이다(GDD §10.2
    /// 현미경 렌즈 초점). 왕복하는 수액 속도 조절과 달리, 한 방향으로만 밀어
    /// 올리다가 목표 구간 안에서 확정 요청을 보내면 완료한다. 서버에서만
    /// 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class SliderToRangeMissionRules
    {
        private readonly float _targetMinNormalized;
        private readonly float _targetMaxNormalized;

        public SliderToRangeMissionRules(
            float targetMinNormalized,
            float targetMaxNormalized)
        {
            _targetMinNormalized = targetMinNormalized;
            _targetMaxNormalized = targetMaxNormalized;
        }

        public float PositionNormalized { get; private set; }
        public bool IsCompleted { get; private set; }

        /// <summary>슬라이더를 델타만큼 밀어 올린다. 0~1로 고정한다.</summary>
        public void Push(float deltaNormalized)
        {
            if (IsCompleted || deltaNormalized <= 0f)
            {
                return;
            }

            PositionNormalized =
                Mathf.Clamp01(PositionNormalized + deltaNormalized);
        }

        /// <summary>현재 위치가 목표 구간 안이면 확정하고 완료한다.</summary>
        public bool TryConfirm()
        {
            if (IsCompleted)
            {
                return false;
            }

            if (PositionNormalized < _targetMinNormalized ||
                PositionNormalized > _targetMaxNormalized)
            {
                return false;
            }

            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            PositionNormalized = 0f;
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            float positionNormalized,
            bool isCompleted)
        {
            PositionNormalized = positionNormalized;
            IsCompleted = isCompleted;
        }
    }
}
