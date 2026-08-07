namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 전선 여러 가닥을 모두 같은 단자로 꽂는 미션의 순수 판정이다(GDD §13.2
    /// 보안 카메라 선 꼬기). 색을 맞추는 배선 복구와 달리, 색과 무관하게
    /// 지정된 하나의 '단락' 단자로 전부 몰아넣어야 완료한다. 서버에서만
    /// 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class TangleWiresMissionRules
    {
        private readonly bool[] _pluggedFlags;

        public TangleWiresMissionRules(int wireCount)
        {
            _pluggedFlags = new bool[wireCount];
        }

        public int WireCount => _pluggedFlags.Length;

        public int PluggedCount
        {
            get
            {
                var count = 0;
                foreach (var plugged in _pluggedFlags)
                {
                    if (plugged)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsCompleted => PluggedCount == WireCount;

        public bool IsPlugged(int wireIndex)
        {
            return wireIndex >= 0 && wireIndex < _pluggedFlags.Length &&
                   _pluggedFlags[wireIndex];
        }

        /// <summary>전선 하나를 단락 단자로 꽂는다. 색 구분 없이 전부 인정한다.</summary>
        public bool TryPlug(int wireIndex)
        {
            if (wireIndex < 0 || wireIndex >= _pluggedFlags.Length ||
                _pluggedFlags[wireIndex])
            {
                return false;
            }

            _pluggedFlags[wireIndex] = true;
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _pluggedFlags.Length; index++)
            {
                _pluggedFlags[index] = false;
            }
        }
    }
}
