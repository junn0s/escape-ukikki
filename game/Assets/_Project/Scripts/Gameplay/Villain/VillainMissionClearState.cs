using System;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 빌런 전용 미션(GDD §13.2) 누적 클리어 횟수다. 축을 선택하지 않고,
    /// 4개 중 어떤 미션을 완료하든 동일하게 누적된다(GDD §13.3, SDD §11.1).
    /// </summary>
    public sealed class VillainMissionClearState
    {
        public const int MaximumClearCount = 4;

        public int AssignedMissionMask { get; private set; }
        public int CompletedMissionMask { get; private set; }
        public int ClearCount { get; private set; }

        public event Action<int> ClearCountChanged;

        public bool CanClearAnother => ClearCount < MaximumClearCount;

        public void Assign(VillainMissionKind[] assignedMissions)
        {
            if (assignedMissions == null ||
                assignedMissions.Length != MaximumClearCount)
            {
                throw new ArgumentException(
                    $"Exactly {MaximumClearCount} villain missions are required.",
                    nameof(assignedMissions));
            }

            var assignedMask = 0;
            for (var index = 0; index < assignedMissions.Length; index++)
            {
                var bit = GetBit(assignedMissions[index]);
                if ((assignedMask & bit) != 0)
                {
                    throw new ArgumentException(
                        "Villain missions must be unique.",
                        nameof(assignedMissions));
                }

                assignedMask |= bit;
            }

            AssignedMissionMask = assignedMask;
            CompletedMissionMask = 0;
            ClearCount = 0;
            ClearCountChanged?.Invoke(ClearCount);
        }

        public bool IsAssigned(VillainMissionKind kind)
        {
            return (AssignedMissionMask & GetBit(kind)) != 0;
        }

        public bool IsCompleted(VillainMissionKind kind)
        {
            return (CompletedMissionMask & GetBit(kind)) != 0;
        }

        public bool TryComplete(
            VillainMissionKind kind,
            out int newClearCount)
        {
            newClearCount = ClearCount;
            var bit = GetBit(kind);
            if (!CanClearAnother ||
                (AssignedMissionMask & bit) == 0 ||
                (CompletedMissionMask & bit) != 0)
            {
                return false;
            }

            CompletedMissionMask |= bit;
            ClearCount++;
            newClearCount = ClearCount;
            ClearCountChanged?.Invoke(ClearCount);
            return true;
        }

        /// <summary>이미 최대치면 올리지 않고 false를 반환한다.</summary>
        public bool TryIncrement(out int newClearCount)
        {
            newClearCount = ClearCount;
            if (!CanClearAnother)
            {
                return false;
            }

            ClearCount++;
            newClearCount = ClearCount;
            ClearCountChanged?.Invoke(ClearCount);
            return true;
        }

        public void Reset()
        {
            AssignedMissionMask = 0;
            CompletedMissionMask = 0;
            ClearCount = 0;
        }

        public void SetClearCount(int clearCount)
        {
            if (clearCount < 0 || clearCount > MaximumClearCount)
            {
                throw new ArgumentOutOfRangeException(nameof(clearCount));
            }

            if (ClearCount == clearCount)
            {
                return;
            }

            ClearCount = clearCount;
            ClearCountChanged?.Invoke(ClearCount);
        }

        private static int GetBit(VillainMissionKind kind)
        {
            var index = (int)kind;
            if (index < 0 ||
                index >= VillainMissionAssignmentService.TotalMissionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return 1 << index;
        }
    }
}
