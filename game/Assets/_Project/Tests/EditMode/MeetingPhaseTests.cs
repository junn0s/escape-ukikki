using MonkeyLab.Gameplay.Application;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 회의 단계 전이와 탐색 타이머 정지를 고정한다.
    /// docs/game-design-document.md §16.2, balance-and-telemetry.md §2 기준.
    /// </summary>
    public sealed class MeetingPhaseTests
    {
        private RoundBalanceConfig _config;
        private RoundStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            _machine = new RoundStateMachine(_config);
            // 탐색 단계까지 진행한다.
            _machine.Tick(_config.RoleRevealSeconds, CreateSnapshot());
            _machine.Tick(_config.InitialGracePeriodSeconds, CreateSnapshot());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        private RoundWinSnapshot CreateSnapshot(
            bool isVillainExiled = false,
            int survivorCount = 5)
        {
            return new RoundWinSnapshot(
                isVillainExiled,
                projectPoints: 0,
                projectMaximumPoints: _config.ProjectMaximumPoints,
                realSurvivorCount: survivorCount,
                remainingRoundSeconds: _machine.RemainingRoundSeconds);
        }

        [Test]
        public void DefaultBalance_MatchesDocumentedMeetingValues()
        {
            Assert.That(_config.FirstMeetingLockSeconds, Is.EqualTo(120f));
            Assert.That(_config.MeetingCooldownSeconds, Is.EqualTo(120f));
            Assert.That(_config.MaximumMeetingCount, Is.EqualTo(3));
            Assert.That(_config.MeetingDiscussionSeconds, Is.EqualTo(90f));
            Assert.That(_config.MeetingVoteSeconds, Is.EqualTo(30f));
            Assert.That(
                _config.PostMeetingBiteProtectionSeconds,
                Is.EqualTo(2f));
        }

        [Test]
        public void BeginMeeting_MovesToDiscussion()
        {
            Assert.That(_machine.TryBeginMeeting(), Is.True);
            Assert.That(
                _machine.Phase,
                Is.EqualTo(RoundPhase.MeetingDiscussion));
            Assert.That(
                _machine.RemainingPhaseSeconds,
                Is.EqualTo(90f));
            Assert.That(_machine.UsedMeetingCount, Is.EqualTo(1));
        }

        [Test]
        public void MeetingFreezesExplorationTimer()
        {
            _machine.Tick(100f, CreateSnapshot());
            var remainingBeforeMeeting = _machine.RemainingRoundSeconds;

            _machine.TryBeginMeeting();
            // 토론 90초 + 투표 30초 + 결과 5초를 모두 흘려보낸다.
            _machine.Tick(90f, CreateSnapshot());
            _machine.Tick(30f, CreateSnapshot());
            _machine.Tick(5f, CreateSnapshot());

            Assert.That(
                _machine.RemainingRoundSeconds,
                Is.EqualTo(remainingBeforeMeeting).Within(0.001f),
                "회의 중에는 탐색 타이머가 줄지 않아야 한다.");
        }

        [Test]
        public void MeetingRunsDiscussionThenVoteThenResult()
        {
            _machine.TryBeginMeeting();

            _machine.Tick(90f, CreateSnapshot());
            Assert.That(
                _machine.Phase,
                Is.EqualTo(RoundPhase.MeetingVote));
            Assert.That(_machine.RemainingPhaseSeconds, Is.EqualTo(30f));

            _machine.Tick(30f, CreateSnapshot());
            Assert.That(
                _machine.Phase,
                Is.EqualTo(RoundPhase.MeetingResult));

            _machine.Tick(5f, CreateSnapshot());
            Assert.That(
                _machine.Phase,
                Is.EqualTo(RoundPhase.Exploration));
        }

        [Test]
        public void CannotBeginMeetingOutsideExploration()
        {
            _machine.TryBeginMeeting();

            Assert.That(_machine.TryBeginMeeting(), Is.False);
            Assert.That(_machine.UsedMeetingCount, Is.EqualTo(1));
        }

        [Test]
        public void CannotExceedThreeMeetings()
        {
            for (var index = 0; index < 3; index++)
            {
                Assert.That(_machine.TryBeginMeeting(), Is.True);
                _machine.Tick(90f, CreateSnapshot());
                _machine.Tick(30f, CreateSnapshot());
                _machine.Tick(5f, CreateSnapshot());
            }

            Assert.That(_machine.UsedMeetingCount, Is.EqualTo(3));
            Assert.That(_machine.TryBeginMeeting(), Is.False);
        }

        [Test]
        public void SecondsSinceLastMeeting_ResetsAfterMeetingEnds()
        {
            _machine.Tick(200f, CreateSnapshot());
            _machine.TryBeginMeeting();
            _machine.Tick(90f, CreateSnapshot());
            _machine.Tick(30f, CreateSnapshot());
            _machine.Tick(5f, CreateSnapshot());

            Assert.That(
                _machine.SecondsSinceLastMeeting,
                Is.EqualTo(0f).Within(0.001f));

            _machine.Tick(60f, CreateSnapshot());
            Assert.That(
                _machine.SecondsSinceLastMeeting,
                Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void VillainExileDuringMeeting_EndsRoundImmediately()
        {
            _machine.TryBeginMeeting();
            _machine.Tick(90f, CreateSnapshot());
            _machine.Tick(30f, CreateSnapshot());

            // 결과 단계에서 빌런 퇴출이 확정되면 즉시 생존자 승리다.
            Assert.That(
                _machine.EvaluateWinConditions(
                    CreateSnapshot(isVillainExiled: true)),
                Is.True);
            Assert.That(_machine.Phase, Is.EqualTo(RoundPhase.RoundResult));
            Assert.That(
                _machine.Outcome,
                Is.EqualTo(RoundOutcome.SurvivorsWin));
            Assert.That(
                _machine.EndReason,
                Is.EqualTo(RoundEndReason.VillainExiled));
        }

        [Test]
        public void SkipDiscussion_MovesStraightToVote()
        {
            _machine.TryBeginMeeting();

            Assert.That(_machine.TrySkipDiscussion(), Is.True);
            Assert.That(_machine.Phase, Is.EqualTo(RoundPhase.MeetingVote));
            Assert.That(_machine.RemainingPhaseSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void FinishVoteEarly_MovesToResult()
        {
            _machine.TryBeginMeeting();
            _machine.TrySkipDiscussion();

            Assert.That(_machine.TryFinishVoteEarly(), Is.True);
            Assert.That(
                _machine.Phase,
                Is.EqualTo(RoundPhase.MeetingResult));
        }

        [Test]
        public void IsMeetingActive_TrueOnlyDuringMeetingPhases()
        {
            Assert.That(_machine.IsMeetingActive, Is.False);

            _machine.TryBeginMeeting();
            Assert.That(_machine.IsMeetingActive, Is.True);

            _machine.Tick(90f, CreateSnapshot());
            Assert.That(_machine.IsMeetingActive, Is.True);

            _machine.Tick(30f, CreateSnapshot());
            Assert.That(_machine.IsMeetingActive, Is.True);

            _machine.Tick(5f, CreateSnapshot());
            Assert.That(_machine.IsMeetingActive, Is.False);
        }
    }
}
