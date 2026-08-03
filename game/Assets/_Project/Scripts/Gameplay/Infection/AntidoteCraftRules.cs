using MonkeyLab.Gameplay.Villain;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 해독제 제작·획득·보관 요청의 서버 검증 규칙이다.
    /// docs/system-design-document.md §12.2, §12.3과 GDD §14.3, §17을 따른다.
    /// </summary>
    public static class AntidoteCraftRules
    {
        /// <summary>
        /// 제작 시작 검증이다(SDD §12.2). 빌런은 제작을 시작할 수 없다.
        /// </summary>
        public static AntidoteRejectionReason ValidateCraftStart(
            PlayerRole senderRole,
            PlayerLifeState lifeState,
            bool hasDiscoveredRecipe,
            FabricatorState fabricatorState,
            bool allowsMissionInteraction,
            bool isWithinRange)
        {
            if (!allowsMissionInteraction)
            {
                return AntidoteRejectionReason.RoundPhaseBlocked;
            }

            if (senderRole != PlayerRole.Survivor)
            {
                return AntidoteRejectionReason.NotSurvivor;
            }

            if (lifeState == PlayerLifeState.DeadGhost)
            {
                return AntidoteRejectionReason.NotAlive;
            }

            if (!hasDiscoveredRecipe)
            {
                return AntidoteRejectionReason.RecipeMissing;
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
        /// 완성품 획득 검증이다. 선착순 서버 판정이며 빌런도 획득할 수 있다(SDD §12.2).
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

        /// <summary>
        /// 지정 보관 칸에 넣는 요청의 검증이다(GDD §14.3).
        /// 바닥 자유 드롭은 MVP에서 지원하지 않으므로 보관 칸만 대상이다.
        /// </summary>
        public static AntidoteRejectionReason ValidateStore(
            PlayerLifeState lifeState,
            int carriedCount,
            int usedSlotCount,
            int slotCapacity,
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

            if (carriedCount <= 0)
            {
                return AntidoteRejectionReason.NotCarrying;
            }

            if (usedSlotCount >= slotCapacity)
            {
                return AntidoteRejectionReason.StorageFull;
            }

            return isWithinRange
                ? AntidoteRejectionReason.None
                : AntidoteRejectionReason.OutOfRange;
        }

        /// <summary>지정 보관 칸에서 꺼내는 요청의 검증이다.</summary>
        public static AntidoteRejectionReason ValidateWithdraw(
            PlayerLifeState lifeState,
            int carriedCount,
            int maxCarryCount,
            int usedSlotCount,
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

            if (usedSlotCount <= 0)
            {
                return AntidoteRejectionReason.StorageEmpty;
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
