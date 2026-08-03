using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 두 밸브의 개방도로 압력을 만들고 안전 구간을 유지한 뒤 잠그는 미션이다.
    /// </summary>
    public sealed class PressureValveMissionInstance
    {
        public const int ValveCount = 2;

        private readonly float[] _valveOpenings = new float[ValveCount];
        private readonly float _targetPressureNormalized;
        private readonly float _toleranceNormalized;
        private readonly float _stabilizeSeconds;
        private double _stableStartedAt = double.NaN;

        public PressureValveMissionInstance(
            float targetPressureNormalized,
            float toleranceNormalized,
            float stabilizeSeconds)
        {
            if (targetPressureNormalized <= 0f ||
                targetPressureNormalized >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetPressureNormalized));
            }

            if (toleranceNormalized <= 0f ||
                toleranceNormalized >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(toleranceNormalized));
            }

            if (stabilizeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stabilizeSeconds));
            }

            _targetPressureNormalized = targetPressureNormalized;
            _toleranceNormalized = toleranceNormalized;
            _stabilizeSeconds = stabilizeSeconds;
        }

        public MissionState State { get; private set; } =
            MissionState.Assigned;
        public float TargetPressureNormalized =>
            _targetPressureNormalized;
        public float ToleranceNormalized => _toleranceNormalized;
        public float PressureNormalized =>
            (_valveOpenings[0] + _valveOpenings[1]) * 0.5f;

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
            }
        }

        public float GetValveOpening(int valveIndex)
        {
            return valveIndex >= 0 && valveIndex < ValveCount
                ? _valveOpenings[valveIndex]
                : 0f;
        }

        public float GetStabilityProgress(double currentTime)
        {
            if (!IsInSafeRange() || double.IsNaN(_stableStartedAt))
            {
                return 0f;
            }

            if (_stabilizeSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(
                (float)((currentTime - _stableStartedAt) /
                        _stabilizeSeconds));
        }

        public FuseMissionInputResult SetValveOpening(
            int valveIndex,
            float openingNormalized,
            double currentTime)
        {
            if (State != MissionState.InProgress || valveIndex < 0 ||
                valveIndex >= ValveCount || openingNormalized < 0f ||
                openingNormalized > 1f)
            {
                return FuseMissionInputResult.Ignored;
            }

            var wasSafe = IsInSafeRange();
            _valveOpenings[valveIndex] = openingNormalized;
            var isSafe = IsInSafeRange();
            if (!isSafe)
            {
                _stableStartedAt = double.NaN;
            }
            else if (!wasSafe || double.IsNaN(_stableStartedAt))
            {
                _stableStartedAt = currentTime;
            }

            return FuseMissionInputResult.Accepted;
        }

        public FuseMissionInputResult LockPressure(double currentTime)
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            if (GetStabilityProgress(currentTime) < 1f)
            {
                State = MissionState.Failed;
                return FuseMissionInputResult.Failed;
            }

            State = MissionState.Completed;
            return FuseMissionInputResult.Completed;
        }

        public void Cancel()
        {
            if (State == MissionState.InProgress)
            {
                State = MissionState.Cancelled;
            }
        }

        private bool IsInSafeRange()
        {
            return Mathf.Abs(
                       PressureNormalized - _targetPressureNormalized) <=
                   _toleranceNormalized;
        }
    }
}
