using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public static class MonsterPerceptionRules
    {
        private const float NearZeroDistance = 0.0001f;

        public static bool IsWithinVisionCone(
            Vector3 observerForward,
            Vector3 toTarget,
            float visionDistance,
            float visionAngleDegrees)
        {
            var horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            var distanceSquared = horizontalDirection.sqrMagnitude;
            if (distanceSquared > visionDistance * visionDistance)
            {
                return false;
            }

            if (distanceSquared <= NearZeroDistance)
            {
                return true;
            }

            var horizontalForward = Vector3.ProjectOnPlane(observerForward, Vector3.up).normalized;
            var minimumDot = Mathf.Cos(visionAngleDegrees * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(horizontalForward, horizontalDirection.normalized) >= minimumDot;
        }

        public static bool IsWithinRadius(Vector3 source, Vector3 target, float radius)
        {
            var horizontalOffset = Vector3.ProjectOnPlane(target - source, Vector3.up);
            return horizontalOffset.sqrMagnitude <= radius * radius;
        }
    }
}
