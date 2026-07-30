using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    public readonly struct MissionAssignmentCandidate
    {
        public MissionAssignmentCandidate(
            ulong missionId,
            Vector2 position)
        {
            MissionId = missionId;
            Position = position;
        }

        public ulong MissionId { get; }
        public Vector2 Position { get; }
    }

    public static class MissionAssignmentOrderService
    {
        public static ulong[] OrderByDistance(
            Vector2 startPosition,
            IReadOnlyList<MissionAssignmentCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var ordered = new MissionAssignmentCandidate[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                ordered[index] = candidates[index];
            }

            Array.Sort(
                ordered,
                (left, right) =>
                {
                    var leftDistance =
                        (left.Position - startPosition).sqrMagnitude;
                    var rightDistance =
                        (right.Position - startPosition).sqrMagnitude;
                    var distanceComparison =
                        leftDistance.CompareTo(rightDistance);
                    return distanceComparison != 0
                        ? distanceComparison
                        : left.MissionId.CompareTo(right.MissionId);
                });

            var missionIds = new ulong[ordered.Length];
            for (var index = 0; index < ordered.Length; index++)
            {
                missionIds[index] = ordered[index].MissionId;
            }

            return missionIds;
        }
    }
}
