using System;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 강화 3축의 단계를 보관하는 순수 상태다.
    /// docs/system-design-document.md §11.1에 따라 각 축을 0~2 정수로 저장한다.
    /// </summary>
    public sealed class VillainUpgradeState
    {
        public const int MinimumLevel = 0;
        public const int MaximumLevel = 2;

        private readonly int[] _levels =
            new int[Enum.GetValues(typeof(UpgradeAxis)).Length];

        public event Action<UpgradeAxis, int> AxisLevelChanged;

        public int ScentLevel => GetLevel(UpgradeAxis.Scent);
        public int PopulationLevel => GetLevel(UpgradeAxis.Population);
        public int ToxicityLevel => GetLevel(UpgradeAxis.Toxicity);

        public int TotalUpgradeCount =>
            _levels[0] + _levels[1] + _levels[2];

        public int GetLevel(UpgradeAxis axis)
        {
            return _levels[ToIndex(axis)];
        }

        public bool CanUpgrade(UpgradeAxis axis)
        {
            return GetLevel(axis) < MaximumLevel;
        }

        /// <summary>
        /// 해당 축을 한 단계 올린다. 이미 최대 단계면 올리지 않고 false를 반환한다.
        /// </summary>
        public bool TryUpgrade(UpgradeAxis axis, out int newLevel)
        {
            var index = ToIndex(axis);
            if (_levels[index] >= MaximumLevel)
            {
                newLevel = _levels[index];
                return false;
            }

            _levels[index]++;
            newLevel = _levels[index];
            AxisLevelChanged?.Invoke(axis, newLevel);
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _levels.Length; index++)
            {
                _levels[index] = MinimumLevel;
            }
        }

        public void SetLevel(UpgradeAxis axis, int level)
        {
            if (level < MinimumLevel || level > MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            var index = ToIndex(axis);
            if (_levels[index] == level)
            {
                return;
            }

            _levels[index] = level;
            AxisLevelChanged?.Invoke(axis, level);
        }

        private static int ToIndex(UpgradeAxis axis)
        {
            if (!Enum.IsDefined(typeof(UpgradeAxis), axis))
            {
                throw new ArgumentOutOfRangeException(nameof(axis));
            }

            return (int)axis;
        }
    }
}
