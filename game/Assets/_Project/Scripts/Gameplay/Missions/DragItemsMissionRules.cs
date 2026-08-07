namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 지정된 개수의 아이템을 목표 지점으로 드래그하는 미션의 순수 판정이다
    /// (GDD §10.2 오염된 주사기 폐기). 아이템 하나당 서버가 1회만 인정한다.
    /// 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class DragItemsMissionRules
    {
        private readonly bool[] _placedFlags;

        public DragItemsMissionRules(int itemCount)
        {
            _placedFlags = new bool[itemCount];
        }

        public int ItemCount => _placedFlags.Length;
        public int PlacedCount
        {
            get
            {
                var count = 0;
                foreach (var placed in _placedFlags)
                {
                    if (placed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsCompleted => PlacedCount == ItemCount;

        public bool IsPlaced(int itemIndex)
        {
            return itemIndex >= 0 && itemIndex < _placedFlags.Length &&
                   _placedFlags[itemIndex];
        }

        /// <summary>같은 아이템을 다시 놓으면 무시하고 false를 반환한다.</summary>
        public bool TryPlaceItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _placedFlags.Length ||
                _placedFlags[itemIndex])
            {
                return false;
            }

            _placedFlags[itemIndex] = true;
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _placedFlags.Length; index++)
            {
                _placedFlags[index] = false;
            }
        }
    }
}
