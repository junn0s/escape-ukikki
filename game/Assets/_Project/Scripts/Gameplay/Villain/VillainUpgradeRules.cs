namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 강화 완료 요청의 서버 검증 규칙이다.
    /// docs/system-design-document.md §11.2의 1~2단계에 해당한다.
    /// </summary>
    public static class VillainUpgradeRules
    {
        public static UpgradeRejectionReason Validate(
            PlayerRole senderRole,
            bool canUpgradeAxis,
            bool allowsUpgradeInteraction,
            bool isOccupiedByOtherPlayer)
        {
            if (senderRole != PlayerRole.Villain)
            {
                return UpgradeRejectionReason.NotVillain;
            }

            if (!allowsUpgradeInteraction)
            {
                return UpgradeRejectionReason.RoundPhaseBlocked;
            }

            if (isOccupiedByOtherPlayer)
            {
                return UpgradeRejectionReason.StationBusy;
            }

            return canUpgradeAxis
                ? UpgradeRejectionReason.None
                : UpgradeRejectionReason.AxisAtMaximum;
        }
    }
}
