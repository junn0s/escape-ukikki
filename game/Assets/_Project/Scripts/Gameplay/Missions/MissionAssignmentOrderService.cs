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
            : this(
                missionId,
                position,
                MissionPrototypeKind.FuseSequence)
        {
        }

        public MissionAssignmentCandidate(
            ulong missionId,
            Vector2 position,
            MissionPrototypeKind kind)
        {
            MissionId = missionId;
            Position = position;
            Kind = kind;
            Difficulty = MissionDifficultyRules.Resolve(kind);
        }

        public ulong MissionId { get; }
        public Vector2 Position { get; }
        public MissionPrototypeKind Kind { get; }
        public MissionDifficulty Difficulty { get; }
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

        /// <summary>
        /// 전체 방의 후보 중 한 플레이어에게 필요한 수만 고른다.
        /// 첫 미션은 시작점에서 가장 가까운 곳으로 두고, 나머지는 거리 순위 전반에
        /// 고르게 퍼뜨려 한 구역만 반복하지 않게 한다(SDD §7.2).
        /// </summary>
        public static ulong[] SelectSpreadAssignments(
            Vector2 startPosition,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            int assignedCount)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (assignedCount <= 0 || assignedCount > candidates.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(assignedCount));
            }

            var ordered = OrderByDistance(startPosition, candidates);
            if (assignedCount == ordered.Length)
            {
                return ordered;
            }

            var selected = new ulong[assignedCount];
            selected[0] = ordered[0];
            if (assignedCount == 1)
            {
                return selected;
            }

            var maximumRank = ordered.Length - 1;
            for (var index = 1; index < assignedCount; index++)
            {
                var normalizedRank = index / (float)(assignedCount - 1);
                var rank = Mathf.RoundToInt(normalizedRank * maximumRank);
                selected[index] = ordered[rank];
            }

            return selected;
        }

        /// <summary>
        /// 어려운 차단기 미션이 4개 후보 안에 포함되면 4개를, 그렇지 않으면
        /// 쉬운 미션 중심의 5개를 배정한다. 어느 경우든 최소 세 종류가 섞이도록
        /// 거리 분산 결과의 뒤쪽 항목부터 교체한다(SDD §7.2).
        /// </summary>
        public static ulong[] SelectDifficultyAdjustedAssignments(
            Vector2 startPosition,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            int difficultSetCount,
            int easySetCount,
            int minimumKindCount)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (difficultSetCount <= 0 ||
                easySetCount < difficultSetCount ||
                easySetCount > candidates.Count ||
                minimumKindCount <= 0 ||
                minimumKindCount > difficultSetCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(difficultSetCount));
            }

            var difficultSet = SelectSpreadAssignments(
                startPosition,
                candidates,
                difficultSetCount);
            var selected = ContainsDifficulty(
                difficultSet,
                candidates,
                MissionDifficulty.Hard)
                ? difficultSet
                : SelectSpreadAssignments(
                    startPosition,
                    candidates,
                    easySetCount);

            EnsureKindDiversity(
                selected,
                startPosition,
                candidates,
                minimumKindCount);
            if (selected.Length == easySetCount &&
                ContainsDifficulty(
                    selected,
                    candidates,
                    MissionDifficulty.Hard))
            {
                selected = SelectSpreadAssignments(
                    startPosition,
                    candidates,
                    difficultSetCount);
                EnsureKindDiversity(
                    selected,
                    startPosition,
                    candidates,
                    minimumKindCount);
            }

            return selected;
        }

        private static bool ContainsDifficulty(
            IReadOnlyList<ulong> missionIds,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            MissionDifficulty difficulty)
        {
            for (var idIndex = 0; idIndex < missionIds.Count; idIndex++)
            {
                for (var candidateIndex = 0;
                     candidateIndex < candidates.Count;
                     candidateIndex++)
                {
                    if (candidates[candidateIndex].MissionId ==
                            missionIds[idIndex] &&
                        candidates[candidateIndex].Difficulty == difficulty)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureKindDiversity(
            ulong[] selected,
            Vector2 startPosition,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            int minimumKindCount)
        {
            if (minimumKindCount <= 1)
            {
                return;
            }

            var orderedIds = OrderByDistance(startPosition, candidates);
            while (CountDistinctKinds(selected, candidates) <
                   minimumKindCount)
            {
                var replacementId = FindNearestUnselectedKind(
                    selected,
                    orderedIds,
                    candidates);
                if (replacementId == 0)
                {
                    return;
                }

                var replaceIndex = FindReplaceableIndex(
                    selected,
                    candidates);
                if (replaceIndex <= 0)
                {
                    return;
                }

                selected[replaceIndex] = replacementId;
            }
        }

        private static int CountDistinctKinds(
            IReadOnlyList<ulong> missionIds,
            IReadOnlyList<MissionAssignmentCandidate> candidates)
        {
            var kinds = new HashSet<MissionPrototypeKind>();
            for (var index = 0; index < missionIds.Count; index++)
            {
                if (TryFindCandidate(
                        missionIds[index],
                        candidates,
                        out var candidate))
                {
                    kinds.Add(candidate.Kind);
                }
            }

            return kinds.Count;
        }

        private static ulong FindNearestUnselectedKind(
            IReadOnlyList<ulong> selected,
            IReadOnlyList<ulong> orderedIds,
            IReadOnlyList<MissionAssignmentCandidate> candidates)
        {
            var selectedKinds = new HashSet<MissionPrototypeKind>();
            for (var index = 0; index < selected.Count; index++)
            {
                if (TryFindCandidate(
                        selected[index],
                        candidates,
                        out var selectedCandidate))
                {
                    selectedKinds.Add(selectedCandidate.Kind);
                }
            }

            for (var index = 0; index < orderedIds.Count; index++)
            {
                var missionId = orderedIds[index];
                if (ContainsId(selected, missionId) ||
                    !TryFindCandidate(
                        missionId,
                        candidates,
                        out var candidate) ||
                    selectedKinds.Contains(candidate.Kind))
                {
                    continue;
                }

                return missionId;
            }

            return 0;
        }

        private static int FindReplaceableIndex(
            IReadOnlyList<ulong> selected,
            IReadOnlyList<MissionAssignmentCandidate> candidates)
        {
            var kindCounts = new Dictionary<MissionPrototypeKind, int>();
            for (var index = 0; index < selected.Count; index++)
            {
                if (!TryFindCandidate(
                        selected[index],
                        candidates,
                        out var candidate))
                {
                    continue;
                }

                kindCounts.TryGetValue(candidate.Kind, out var count);
                kindCounts[candidate.Kind] = count + 1;
            }

            for (var index = selected.Count - 1; index > 0; index--)
            {
                if (TryFindCandidate(
                        selected[index],
                        candidates,
                        out var candidate) &&
                    kindCounts[candidate.Kind] > 1)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool ContainsId(
            IReadOnlyList<ulong> missionIds,
            ulong missionId)
        {
            for (var index = 0; index < missionIds.Count; index++)
            {
                if (missionIds[index] == missionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindCandidate(
            ulong missionId,
            IReadOnlyList<MissionAssignmentCandidate> candidates,
            out MissionAssignmentCandidate candidate)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].MissionId == missionId)
                {
                    candidate = candidates[index];
                    return true;
                }
            }

            candidate = default;
            return false;
        }
    }
}
