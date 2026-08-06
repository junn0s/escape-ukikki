using UnityEngine;

namespace MonkeyLab.Network
{
    public static class NetworkPlayerSpawnLayout
    {
        private static readonly Vector3[] LaboratoryPositions =
        {
            new(-22f, 3f, 0f),
            new(-17f, 15f, 0f),
            new(6f, 7f, 0f),
            new(14f, 15f, 0f),
            new(-18f, -7f, 0f),
            new(1f, -7f, 0f)
        };

        public static int SlotCount => LaboratoryPositions.Length;

        public static bool TryGetLaboratoryPosition(
            int slotIndex,
            out Vector3 position)
        {
            if (slotIndex < 0 || slotIndex >= LaboratoryPositions.Length)
            {
                position = default;
                return false;
            }

            position = LaboratoryPositions[slotIndex];
            return true;
        }
    }
}
