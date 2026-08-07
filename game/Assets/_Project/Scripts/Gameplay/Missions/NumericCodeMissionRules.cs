namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 화면에 표시된 숫자를 그대로 입력하는 미션의 순수 판정이다(GDD §10.2
    /// 환자 바이탈 기록). 정답을 입력하면 완료하고, 오답이면 입력만 초기화한다.
    /// 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class NumericCodeMissionRules
    {
        public string TargetCode { get; }
        public bool IsCompleted { get; private set; }

        public NumericCodeMissionRules(string targetCode)
        {
            TargetCode = targetCode ?? string.Empty;
        }

        /// <summary>입력값을 판정한다. 정답이면 완료하고 true를 반환한다.</summary>
        public bool TrySubmit(string attempt)
        {
            if (IsCompleted)
            {
                return false;
            }

            if (TargetCode != attempt)
            {
                return false;
            }

            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            IsCompleted = false;
        }
    }
}
