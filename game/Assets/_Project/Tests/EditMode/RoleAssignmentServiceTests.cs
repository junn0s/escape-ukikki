using System;
using System.Linq;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class RoleAssignmentServiceTests
    {
        [Test]
        public void AssignRoles_AssignsExactlyOneVillain()
        {
            var service = new RoleAssignmentService();
            var assignments = service.AssignRoles(
                new ulong[] { 10, 20, 30, 40, 50, 60 },
                villainIndex: 3);

            Assert.That(
                assignments.Count(
                    assignment =>
                        assignment.Role == PlayerRole.Villain),
                Is.EqualTo(1));
            Assert.That(
                assignments.Single(
                    assignment =>
                        assignment.Role == PlayerRole.Villain).ClientId,
                Is.EqualTo(40));
            Assert.That(
                assignments.Count(
                    assignment =>
                        assignment.Role == PlayerRole.Survivor),
                Is.EqualTo(5));
        }

        [Test]
        public void AssignRoles_RejectsDuplicateParticipant()
        {
            var service = new RoleAssignmentService();

            Assert.Throws<ArgumentException>(() =>
                service.AssignRoles(
                    new ulong[] { 10, 10 },
                    villainIndex: 0));
        }
    }
}
