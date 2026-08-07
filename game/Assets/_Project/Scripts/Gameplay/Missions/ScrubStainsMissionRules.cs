namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 얼룩 여러 개를 각각 일정 횟수 문질러 지우는 미션의 순수 판정이다
    /// (GDD §10.2 슬라이드 글라스 닦기). 얼룩마다 목표 문지름 횟수에 도달하면 지워진다.
    /// 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class ScrubStainsMissionRules
    {
        private readonly int[] _scrubCounts;
        private readonly int _requiredScrubsPerStain;

        public ScrubStainsMissionRules(int stainCount, int requiredScrubsPerStain)
        {
            _scrubCounts = new int[stainCount];
            _requiredScrubsPerStain = requiredScrubsPerStain;
        }

        public int StainCount => _scrubCounts.Length;

        public int CleanedCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _scrubCounts.Length; index++)
                {
                    if (IsClean(index))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsCompleted => CleanedCount == StainCount;

        public bool IsClean(int stainIndex)
        {
            return stainIndex >= 0 && stainIndex < _scrubCounts.Length &&
                   _scrubCounts[stainIndex] >= _requiredScrubsPerStain;
        }

        public int GetScrubCount(int stainIndex)
        {
            return stainIndex >= 0 && stainIndex < _scrubCounts.Length
                ? _scrubCounts[stainIndex]
                : 0;
        }

        /// <summary>이미 지워진 얼룩을 다시 문지르면 무시하고 false를 반환한다.</summary>
        public bool TryScrub(int stainIndex)
        {
            if (stainIndex < 0 || stainIndex >= _scrubCounts.Length ||
                IsClean(stainIndex))
            {
                return false;
            }

            _scrubCounts[stainIndex]++;
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _scrubCounts.Length; index++)
            {
                _scrubCounts[index] = 0;
            }
        }
    }
}
