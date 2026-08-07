using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 다이얼을 돌려 눈금을 0에 맞추는 미션의 순수 판정이다(GDD §10.2 에어록
    /// 압력 조절). 각도는 -180~180도로 감싸며, 목표(0도) 허용 오차 안에 들어오면
    /// 완료한다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class DialToZeroMissionRules
    {
        private readonly float _toleranceDegrees;

        public DialToZeroMissionRules(float toleranceDegrees)
        {
            _toleranceDegrees = toleranceDegrees;
        }

        public float CurrentAngleDegrees { get; private set; }
        public bool IsCompleted { get; private set; }

        public void Rotate(float deltaDegrees)
        {
            if (IsCompleted)
            {
                return;
            }

            CurrentAngleDegrees =
                Mathf.DeltaAngle(0f, CurrentAngleDegrees + deltaDegrees);
            if (Mathf.Abs(CurrentAngleDegrees) <= _toleranceDegrees)
            {
                IsCompleted = true;
            }
        }

        public void SetAngle(float angleDegrees)
        {
            CurrentAngleDegrees = Mathf.DeltaAngle(0f, angleDegrees);
            IsCompleted =
                Mathf.Abs(CurrentAngleDegrees) <= _toleranceDegrees;
        }

        public void Reset()
        {
            CurrentAngleDegrees = 0f;
            IsCompleted = false;
        }
    }
}
