using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public static class MonsterPerceptionRules
    {
        public static bool IsWithinRadius(Vector3 source, Vector3 target, float radius)
        {
            var planarOffset = new Vector2(target.x - source.x, target.y - source.y);
            return planarOffset.sqrMagnitude <= radius * radius;
        }
    }
}
