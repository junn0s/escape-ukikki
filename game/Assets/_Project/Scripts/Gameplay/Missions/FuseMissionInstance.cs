using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    public sealed class FuseMissionInstance
    {
        public const int MinimumFuseCount = 3;
        public const int MaximumFuseCount = 5;

        private readonly int[] _requiredOrder;

        public FuseMissionInstance(IReadOnlyList<int> requiredOrder)
        {
            ValidateOrder(requiredOrder);
            _requiredOrder = new int[requiredOrder.Count];
            for (var index = 0; index < requiredOrder.Count; index++)
            {
                _requiredOrder[index] = requiredOrder[index];
            }
        }

        public MissionState State { get; private set; } = MissionState.Assigned;
        public int ProgressIndex { get; private set; }
        public IReadOnlyList<int> RequiredOrder => _requiredOrder;

        public void Begin()
        {
            if (State != MissionState.Assigned)
            {
                return;
            }

            ProgressIndex = 0;
            State = MissionState.InProgress;
        }

        public FuseMissionInputResult SubmitFuse(int fuseId)
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            if (_requiredOrder[ProgressIndex] != fuseId)
            {
                State = MissionState.Failed;
                return FuseMissionInputResult.Failed;
            }

            ProgressIndex++;
            if (ProgressIndex < _requiredOrder.Length)
            {
                return FuseMissionInputResult.Accepted;
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

        private static void ValidateOrder(IReadOnlyList<int> requiredOrder)
        {
            if (requiredOrder == null)
            {
                throw new ArgumentNullException(nameof(requiredOrder));
            }

            if (requiredOrder.Count < MinimumFuseCount || requiredOrder.Count > MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredOrder),
                    $"Fuse count must be between {MinimumFuseCount} and {MaximumFuseCount}.");
            }

            var seen = new bool[requiredOrder.Count + 1];
            for (var index = 0; index < requiredOrder.Count; index++)
            {
                var fuseId = requiredOrder[index];
                if (fuseId < 1 || fuseId > requiredOrder.Count || seen[fuseId])
                {
                    throw new ArgumentException(
                        "Fuse order must contain each fuse ID exactly once.",
                        nameof(requiredOrder));
                }

                seen[fuseId] = true;
            }
        }
    }
}
