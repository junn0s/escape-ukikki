using System.Collections.Generic;
using MonkeyLab.Network;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class NetworkPlayerSpawnLayoutTests
    {
        [Test]
        public void LaboratoryProvidesSixUniquePlayerSpawns()
        {
            var positions = new HashSet<Vector3>();
            for (var slotIndex = 0;
                 slotIndex < NetworkPlayerSpawnLayout.SlotCount;
                 slotIndex++)
            {
                Assert.That(
                    NetworkPlayerSpawnLayout.TryGetLaboratoryPosition(
                        slotIndex,
                        out var position),
                    Is.True);
                Assert.That(positions.Add(position), Is.True);
            }

            Assert.That(
                positions.Count,
                Is.EqualTo(GameSessionService.RequiredPlayerCount));
        }

        [Test]
        public void LaboratorySpawnsMatchWalkableRoomCenters()
        {
            var expectedPositions = new[]
            {
                new Vector3(-25f, -7f, 0f),
                new Vector3(-10f, 24f, 0f),
                new Vector3(13f, -7f, 0f),
                new Vector3(-7f, -29f, 0f),
                new Vector3(13f, -29f, 0f),
                new Vector3(-7f, -7f, 0f)
            };

            for (var slotIndex = 0;
                 slotIndex < expectedPositions.Length;
                 slotIndex++)
            {
                Assert.That(
                    NetworkPlayerSpawnLayout.TryGetLaboratoryPosition(
                        slotIndex,
                        out var position),
                    Is.True);
                Assert.That(position, Is.EqualTo(expectedPositions[slotIndex]));
            }
        }

        [TestCase(-1)]
        [TestCase(6)]
        public void InvalidSlotDoesNotProvideSpawn(int slotIndex)
        {
            Assert.That(
                NetworkPlayerSpawnLayout.TryGetLaboratoryPosition(
                    slotIndex,
                    out _),
                Is.False);
        }
    }
}
