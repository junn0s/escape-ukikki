namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 플레이어 한 명이 발급받은 배합 코드와 누적 오입 횟수를 서버에서 관리한다
    /// (GDD §14.2, SDD §12.1, §12.4). MonoBehaviour가 아니므로 서버에서만 갱신하고
    /// 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class AntidoteCodeSession
    {
        public string Code { get; private set; } = string.Empty;
        public int FailedAttemptCount { get; private set; }
        public bool HasValidCode => !string.IsNullOrEmpty(Code);

        /// <summary>PC에서 새 코드를 발급한다. 이전 코드와 오입 횟수를 덮어쓴다.</summary>
        public void IssueCode(string code)
        {
            Code = code ?? string.Empty;
            FailedAttemptCount = 0;
        }

        /// <summary>
        /// 입력값을 판정한다. 정답이면 코드를 유지한 채 참을 반환하고,
        /// 오답이면 오입 횟수를 올린 뒤 최대치 도달 여부에 따라 코드를 무효화할 수 있다.
        /// </summary>
        public bool TrySubmit(string attempt, int maxAttempts)
        {
            if (!HasValidCode)
            {
                return false;
            }

            if (Code == attempt)
            {
                return true;
            }

            FailedAttemptCount++;
            if (FailedAttemptCount >= maxAttempts)
            {
                Invalidate();
            }

            return false;
        }

        public void Invalidate()
        {
            Code = string.Empty;
            FailedAttemptCount = 0;
        }
    }
}
