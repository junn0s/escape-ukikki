using MonkeyLab.Gameplay.Interaction;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class NetworkInteractionRulesTests
    {
        [Test]
        public void Validate_AcceptsCurrentOwnedUnoccupiedTarget()
        {
            var result = NetworkInteractionRules.Validate(
                isOwnedBySender: true,
                clientSequence: 2,
                lastAcceptedSequence: 1,
                isTargetActive: true,
                isOccupiedByOtherPlayer: false,
                squaredDistanceMeters: 2.25f,
                interactionRangeMeters: 1.5f,
                hasUnblockedPath: true);

            Assert.That(
                result,
                Is.EqualTo(InteractionRejectionReason.None));
        }

        [TestCase(
            false, 2u, 1u, true, false, 1f, true,
            InteractionRejectionReason.InvalidOwner)]
        [TestCase(
            true, 1u, 1u, true, false, 1f, true,
            InteractionRejectionReason.StaleSequence)]
        [TestCase(
            true, 2u, 1u, false, false, 1f, true,
            InteractionRejectionReason.TargetInactive)]
        [TestCase(
            true, 2u, 1u, true, true, 1f, true,
            InteractionRejectionReason.TargetOccupied)]
        [TestCase(
            true, 2u, 1u, true, false, 2.26f, true,
            InteractionRejectionReason.OutOfRange)]
        [TestCase(
            true, 2u, 1u, true, false, 1f, false,
            InteractionRejectionReason.PathBlocked)]
        public void Validate_RejectsInvalidRequest(
            bool isOwnedBySender,
            uint clientSequence,
            uint lastAcceptedSequence,
            bool isTargetActive,
            bool isOccupiedByOtherPlayer,
            float squaredDistanceMeters,
            bool hasUnblockedPath,
            InteractionRejectionReason expected)
        {
            var result = NetworkInteractionRules.Validate(
                isOwnedBySender,
                clientSequence,
                lastAcceptedSequence,
                isTargetActive,
                isOccupiedByOtherPlayer,
                squaredDistanceMeters,
                interactionRangeMeters: 1.5f,
                hasUnblockedPath);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
