using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// 여러 괴물이 같은 방 중심을 순찰 목적지로 동시에 선택하지 않도록 한다.
    /// 소음 조사와 추격은 집결이 의도된 상태이므로 순찰 중에만 예약한다.
    /// </summary>
    public static class MonsterPatrolReservation
    {
        private static readonly Dictionary<Vector2, UnityEngine.Object> Owners =
            new();
        private static readonly List<Vector2> ReleaseBuffer = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Owners.Clear();
            ReleaseBuffer.Clear();
        }

        public static bool TryReserve(
            Vector2 position,
            UnityEngine.Object owner)
        {
            if (owner == null)
            {
                return false;
            }

            if (Owners.TryGetValue(position, out var currentOwner) &&
                currentOwner != null && currentOwner != owner)
            {
                return false;
            }

            Owners[position] = owner;
            return true;
        }

        public static void Release(
            Vector2 position,
            UnityEngine.Object owner)
        {
            if (owner != null &&
                Owners.TryGetValue(position, out var currentOwner) &&
                currentOwner == owner)
            {
                Owners.Remove(position);
            }
        }

        public static void ReleaseAll(UnityEngine.Object owner)
        {
            if (owner == null)
            {
                return;
            }

            ReleaseBuffer.Clear();
            foreach (var pair in Owners)
            {
                if (pair.Value == owner || pair.Value == null)
                {
                    ReleaseBuffer.Add(pair.Key);
                }
            }

            foreach (var position in ReleaseBuffer)
            {
                Owners.Remove(position);
            }

            ReleaseBuffer.Clear();
        }
    }
}
