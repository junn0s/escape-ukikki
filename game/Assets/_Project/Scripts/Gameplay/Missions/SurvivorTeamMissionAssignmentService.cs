using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    public readonly struct SurvivorMissionAssignee
    {
        public SurvivorMissionAssignee(ulong playerId, Vector2 startPosition)
        {
            PlayerId = playerId;
            StartPosition = startPosition;
        }

        public ulong PlayerId { get; }
        public Vector2 StartPosition { get; }
    }

    public readonly struct SurvivorMissionAssignment
    {
        public SurvivorMissionAssignment(ulong playerId, ulong[] missionIds)
        {
            PlayerId = playerId;
            MissionIds = missionIds ?? Array.Empty<ulong>();
        }

        public ulong PlayerId { get; }
        public ulong[] MissionIds { get; }
    }

    /// <summary>
    /// 생존자 전원의 개인 미션을 한 번에 배정한다. 같은 라운드에서는 하나의
    /// 스테이션을 한 명에게만 주어 서버 권위 퍼즐 상태가 서로 섞이지 않게 한다.
    /// 각 플레이어는 난이도 구성에 따라 4~5개를 받고 최소 네 개를 보장한다.
    /// </summary>
    public static class SurvivorTeamMissionAssignmentService
    {
        public static SurvivorMissionAssignment[] Assign(
            IReadOnlyList<SurvivorMissionAssignee> assignees,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            int difficultSetCount,
            int easySetCount,
            int minimumKindCount)
        {
            if (assignees == null)
            {
                throw new ArgumentNullException(nameof(assignees));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (assignees.Count == 0)
            {
                return Array.Empty<SurvivorMissionAssignment>();
            }

            if (difficultSetCount <= 0 ||
                easySetCount < difficultSetCount ||
                minimumKindCount <= 0 ||
                minimumKindCount > difficultSetCount ||
                candidates.Count < assignees.Count * difficultSetCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(difficultSetCount));
            }

            var orderedAssignees = new SurvivorMissionAssignee[assignees.Count];
            for (var index = 0; index < assignees.Count; index++)
            {
                orderedAssignees[index] = assignees[index];
            }

            Array.Sort(
                orderedAssignees,
                (left, right) => left.PlayerId.CompareTo(right.PlayerId));

            var remaining = new List<MissionAssignmentCandidate>(
                candidates.Count);
            var knownMissionIds = new HashSet<ulong>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.MissionId == 0UL ||
                    !knownMissionIds.Add(candidate.MissionId))
                {
                    throw new ArgumentException(
                        "Mission candidates must have unique non-zero IDs.",
                        nameof(candidates));
                }

                remaining.Add(candidate);
            }

            var assignments =
                new SurvivorMissionAssignment[orderedAssignees.Length];
            for (var assigneeIndex = 0;
                 assigneeIndex < orderedAssignees.Length;
                 assigneeIndex++)
            {
                var remainingPlayerCount =
                    orderedAssignees.Length - assigneeIndex - 1;
                var reservedMinimum =
                    remainingPlayerCount * difficultSetCount;
                var maximumForCurrent = Mathf.Min(
                    easySetCount,
                    remaining.Count - reservedMinimum);
                var selected = MissionAssignmentOrderService
                    .SelectDifficultyAdjustedAssignments(
                        orderedAssignees[assigneeIndex].StartPosition,
                        remaining,
                        difficultSetCount,
                        maximumForCurrent,
                        minimumKindCount);

                assignments[assigneeIndex] = new SurvivorMissionAssignment(
                    orderedAssignees[assigneeIndex].PlayerId,
                    selected);
                RemoveSelected(remaining, selected);
            }

            return assignments;
        }

        private static void RemoveSelected(
            List<MissionAssignmentCandidate> candidates,
            IReadOnlyList<ulong> selectedMissionIds)
        {
            for (var selectedIndex = 0;
                 selectedIndex < selectedMissionIds.Count;
                 selectedIndex++)
            {
                for (var candidateIndex = candidates.Count - 1;
                     candidateIndex >= 0;
                     candidateIndex--)
                {
                    if (candidates[candidateIndex].MissionId ==
                        selectedMissionIds[selectedIndex])
                    {
                        candidates.RemoveAt(candidateIndex);
                        break;
                    }
                }
            }
        }
    }
}
