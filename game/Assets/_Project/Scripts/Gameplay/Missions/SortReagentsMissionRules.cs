namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 색상별 시약병을 같은 색 칸으로 분류하는 미션의 순수 판정이다
    /// (GDD §10.2 시약병 분류). 시약병마다 정해진 목표 칸이 있으며, 다른 칸에 놓으면
    /// 거부하고 목표 칸에 놓아야만 인정한다. 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class SortReagentsMissionRules
    {
        private readonly int[] _targetBinIndices;
        private readonly bool[] _sortedFlags;

        public SortReagentsMissionRules(int[] targetBinIndices)
        {
            _targetBinIndices = targetBinIndices;
            _sortedFlags = new bool[targetBinIndices.Length];
        }

        public int ReagentCount => _targetBinIndices.Length;

        public int SortedCount
        {
            get
            {
                var count = 0;
                foreach (var sorted in _sortedFlags)
                {
                    if (sorted)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsCompleted => SortedCount == ReagentCount;

        public bool IsSorted(int reagentIndex)
        {
            return reagentIndex >= 0 && reagentIndex < _sortedFlags.Length &&
                   _sortedFlags[reagentIndex];
        }

        public int GetTargetBinIndex(int reagentIndex)
        {
            return reagentIndex >= 0 && reagentIndex < _targetBinIndices.Length
                ? _targetBinIndices[reagentIndex]
                : -1;
        }

        /// <summary>
        /// 목표 칸과 다르면 거부하고 false를 반환한다. 이미 분류된 시약병도 거부한다.
        /// </summary>
        public bool TrySort(int reagentIndex, int binIndex)
        {
            if (reagentIndex < 0 || reagentIndex >= _sortedFlags.Length ||
                _sortedFlags[reagentIndex] ||
                _targetBinIndices[reagentIndex] != binIndex)
            {
                return false;
            }

            _sortedFlags[reagentIndex] = true;
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _sortedFlags.Length; index++)
            {
                _sortedFlags[index] = false;
            }
        }
    }
}
