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
                new Vector3(-22f, 3f, 0f),
                new Vector3(-17f, 15f, 0f),
                new Vector3(6f, 7f, 0f),
                new Vector3(14f, 15f, 0f),
                new Vector3(-18f, -7f, 0f),
                new Vector3(1f, -7f, 0f)
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
