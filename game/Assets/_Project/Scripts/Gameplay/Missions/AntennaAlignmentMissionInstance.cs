using System;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 방향키 조작을 서버에서 한 칸씩 재현해 안테나 방위와 주파수를 맞춘다.
    /// </summary>
    public sealed class AntennaAlignmentMissionInstance
    {
        public const int AzimuthAxis = 0;
        public const int FrequencyAxis = 1;

        private readonly int _gridSize;
        private readonly int _targetAzimuth;
        private readonly int _targetFrequency;

        public AntennaAlignmentMissionInstance(int gridSize, int seed)
        {
            if (gridSize < FuseMissionInstance.MinimumFuseCount ||
                gridSize > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(gridSize));
            }

            _gridSize = gridSize;
            var random = new Random(seed);
            _targetAzimuth = random.Next(gridSize);
            _targetFrequency = random.Next(gridSize);
            CurrentAzimuth = gridSize / 2;
            CurrentFrequency = gridSize / 2;

            if (_targetAzimuth == CurrentAzimuth &&
                _targetFrequency == CurrentFrequency)
            {
                _targetFrequency = (CurrentFrequency + 1) % gridSize;
            }
        }

        public MissionState State { get; private set; } =
            MissionState.Assigned;
        public int GridSize => _gridSize;
        public int CurrentAzimuth { get; private set; }
        public int CurrentFrequency { get; private set; }
        public int TargetAzimuth => _targetAzimuth;
        public int TargetFrequency => _targetFrequency;
        public int AlignedAxisCount =>
            (CurrentAzimuth == TargetAzimuth ? 1 : 0) +
            (CurrentFrequency == TargetFrequency ? 1 : 0);

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
            }
        }

        public FuseMissionInputResult AdjustAxis(int axis, int direction)
        {
            if (State != MissionState.InProgress ||
                (direction != -1 && direction != 1))
            {
                return FuseMissionInputResult.Ignored;
            }

            switch (axis)
            {
                case AzimuthAxis:
                {
                    var next = Math.Clamp(
                        CurrentAzimuth + direction,
                        0,
                        GridSize - 1);
                    if (next == CurrentAzimuth)
                    {
                        return FuseMissionInputResult.Ignored;
                    }

                    CurrentAzimuth = next;
                    return FuseMissionInputResult.Accepted;
                }
                case FrequencyAxis:
                {
                    var next = Math.Clamp(
                        CurrentFrequency + direction,
                        0,
                        GridSize - 1);
                    if (next == CurrentFrequency)
                    {
                        return FuseMissionInputResult.Ignored;
                    }

                    CurrentFrequency = next;
                    return FuseMissionInputResult.Accepted;
                }
                default:
                    return FuseMissionInputResult.Ignored;
            }
        }

        public FuseMissionInputResult LockSignal()
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            if (AlignedAxisCount < 2)
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
    }
}
