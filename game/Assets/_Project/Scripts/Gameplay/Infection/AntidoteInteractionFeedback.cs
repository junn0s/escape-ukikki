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
                AntidoteRejectionReason.NotAlive =>
                    "유령은 사용할 수 없음",
                AntidoteRejectionReason.CodeMissing =>
                    "배합 코드가 필요함",
                AntidoteRejectionReason.FabricatorBusy =>
                    "이미 배합 중",
                AntidoteRejectionReason.NothingToCollect =>
                    "완성된 해독제가 없음",
                AntidoteRejectionReason.CarryLimitReached =>
                    "이미 해독제를 소지 중",
                AntidoteRejectionReason.OutOfRange =>
                    "너무 멀리 떨어져 있음",
                AntidoteRejectionReason.WrongCode =>
                    "코드가 틀림",
                AntidoteRejectionReason.CodeInvalidated =>
                    "코드가 무효화됨 — PC에서 다시 발급받으세요",
                _ => string.Empty
            };
        }
    }
}
