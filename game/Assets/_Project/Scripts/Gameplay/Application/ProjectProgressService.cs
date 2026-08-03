using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    public sealed class ProjectProgressService
    {
        private readonly int _maximumPoints;
        private readonly int _personalBudgetPoints;
        private readonly HashSet<MissionCompletionKey> _completedMissions = new();
        private readonly Dictionary<ulong, int> _playerPoints = new();

        public ProjectProgressService(
            int maximumPoints,
            int personalBudgetPoints)
        {
            if (maximumPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPoints));
            }

            if (personalBudgetPoints <= 0 ||
                personalBudgetPoints > maximumPoints)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(personalBudgetPoints));
            }

            _maximumPoints = maximumPoints;
            _personalBudgetPoints = personalBudgetPoints;
        }

        public int Points { get; private set; }
        public int MaximumPoints => _maximumPoints;
        public float NormalizedProgress =>
            (float)Points / _maximumPoints;
        public ProjectMilestone Milestone =>
            ResolveMilestone(Points, _maximumPoints);

        public bool TryCompleteMission(
            ulong playerId,
            ulong missionId,
            int assignedMissionCount,
            out int awardedPoints)
        {
            if (assignedMissionCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(assignedMissionCount));
            }

            var key = new MissionCompletionKey(playerId, missionId);
            if (!_completedMissions.Add(key))
            {
                awardedPoints = 0;
                return false;
            }

            var currentPlayerPoints = _playerPoints.TryGetValue(
                playerId,
                out var storedPoints)
                ? storedPoints
                : 0;
            var remainingPersonalPoints =
                _personalBudgetPoints - currentPlayerPoints;
            if (remainingPersonalPoints <= 0)
            {
                awardedPoints = 0;
                return false;
            }

            var baseAward = _personalBudgetPoints / assignedMissionCount;
            awardedPoints = Mathf.Min(
                Mathf.Max(1, baseAward),
                remainingPersonalPoints);
            Points = Mathf.Min(_maximumPoints, Points + awardedPoints);
            _playerPoints[playerId] =
                currentPlayerPoints + awardedPoints;
            return true;
        }

        public int AddDevelopmentPoints(int points)
        {
            if (points <= 0)
            {
                return 0;
            }

            var previousPoints = Points;
            Points = Mathf.Min(_maximumPoints, Points + points);
            return Points - previousPoints;
        }

        /// <summary>개발 빌드의 단계별 연출과 승패를 반복 확인할 때만 사용한다.</summary>
        public void SetDevelopmentPoints(int points)
        {
            Points = Mathf.Clamp(points, 0, _maximumPoints);
        }

        /// <summary>
        /// 재접속으로 NGO clientId가 바뀌었을 때 개인 점수 예산과 완료 이력을
        /// 새 ID로 옮긴다. 전체 프로젝트 점수는 이미 합산되어 있으므로 건드리지 않는다.
        /// </summary>
        public bool RebindPlayer(ulong previousPlayerId, ulong currentPlayerId)
        {
            if (previousPlayerId == currentPlayerId)
            {
                return false;
            }

            var hasState = false;
            if (_playerPoints.Remove(previousPlayerId, out var points))
            {
                _playerPoints[currentPlayerId] = points;
                hasState = true;
            }

            var reboundKeys = new List<MissionCompletionKey>();
            foreach (var key in _completedMissions)
            {
                if (key.PlayerId == previousPlayerId)
                {
                    reboundKeys.Add(key);
                }
            }

            foreach (var key in reboundKeys)
            {
                _completedMissions.Remove(key);
                _completedMissions.Add(
                    new MissionCompletionKey(currentPlayerId, key.MissionId));
                hasState = true;
            }

            return hasState;
        }

        public static ProjectMilestone ResolveMilestone(
            int points,
            int maximumPoints)
        {
            if (maximumPoints <= 0)
            {
                return ProjectMilestone.None;
            }

            var clampedPoints = Mathf.Clamp(points, 0, maximumPoints);
            if (clampedPoints >= maximumPoints)
            {
                return ProjectMilestone.Completed;
            }

            if (clampedPoints * 4 >= maximumPoints * 3)
            {
                return ProjectMilestone.ExitGuidance;
            }

            if (clampedPoints * 2 >= maximumPoints)
            {
                return ProjectMilestone.SecurityAccess;
            }

            return clampedPoints * 4 >= maximumPoints
                ? ProjectMilestone.FacilityGuidance
                : ProjectMilestone.None;
        }

        private readonly struct MissionCompletionKey :
            IEquatable<MissionCompletionKey>
        {
            public MissionCompletionKey(ulong playerId, ulong missionId)
            {
                PlayerId = playerId;
                MissionId = missionId;
            }

            public ulong PlayerId { get; }
            public ulong MissionId { get; }

            public bool Equals(MissionCompletionKey other)
            {
                return PlayerId == other.PlayerId &&
                       MissionId == other.MissionId;
            }

            public override bool Equals(object obj)
            {
                return obj is MissionCompletionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PlayerId, MissionId);
            }
        }
    }
}
