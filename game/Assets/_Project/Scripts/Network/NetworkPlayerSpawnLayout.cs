using UnityEngine;

namespace MonkeyLab.Network
{
    public static class NetworkPlayerSpawnLayout
    {
        private static readonly Vector3[] LaboratoryPositions =
        {
            new(-25f, -7f, 0f),
            new(-10f, 24f, 0f),
            new(13f, -7f, 0f),
            new(-7f, -29f, 0f),
            new(13f, -29f, 0f),
            new(-7f, -7f, 0f)
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
