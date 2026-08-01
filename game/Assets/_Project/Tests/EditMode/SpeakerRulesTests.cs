using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class SpeakerRulesTests
    {
        private static SpeakerRejectionReason Validate(
            PlayerRole role = PlayerRole.Villain,
            bool isAlive = true,
            bool allowsUse = true,
            bool isKnownRoom = true,
            bool isReady = true)
        {
            return SpeakerRules.Validate(
                role,
                isAlive,
                allowsUse,
                isKnownRoom,
                isReady);
        }

        [Test]
        public void Validate_AcceptsAliveVillainInExploration()
        {
            Assert.That(
                Validate(),
                Is.EqualTo(SpeakerRejectionReason.None));
        }

        [Test]
        public void Validate_RejectsSurvivor()
        {
            Assert.That(
                Validate(role: PlayerRole.Survivor),
                Is.EqualTo(SpeakerRejectionReason.NotVillain));
        }

        [Test]
        public void Validate_RejectsUnassignedRole()
        {
            Assert.That(
                Validate(role: PlayerRole.Unassigned),
                Is.EqualTo(SpeakerRejectionReason.NotVillain));
        }

        [Test]
        public void Validate_RejectsDeadVillain()
        {
            Assert.That(
                Validate(isAlive: false),
                Is.EqualTo(SpeakerRejectionReason.VillainDead));
        }

        [Test]
        public void Validate_RejectsDuringGracePeriodOrMeeting()
        {
            // 시작 보호 시간과 회의 중에는 탐색 단계가 아니다(GDD §13.1).
            Assert.That(
                Validate(allowsUse: false),
                Is.EqualTo(SpeakerRejectionReason.RoundPhaseBlocked));
        }

        [Test]
        public void Validate_RejectsUnknownRoom()
        {
            Assert.That(
                Validate(isKnownRoom: false),
                Is.EqualTo(SpeakerRejectionReason.UnknownRoom));
        }

        [Test]
        public void Validate_RejectsWhileOnCooldown()
        {
            Assert.That(
                Validate(isReady: false),
                Is.EqualTo(SpeakerRejectionReason.OnCooldown));
        }

        [Test]
        public void Validate_ChecksRoleBeforeCooldown()
        {
            Assert.That(
                Validate(role: PlayerRole.Survivor, isReady: false),
                Is.EqualTo(SpeakerRejectionReason.NotVillain));
        }

        [Test]
        public void Validate_ChecksPhaseBeforeCooldown()
        {
            Assert.That(
                Validate(allowsUse: false, isReady: false),
                Is.EqualTo(SpeakerRejectionReason.RoundPhaseBlocked));
        }
    }
}
