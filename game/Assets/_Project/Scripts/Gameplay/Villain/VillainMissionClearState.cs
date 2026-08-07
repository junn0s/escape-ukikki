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

        public int ClearCount { get; private set; }

        public event Action<int> ClearCountChanged;

        public bool CanClearAnother => ClearCount < MaximumClearCount;

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
    }
}
