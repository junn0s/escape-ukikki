namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 좌우 같은 색 전선을 연결하는 미션의 순수 판정이다(GDD §10.2 배선 복구).
    /// 전선마다 목표 색이 있으며, 다른 색끼리 연결하면 거부한다.
    /// 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class WireConnectMissionRules
    {
        private readonly int[] _wireColors;
        private readonly bool[] _connectedFlags;

        public WireConnectMissionRules(int[] wireColors)
        {
            _wireColors = wireColors;
            _connectedFlags = new bool[wireColors.Length];
        }

        public int WireCount => _wireColors.Length;

        public int ConnectedCount
        {
            get
            {
                var count = 0;
                foreach (var connected in _connectedFlags)
                {
                    if (connected)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsCompleted => ConnectedCount == WireCount;

        public bool IsConnected(int leftIndex)
        {
            return leftIndex >= 0 && leftIndex < _connectedFlags.Length &&
                   _connectedFlags[leftIndex];
        }

        public int GetColor(int leftIndex)
        {
            return leftIndex >= 0 && leftIndex < _wireColors.Length
                ? _wireColors[leftIndex]
                : -1;
        }

        /// <summary>
        /// 왼쪽 전선을 오른쪽 단자에 연결한다. 색이 다르거나 이미 연결됐으면
        /// 거부하고 false를 반환한다. 오른쪽 단자도 같은 색 배열에서 찾는다.
        /// </summary>
        public bool TryConnect(int leftIndex, int rightIndex)
        {
            if (leftIndex < 0 || leftIndex >= _wireColors.Length ||
                rightIndex < 0 || rightIndex >= _wireColors.Length ||
                _connectedFlags[leftIndex] ||
                _wireColors[leftIndex] != _wireColors[rightIndex])
            {
                return false;
            }

            _connectedFlags[leftIndex] = true;
            return true;
        }

        public void Reset()
        {
            for (var index = 0; index < _connectedFlags.Length; index++)
            {
                _connectedFlags[index] = false;
            }
        }
    }
}
