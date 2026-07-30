using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    public sealed class RoundStateMachine
    {
        private readonly RoundBalanceConfig _config;

        public RoundStateMachine(RoundBalanceConfig config)
        {
            _config = config != null
                ? config
                : throw new ArgumentNullException(nameof(config));
            Reset();
        }

        public RoundPhase Phase { get; private set; }
        public RoundOutcome Outcome { get; private set; }
        public RoundEndReason EndReason { get; private set; }
        public float RemainingPhaseSeconds { get; private set; }
        public float RemainingRoundSeconds { get; private set; }
        public bool HasEnded => Phase == RoundPhase.RoundResult;

        public void Reset()
        {
            Phase = RoundPhase.RoleReveal;
            Outcome = RoundOutcome.None;
            EndReason = RoundEndReason.None;
            RemainingPhaseSeconds = _config.RoleRevealSeconds;
            RemainingRoundSeconds = _config.ExplorationDurationSeconds;
        }

        public bool Tick(float deltaTime, RoundWinSnapshot snapshot)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (HasEnded)
            {
                return false;
            }

            var previousPhase = Phase;
            switch (Phase)
            {
                case RoundPhase.RoleReveal:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    if (RemainingPhaseSeconds <= 0f)
                    {
                        EnterGracePeriod();
                    }
                    break;
                case RoundPhase.GracePeriod:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    if (RemainingPhaseSeconds <= 0f)
                    {
                        EnterExploration();
                    }
                    break;
                case RoundPhase.Exploration:
                    RemainingRoundSeconds =
                        Mathf.Max(0f, RemainingRoundSeconds - deltaTime);
                    RemainingPhaseSeconds = RemainingRoundSeconds;
                    var currentSnapshot = new RoundWinSnapshot(
                        snapshot.IsVillainExiled,
                        snapshot.ProjectPoints,
                        snapshot.ProjectMaximumPoints,
                        snapshot.RealSurvivorCount,
                        RemainingRoundSeconds);
                    if (RoundWinConditionService.TryResolve(
                            currentSnapshot,
                            out var outcome,
                            out var reason))
                    {
                        End(outcome, reason);
                    }
                    break;
            }

            return previousPhase != Phase;
        }

        public bool EvaluateWinConditions(RoundWinSnapshot snapshot)
        {
            if (Phase != RoundPhase.Exploration ||
                !RoundWinConditionService.TryResolve(
                    snapshot,
                    out var outcome,
                    out var reason))
            {
                return false;
            }

            End(outcome, reason);
            return true;
        }

        public void SkipToExplorationForDevelopment()
        {
            if (HasEnded)
            {
                return;
            }

            EnterExploration();
        }

        public void SetRemainingRoundSecondsForDevelopment(float seconds)
        {
            RemainingRoundSeconds = Mathf.Clamp(
                seconds,
                0f,
                _config.ExplorationDurationSeconds);
            if (Phase == RoundPhase.Exploration)
            {
                RemainingPhaseSeconds = RemainingRoundSeconds;
            }
        }

        private void EnterGracePeriod()
        {
            Phase = RoundPhase.GracePeriod;
            RemainingPhaseSeconds = _config.InitialGracePeriodSeconds;
        }

        private void EnterExploration()
        {
            Phase = RoundPhase.Exploration;
            RemainingRoundSeconds = _config.ExplorationDurationSeconds;
            RemainingPhaseSeconds = RemainingRoundSeconds;
        }

        private void End(RoundOutcome outcome, RoundEndReason reason)
        {
            Phase = RoundPhase.RoundResult;
            Outcome = outcome;
            EndReason = reason;
            RemainingPhaseSeconds = _config.ResultDisplaySeconds;
        }
    }
}
