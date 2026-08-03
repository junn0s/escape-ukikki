namespace MonkeyLab.Gameplay.Infection
{
    public static class AntidoteInteractionFeedback
    {
        public static string ToPrompt(AntidoteRejectionReason reason)
        {
            return reason switch
            {
                AntidoteRejectionReason.RoundPhaseBlocked =>
                    "지금은 사용할 수 없음",
                AntidoteRejectionReason.NotSurvivor =>
                    "생존자만 제작 가능",
                AntidoteRejectionReason.NotAlive =>
                    "유령은 사용할 수 없음",
                AntidoteRejectionReason.RecipeMissing =>
                    "개인 레시피가 필요함",
                AntidoteRejectionReason.FabricatorBusy =>
                    "이미 제작 중",
                AntidoteRejectionReason.NothingToCollect =>
                    "완성된 해독제가 없음",
                AntidoteRejectionReason.CarryLimitReached =>
                    "이미 해독제를 소지 중",
                AntidoteRejectionReason.NotCarrying =>
                    "보관할 해독제가 없음",
                AntidoteRejectionReason.StorageFull =>
                    "보관함이 가득 참",
                AntidoteRejectionReason.StorageEmpty =>
                    "보관함이 비어 있음",
                AntidoteRejectionReason.OutOfRange =>
                    "너무 멀리 떨어져 있음",
                _ => string.Empty
            };
        }
    }
}
