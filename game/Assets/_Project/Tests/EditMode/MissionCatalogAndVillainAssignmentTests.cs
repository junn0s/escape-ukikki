using System.Collections.Generic;
using System.Linq;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class MissionCatalogAndVillainAssignmentTests
    {
        [Test]
        public void SurvivorCatalogContainsTwentyTwoUniqueMissionsAcrossTenRooms()
        {
            var definitions = SurvivorMissionCatalog.All;

            Assert.That(definitions.Count, Is.EqualTo(22));
            Assert.That(
                definitions.Select(item => item.MissionId).Distinct().Count(),
                Is.EqualTo(22));
            Assert.That(
                definitions.Select(item => item.RoomId).Distinct().Count(),
                Is.EqualTo(10));
            Assert.That(
                definitions.Count(item => item.RoomId == "LabB"),
                Is.EqualTo(3));
            Assert.That(
                definitions.Count(item => item.RoomId == "QuarantineA"),
                Is.EqualTo(3));
        }

        [Test]
        public void SurvivorMissionIdsRoundTripToDefinitions()
        {
            foreach (var expected in SurvivorMissionCatalog.All)
            {
                Assert.That(
                    SurvivorMissionCatalog.TryGetDefinition(
                        expected.MissionId,
                        out var actual),
                    Is.True);
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName));
            }

            Assert.That(
                SurvivorMissionCatalog.TryGetDefinition(1UL, out _),
                Is.False);
        }

        [Test]
        public void VillainAssignmentSelectsFourUniqueMissionsFromSix()
        {
            var assigned = VillainMissionAssignmentService.Assign(20260808);

            Assert.That(
                VillainMissionCatalog.All.Count,
                Is.EqualTo(VillainMissionAssignmentService.TotalMissionCount));
            Assert.That(
                assigned.Length,
                Is.EqualTo(VillainMissionAssignmentService.AssignedMissionCount));
            Assert.That(
                new HashSet<VillainMissionKind>(assigned).Count,
                Is.EqualTo(assigned.Length));
        }

        [Test]
        public void VillainAssignmentIsDeterministicForSameSeed()
        {
            var first = VillainMissionAssignmentService.Assign(77);
            var second = VillainMissionAssignmentService.Assign(77);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void VillainClearStateRejectsUnassignedAndDuplicateMissions()
        {
            var assigned = new[]
            {
                VillainMissionKind.CultureContamination,
                VillainMissionKind.VentBackflow,
                VillainMissionKind.MedicationRecordWipe,
                VillainMissionKind.MainPowerLineCut
            };
            var state = new VillainMissionClearState();
            state.Assign(assigned);

            Assert.That(
                state.TryComplete(
                    VillainMissionKind.SecurityWireTangle,
                    out _),
                Is.False,
                "배정되지 않은 미션은 강화 스택을 올리면 안 된다.");
            Assert.That(
                state.TryComplete(
                    VillainMissionKind.CultureContamination,
                    out var firstClearCount),
                Is.True);
            Assert.That(firstClearCount, Is.EqualTo(1));
            Assert.That(
                state.TryComplete(
                    VillainMissionKind.CultureContamination,
                    out _),
                Is.False,
                "같은 미션 재완료로 강화 스택을 중복 획득하면 안 된다.");
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(1, 1, 0, 0)]
        [TestCase(2, 2, 0, 0)]
        [TestCase(3, 2, 1, 0)]
        [TestCase(4, 2, 1, 1)]
        public void VillainClearCountMapsToExpectedStackEffects(
            int clearCount,
            int populationTier,
            int toxicityTier,
            int proximityTier)
        {
            Assert.That(
                VillainMissionStackEffectRules.GetPopulationTier(clearCount),
                Is.EqualTo(populationTier));
            Assert.That(
                VillainMissionStackEffectRules.GetToxicityTier(clearCount),
                Is.EqualTo(toxicityTier));
            Assert.That(
                VillainMissionStackEffectRules.GetProximityDetectionTier(
                    clearCount),
                Is.EqualTo(proximityTier));
        }
    }
}
