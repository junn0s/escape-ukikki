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

        /// <summary>사용한 회의 수다. 최대 3회를 넘길 수 없다.</summary>
        public int UsedMeetingCount { get; private set; }

        /// <summary>탐색 경과 시간이다. 회의 중에는 늘지 않는다.</summary>
        public float ElapsedExplorationSeconds =>
            _config.ExplorationDurationSeconds - RemainingRoundSeconds;

        /// <summary>마지막 회의 종료 이후의 탐색 경과 시간이다.</summary>
        public float SecondsSinceLastMeeting =>
            ElapsedExplorationSeconds - _explorationSecondsAtLastMeetingEnd;

        public bool IsMeetingActive =>
            Phase is RoundPhase.MeetingDiscussion or
                RoundPhase.MeetingVote or
                RoundPhase.MeetingResult;

        private float _explorationSecondsAtLastMeetingEnd;

        public void Reset()
        {
            Phase = RoundPhase.RoleReveal;
            Outcome = RoundOutcome.None;
            EndReason = RoundEndReason.None;
            RemainingPhaseSeconds = _config.RoleRevealSeconds;
            RemainingRoundSeconds = _config.ExplorationDurationSeconds;
            UsedMeetingCount = 0;
            _explorationSecondsAtLastMeetingEnd = 0f;
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
                // 보호 시간에도 미션을 수행할 수 있으므로 라운드 타이머가 함께
                // 흐른다. 보호는 시작 지점의 괴물을 피하게 해주는 것이고 시간을
                // 멈추는 장치가 아니다(GDD §6.3).
                case RoundPhase.GracePeriod:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    RemainingRoundSeconds =
                        Mathf.Max(0f, RemainingRoundSeconds - deltaTime);
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

                // 회의 단계에서는 탐색 타이머를 건드리지 않는다(GDD §16.2).
                // 단계 타이머만 흐른다.
                case RoundPhase.MeetingDiscussion:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    if (RemainingPhaseSeconds <= 0f)
                    {
                        EnterMeetingVote();
                    }
                    break;
                case RoundPhase.MeetingVote:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    if (RemainingPhaseSeconds <= 0f)
                    {
                        EnterMeetingResult();
                    }
                    break;
                case RoundPhase.MeetingResult:
                    RemainingPhaseSeconds =
                        Mathf.Max(0f, RemainingPhaseSeconds - deltaTime);
                    if (RemainingPhaseSeconds <= 0f)
                    {
                        ResumeExplorationAfterMeeting();
                    }
                    break;
            }

            return previousPhase != Phase;
        }

        /// <summary>회의를 시작한다. 서버가 호출 검증을 마친 뒤에만 부른다.</summary>
        public bool TryBeginMeeting()
        {
            if (Phase != RoundPhase.Exploration ||
                UsedMeetingCount >= _config.MaximumMeetingCount)
            {
                return false;
            }

            UsedMeetingCount++;
            Phase = RoundPhase.MeetingDiscussion;
            RemainingPhaseSeconds = _config.MeetingDiscussionSeconds;
            return true;
        }

        /// <summary>토론을 앞당겨 끝낸다. 개발 검증과 전원 준비 완료에 쓴다.</summary>
        public bool TrySkipDiscussion()
        {
            if (Phase != RoundPhase.MeetingDiscussion)
            {
                return false;
            }

            EnterMeetingVote();
            return true;
        }

        /// <summary>전원이 투표를 마치면 결과 단계로 넘어간다.</summary>
        public bool TryFinishVoteEarly()
        {
            if (Phase != RoundPhase.MeetingVote)
            {
                return false;
            }

            EnterMeetingResult();
            return true;
        }

        public bool EvaluateWinConditions(RoundWinSnapshot snapshot)
        {
            // 빌런 퇴출은 회의 결과 단계에서 확정되므로 회의 중에도 판정한다.
            if ((Phase != RoundPhase.Exploration && !IsMeetingActive) ||
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
            if (HasEnded || Phase == RoundPhase.Exploration)
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

        public void ResetMeetingCooldownForDevelopment()
        {
            if (HasEnded)
            {
                return;
            }

            var requiredElapsed = Mathf.Max(
                _config.FirstMeetingLockSeconds,
                _config.MeetingCooldownSeconds);
            if (ElapsedExplorationSeconds < requiredElapsed)
            {
                RemainingRoundSeconds = Mathf.Max(
                    0f,
                    _config.ExplorationDurationSeconds - requiredElapsed);
            }

            _explorationSecondsAtLastMeetingEnd =
                ElapsedExplorationSeconds - _config.MeetingCooldownSeconds;
            if (Phase == RoundPhase.Exploration)
            {
                RemainingPhaseSeconds = RemainingRoundSeconds;
            }
        }

        public void ForceOutcomeForDevelopment(
            RoundOutcome outcome,
            RoundEndReason reason)
        {
            if (HasEnded || outcome == RoundOutcome.None ||
                reason == RoundEndReason.None)
            {
                return;
            }

            End(outcome, reason);
        }

        private void EnterGracePeriod()
        {
            Phase = RoundPhase.GracePeriod;
            RemainingPhaseSeconds = _config.InitialGracePeriodSeconds;

            // 라운드 시계는 보호 시간부터 흐른다. 보호 중에도 미션을 하므로
            // 여기서 채워야 흘린 시간이 무효가 되지 않는다(GDD §6.3).
            RemainingRoundSeconds = _config.ExplorationDurationSeconds;
        }

        private void EnterExploration()
        {
            Phase = RoundPhase.Exploration;

            // 라운드 시계는 보호 시간에 이미 시작했다. 여기서 되채우면 보호 중
            // 흘린 시간이 사라진다.
            RemainingPhaseSeconds = RemainingRoundSeconds;
        }

        private void EnterMeetingVote()
        {
            Phase = RoundPhase.MeetingVote;
            RemainingPhaseSeconds = _config.MeetingVoteSeconds;
        }

        private void EnterMeetingResult()
        {
            Phase = RoundPhase.MeetingResult;
            RemainingPhaseSeconds = _config.MeetingResultSeconds;
        }

        private void ResumeExplorationAfterMeeting()
        {
            // 탐색 타이머는 회의 전 값을 그대로 이어간다.
            Phase = RoundPhase.Exploration;
            RemainingPhaseSeconds = RemainingRoundSeconds;
            _explorationSecondsAtLastMeetingEnd = ElapsedExplorationSeconds;
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
