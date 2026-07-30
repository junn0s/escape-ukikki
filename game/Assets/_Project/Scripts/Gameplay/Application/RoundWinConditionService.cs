namespace MonkeyLab.Gameplay.Application
{
    public static class RoundWinConditionService
    {
        public static bool TryResolve(
            RoundWinSnapshot snapshot,
            out RoundOutcome outcome,
            out RoundEndReason reason)
        {
            if (snapshot.IsVillainExiled)
            {
                outcome = RoundOutcome.SurvivorsWin;
                reason = RoundEndReason.VillainExiled;
                return true;
            }

            if (snapshot.ProjectMaximumPoints > 0 &&
                snapshot.ProjectPoints >= snapshot.ProjectMaximumPoints)
            {
                outcome = RoundOutcome.SurvivorsWin;
                reason = RoundEndReason.ProjectCompleted;
                return true;
            }

            if (snapshot.RealSurvivorCount <= 0)
            {
                outcome = RoundOutcome.VillainWins;
                reason = RoundEndReason.AllRealSurvivorsLost;
                return true;
            }

            if (snapshot.RemainingRoundSeconds <= 0f)
            {
                outcome = RoundOutcome.VillainWins;
                reason = RoundEndReason.TimeExpired;
                return true;
            }

            outcome = RoundOutcome.None;
            reason = RoundEndReason.None;
            return false;
        }
    }
}
