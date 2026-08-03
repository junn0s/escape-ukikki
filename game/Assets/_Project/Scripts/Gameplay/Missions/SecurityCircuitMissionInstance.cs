using System;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 클릭으로 회로 모듈을 90도씩 돌려 제시된 방향과 맞춘 뒤 전원을 인가한다.
    /// 서버와 클라이언트가 같은 시드로 목표 회전을 생성한다.
    /// </summary>
    public sealed class SecurityCircuitMissionInstance
    {
        private const int RotationStepCount = 4;

        private readonly int[] _targetRotations;
        private readonly int[] _currentRotations;

        public SecurityCircuitMissionInstance(int nodeCount, int seed)
        {
            if (nodeCount < FuseMissionInstance.MinimumFuseCount ||
                nodeCount > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount));
            }

            _targetRotations = new int[nodeCount];
            _currentRotations = new int[nodeCount];
            var random = new Random(seed);
            for (var index = 0; index < nodeCount; index++)
            {
                // 시작 상태와 같은 0도는 제외해 모든 모듈을 직접 조작하게 한다.
                _targetRotations[index] = random.Next(1, RotationStepCount);
            }
        }

        public MissionState State { get; private set; } =
            MissionState.Assigned;
        public int NodeCount => _targetRotations.Length;
        public int AlignedNodeCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < NodeCount; index++)
                {
                    if (_currentRotations[index] ==
                        _targetRotations[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
            }
        }

        public int GetCurrentRotation(int nodeIndex)
        {
            return IsValidNode(nodeIndex)
                ? _currentRotations[nodeIndex]
                : 0;
        }

        public int GetTargetRotation(int nodeIndex)
        {
            return IsValidNode(nodeIndex)
                ? _targetRotations[nodeIndex]
                : 0;
        }

        public FuseMissionInputResult RotateNode(
            int nodeIndex,
            int direction)
        {
            if (State != MissionState.InProgress ||
                !IsValidNode(nodeIndex) ||
                (direction != -1 && direction != 1))
            {
                return FuseMissionInputResult.Ignored;
            }

            _currentRotations[nodeIndex] =
                (_currentRotations[nodeIndex] + direction +
                 RotationStepCount) % RotationStepCount;
            return FuseMissionInputResult.Accepted;
        }

        public FuseMissionInputResult TestCircuit()
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            if (AlignedNodeCount != NodeCount)
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

        private bool IsValidNode(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < NodeCount;
        }
    }
}
