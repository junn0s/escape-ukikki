using System;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>움직이는 전류 표시가 안전 구간에 들어왔을 때 차단기를 올리는 미션이다.</summary>
    public sealed class BreakerTimingMissionInstance
    {
        private readonly int _stepCount;
        private readonly float _cycleSeconds;
        private readonly float _successToleranceNormalized;
        private double _stepStartedAt;

        public BreakerTimingMissionInstance(
            int stepCount,
            float cycleSeconds,
            float successToleranceNormalized)
        {
            if (stepCount < FuseMissionInstance.MinimumFuseCount ||
                stepCount > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            if (cycleSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleSeconds));
            }

            if (successToleranceNormalized <= 0f ||
                successToleranceNormalized >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(successToleranceNormalized));
            }

            _stepCount = stepCount;
            _cycleSeconds = cycleSeconds;
            _successToleranceNormalized = successToleranceNormalized;
        }

        public MissionState State { get; private set; } = MissionState.Assigned;
        public int ProgressIndex { get; private set; }
        public int StepCount => _stepCount;
        public float SuccessToleranceNormalized =>
            _successToleranceNormalized;
        public float TargetNormalized =>
            (ProgressIndex + 1f) / (_stepCount + 1f);
        public double StepStartedAt => _stepStartedAt;

        public void Begin(double currentTime)
        {
            if (State != MissionState.Assigned)
            {
                return;
            }

            ProgressIndex = 0;
            _stepStartedAt = currentTime;
            State = MissionState.InProgress;
        }

        public float GetMarkerNormalized(double currentTime)
        {
            if (State != MissionState.InProgress)
            {
                return 0f;
            }

            var elapsed = Math.Max(0d, currentTime - _stepStartedAt);
            var halfCycles = elapsed * 2d / _cycleSeconds;
            var remainder = halfCycles % 2d;
            return (float)(remainder <= 1d ? remainder : 2d - remainder);
        }

        public FuseMissionInputResult Submit(double currentTime)
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            var marker = GetMarkerNormalized(currentTime);
            if (Math.Abs(marker - TargetNormalized) >
                _successToleranceNormalized)
            {
                State = MissionState.Failed;
                return FuseMissionInputResult.Failed;
            }

            ProgressIndex++;
            if (ProgressIndex >= _stepCount)
            {
                State = MissionState.Completed;
                return FuseMissionInputResult.Completed;
            }

            _stepStartedAt = currentTime;
            return FuseMissionInputResult.Accepted;
        }

        /// <summary>
        /// 서버가 승인한 중간 단계를 소유자 UI에 맞춘다. 최종 완료 판정에는
        /// 사용하지 않고 다음 타이밍 구간의 표시 원점만 복원한다.
        /// </summary>
        public bool ApplyAuthoritativeProgress(
            int progressIndex,
            double stepStartedAt)
        {
            if (progressIndex < 0 || progressIndex >= _stepCount)
            {
                return false;
            }

            ProgressIndex = progressIndex;
            _stepStartedAt = stepStartedAt;
            State = MissionState.InProgress;
            return true;
        }

        public void Cancel()
        {
            if (State == MissionState.InProgress)
            {
                State = MissionState.Cancelled;
            }
        }
    }
}
