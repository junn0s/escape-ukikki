using MonkeyLab.Gameplay.Meeting;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/system-design-document.md §15.1 회의 호출 검증을 고정한다.
    /// 기준값은 balance-and-telemetry.md §2를 따른다(첫 회의 잠금 120초,
    /// 공용 쿨타임 120초, 최대 3회).
    /// </summary>
    public sealed class MeetingCallRulesTests
    {
        private const float FirstMeetingLockSeconds = 120f;
        private const float CooldownSeconds = 120f;
        private const int MaximumMeetingCount = 3;

        private static MeetingRejectionReason Validate(
            bool isExploring = true,
            bool isRoundEnded = false,
            bool isCallerAlive = true,
            float elapsedExplorationSeconds = 300f,
            float secondsSinceLastMeeting = 300f,
            int usedMeetingCount = 0)
        {
            return MeetingCallRules.Validate(
                isExploring,
                isRoundEnded,
                isCallerAlive,
                elapsedExplorationSeconds,
                FirstMeetingLockSeconds,
                secondsSinceLastMeeting,
                CooldownSeconds,
                usedMeetingCount,
                MaximumMeetingCount);
        }

        [Test]
        public void Validate_AcceptsAliveCallerAfterLock()
        {
            Assert.That(
                Validate(),
                Is.EqualTo(MeetingRejectionReason.None));
        }

        [Test]
        public void Validate_RejectsBeforeFirstMeetingLock()
        {
            Assert.That(
                Validate(elapsedExplorationSeconds: 119.9f),
                Is.EqualTo(MeetingRejectionReason.FirstMeetingLocked));
        }

        [Test]
        public void Validate_AcceptsExactlyAtFirstMeetingLock()
        {
            Assert.That(
                Validate(elapsedExplorationSeconds: 120f),
                Is.EqualTo(MeetingRejectionReason.None));
        }

        [Test]
        public void Validate_RejectsDeadCaller()
        {
            Assert.That(
                Validate(isCallerAlive: false),
                Is.EqualTo(MeetingRejectionReason.CallerDead));
        }

        [Test]
        public void Validate_RejectsOutsideExploration()
        {
            Assert.That(
                Validate(isExploring: false),
                Is.EqualTo(MeetingRejectionReason.NotExploring));
        }

        [Test]
        public void Validate_RejectsAfterRoundEnded()
        {
            Assert.That(
                Validate(isRoundEnded: true, isExploring: false),
                Is.EqualTo(MeetingRejectionReason.RoundAlreadyEnded));
        }

        [Test]
        public void Validate_RejectsDuringCooldown()
        {
            Assert.That(
                Validate(
                    usedMeetingCount: 1,
                    secondsSinceLastMeeting: 119.9f),
                Is.EqualTo(MeetingRejectionReason.OnCooldown));
        }

        [Test]
        public void Validate_AcceptsExactlyAtCooldownEnd()
        {
            Assert.That(
                Validate(
                    usedMeetingCount: 1,
                    secondsSinceLastMeeting: 120f),
                Is.EqualTo(MeetingRejectionReason.None));
        }

        [Test]
        public void Validate_IgnoresCooldownBeforeFirstMeeting()
        {
            // 첫 회의는 쿨타임 대상이 아니다.
            Assert.That(
                Validate(
                    usedMeetingCount: 0,
                    secondsSinceLastMeeting: 0f),
                Is.EqualTo(MeetingRejectionReason.None));
        }

        [Test]
        public void Validate_RejectsAfterThreeMeetings()
        {
            Assert.That(
                Validate(usedMeetingCount: 3),
                Is.EqualTo(MeetingRejectionReason.MeetingLimitReached));
        }

        [Test]
        public void Validate_ChecksPhaseBeforeLifeState()
        {
            Assert.That(
                Validate(isExploring: false, isCallerAlive: false),
                Is.EqualTo(MeetingRejectionReason.NotExploring));
        }
    }
}
