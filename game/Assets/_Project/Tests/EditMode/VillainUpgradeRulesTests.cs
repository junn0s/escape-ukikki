using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class VillainUpgradeRulesTests
    {
        [Test]
        public void Validate_AcceptsVillainOnUpgradableAxis()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Villain,
                canUpgradeAxis: true,
                allowsUpgradeInteraction: true,
                isOccupiedByOtherPlayer: false);

            Assert.That(reason, Is.EqualTo(UpgradeRejectionReason.None));
        }

        [Test]
        public void Validate_RejectsSurvivor()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Survivor,
                canUpgradeAxis: true,
                allowsUpgradeInteraction: true,
                isOccupiedByOtherPlayer: false);

            Assert.That(
                reason,
                Is.EqualTo(UpgradeRejectionReason.NotVillain));
        }

        [Test]
        public void Validate_RejectsUnassignedRole()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Unassigned,
                canUpgradeAxis: true,
                allowsUpgradeInteraction: true,
                isOccupiedByOtherPlayer: false);

            Assert.That(
                reason,
                Is.EqualTo(UpgradeRejectionReason.NotVillain));
        }

        [Test]
        public void Validate_RejectsWhenRoundPhaseBlocks()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Villain,
                canUpgradeAxis: true,
                allowsUpgradeInteraction: false,
                isOccupiedByOtherPlayer: false);

            Assert.That(
                reason,
                Is.EqualTo(UpgradeRejectionReason.RoundPhaseBlocked));
        }

        [Test]
        public void Validate_RejectsWhenAxisAlreadyMaxed()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Villain,
                canUpgradeAxis: false,
                allowsUpgradeInteraction: true,
                isOccupiedByOtherPlayer: false);

            Assert.That(
                reason,
                Is.EqualTo(UpgradeRejectionReason.AxisAtMaximum));
        }

        [Test]
        public void Validate_ChecksRoleBeforeAxisLimit()
        {
            var reason = VillainUpgradeRules.Validate(
                PlayerRole.Survivor,
                canUpgradeAxis: false,
                allowsUpgradeInteraction: true,
                isOccupiedByOtherPlayer: false);

            Assert.That(
                reason,
                Is.EqualTo(UpgradeRejectionReason.NotVillain));
        }
    }
}
