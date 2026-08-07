namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 낡은 필터를 빼고 새 필터를 꽂는 미션의 순수 판정이다(GDD §10.2
    /// 공기 필터 교체). 반드시 낡은 필터를 먼저 뺀 뒤에만 새 필터를 꽂을 수 있다.
    /// 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class SwapFilterMissionRules
    {
        public bool IsOldFilterRemoved { get; private set; }
        public bool IsNewFilterInstalled { get; private set; }
        public bool IsCompleted => IsOldFilterRemoved && IsNewFilterInstalled;

        public bool TryRemoveOldFilter()
        {
            if (IsOldFilterRemoved)
            {
                return false;
            }

            IsOldFilterRemoved = true;
            return true;
        }

        /// <summary>낡은 필터를 먼저 빼지 않으면 거부한다.</summary>
        public bool TryInstallNewFilter()
        {
            if (!IsOldFilterRemoved || IsNewFilterInstalled)
            {
                return false;
            }

            IsNewFilterInstalled = true;
            return true;
        }

        public void Reset()
        {
            IsOldFilterRemoved = false;
            IsNewFilterInstalled = false;
        }
    }
}
