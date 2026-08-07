namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 해독제 코드 발급·제작·획득 요청의 서버 검증 규칙이다.
    /// docs/system-design-document.md §12.1~§12.5과 GDD §14.2, §14.3을 따른다.
    /// </summary>
    public static class AntidoteCraftRules
    {
        /// <summary>
        /// 중앙 제어 PC 코드 발급 검증이다(SDD §12.1). 역할은 검사하지 않는다.
        /// 빌런도 위장과 완성품 선점을 위해 동일하게 발급받을 수 있다(GDD §14.3).
        /// </summary>
        public static AntidoteRejectionReason ValidateCodeIssue(
            PlayerLifeState lifeState,
            bool allowsMissionInteraction,
            bool isWithinRange)
        {
            if (!allowsMissionInteraction)
            {
                return AntidoteRejectionReason.RoundPhaseBlocked;
            }

            if (lifeState == PlayerLifeState.DeadGhost)
            {
                return AntidoteRejectionReason.NotAlive;
            }

            return isWithinRange
                ? AntidoteRejectionReason.None
                : AntidoteRejectionReason.OutOfRange;
        }

        /// <summary>
        /// 제작 시작(코드 입력 개시) 검증이다(SDD §12.3). 역할은 검사하지 않는다.
        /// </summary>
        public static AntidoteRejectionReason ValidateCraftStart(
            PlayerLifeState lifeState,
            bool hasValidCode,
            FabricatorState fabricatorState,
            bool allowsMissionInteraction,
            bool isWithinRange)
        {
            if (!allowsMissionInteraction)
            {
                return AntidoteRejectionReason.RoundPhaseBlocked;
            }

            if (lifeState == PlayerLifeState.DeadGhost)
            {
                return AntidoteRejectionReason.NotAlive;
            }

            if (!hasValidCode)
            {
                return AntidoteRejectionReason.CodeMissing;
            }

            if (fabricatorState != FabricatorState.Idle)
            {
                return AntidoteRejectionReason.FabricatorBusy;
            }

            return isWithinRange
                ? AntidoteRejectionReason.None
                : AntidoteRejectionReason.OutOfRange;
        }

        /// <summary>
        /// 완성품 획득 검증이다. 선착순 서버 판정이며 빌런도 획득할 수 있다(SDD §12.5).
        /// 유령은 해독제를 조작할 수 없다(GDD §17).
        /// </summary>
        public static AntidoteRejectionReason ValidateCollect(
            PlayerLifeState lifeState,
            FabricatorState fabricatorState,
            int carriedCount,
            int maxCarryCount,
            bool allowsMissionInteraction,
            bool isWithinRange)
        {
            if (!allowsMissionInteraction)
            {
                return AntidoteRejectionReason.RoundPhaseBlocked;
            }

            if (lifeState == PlayerLifeState.DeadGhost)
            {
                return AntidoteRejectionReason.NotAlive;
            }

            if (fabricatorState != FabricatorState.Ready)
            {
                return AntidoteRejectionReason.NothingToCollect;
            }

            if (carriedCount >= maxCarryCount)
            {
                return AntidoteRejectionReason.CarryLimitReached;
            }

            return isWithinRange
                ? AntidoteRejectionReason.None
                : AntidoteRejectionReason.OutOfRange;
        }
    }
}
