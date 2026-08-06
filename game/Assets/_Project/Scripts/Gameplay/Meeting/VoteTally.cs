using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Meeting
{
    /// <summary>
    /// 회의 투표 집계다. docs/game-design-document.md §16.3을 따른다.
    ///
    /// - 각 플레이어는 한 표만 가지며 마지막 표만 유효하다.
    /// - 자기 자신에게도 투표할 수 있다.
    /// - 단독 최다 득표자만 퇴출한다.
    /// - 동률이면 아무도 퇴출하지 않는다.
    /// - 기권이 단독 최다여도 아무도 퇴출하지 않는다.
    /// - 미투표자는 집계 시점에 기권으로 처리한다.
    /// </summary>
    public sealed class VoteTally
    {
        /// <summary>기권을 나타내는 대상 ID다.</summary>
        public const ulong AbstainTargetId = ulong.MaxValue;

        private readonly HashSet<ulong> _eligibleVoters = new();
        private readonly Dictionary<ulong, ulong> _votes = new();

        public int EligibleVoterCount => _eligibleVoters.Count;
        public int CastVoteCount => _votes.Count;

        public VoteTally(IReadOnlyList<ulong> eligibleVoterIds)
        {
            if (eligibleVoterIds == null)
            {
                throw new ArgumentNullException(nameof(eligibleVoterIds));
            }

            for (var index = 0; index < eligibleVoterIds.Count; index++)
            {
                _eligibleVoters.Add(eligibleVoterIds[index]);
            }
        }

        public bool IsEligible(ulong voterId)
        {
            return _eligibleVoters.Contains(voterId);
        }

        public bool HasVoted(ulong voterId)
        {
            return _votes.ContainsKey(voterId);
        }

        public bool TryGetVote(ulong voterId, out ulong targetId)
        {
            return _votes.TryGetValue(voterId, out targetId);
        }

        /// <summary>
        /// 표를 기록한다. 제한시간 안에는 표를 바꿀 수 있으므로 덮어쓴다.
        /// 살아 있지 않은 대상이나 비참가자에게는 투표할 수 없다.
        /// </summary>
        public bool TryCastVote(ulong voterId, ulong targetId)
        {
            if (!_eligibleVoters.Contains(voterId))
            {
                return false;
            }

            if (targetId != AbstainTargetId &&
                !_eligibleVoters.Contains(targetId))
            {
                return false;
            }

            _votes[voterId] = targetId;
            return true;
        }

        /// <summary>
        /// 재접속으로 참가자의 네트워크 ID가 바뀌어도 기존 투표권과 표를 유지한다.
        /// 해당 참가자가 이미 던진 표와 다른 참가자가 그에게 던진 표를 함께 옮긴다.
        /// </summary>
        public bool RebindPlayer(ulong previousPlayerId, ulong currentPlayerId)
        {
            if (previousPlayerId == currentPlayerId ||
                !_eligibleVoters.Contains(previousPlayerId) ||
                _eligibleVoters.Contains(currentPlayerId))
            {
                return false;
            }

            _eligibleVoters.Remove(previousPlayerId);
            _eligibleVoters.Add(currentPlayerId);
            if (_votes.Remove(previousPlayerId, out var targetId))
            {
                _votes[currentPlayerId] = targetId == previousPlayerId
                    ? currentPlayerId
                    : targetId;
            }

            var voterIds = new List<ulong>(_votes.Keys);
            foreach (var voterId in voterIds)
            {
                if (_votes[voterId] == previousPlayerId)
                {
                    _votes[voterId] = currentPlayerId;
                }
            }

            return true;
        }

        public int GetVoteCount(ulong targetId)
        {
            var count = 0;
            foreach (var pair in _votes)
            {
                if (pair.Value == targetId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>미투표자를 기권으로 확정한다.</summary>
        public int GetAbstainCount()
        {
            var explicitAbstain = GetVoteCount(AbstainTargetId);
            var notVoted = _eligibleVoters.Count - _votes.Count;
            return explicitAbstain + notVoted;
        }

        /// <summary>
        /// 결과 화면에 공개할 최종 표를 복사한다.
        /// 시간 내 투표하지 않은 참가자는 기획서대로 기권으로 확정한다.
        /// 반환된 사본은 집계 상태를 변경하지 않는다.
        /// </summary>
        public Dictionary<ulong, ulong> CreateFinalVoteSnapshot()
        {
            var snapshot = new Dictionary<ulong, ulong>(
                _eligibleVoters.Count);
            foreach (var voterId in _eligibleVoters)
            {
                snapshot[voterId] = _votes.TryGetValue(
                    voterId,
                    out var targetId)
                        ? targetId
                        : AbstainTargetId;
            }

            return snapshot;
        }

        /// <summary>
        /// 집계 결과를 낸다. 퇴출 대상이 없으면 false를 반환한다.
        /// </summary>
        public bool TryResolveExile(out ulong exiledPlayerId)
        {
            exiledPlayerId = 0;

            var highestCount = 0;
            var leaderCount = 0;
            var leaderId = 0ul;
            foreach (var voterId in _eligibleVoters)
            {
                var count = GetVoteCount(voterId);
                if (count > highestCount)
                {
                    highestCount = count;
                    leaderId = voterId;
                    leaderCount = 1;
                }
                else if (count == highestCount && count > 0)
                {
                    leaderCount++;
                }
            }

            if (highestCount <= 0 || leaderCount != 1)
            {
                // 아무도 표를 받지 않았거나 동률이면 퇴출하지 않는다.
                return false;
            }

            // 기권이 단독 최다이거나 최다 득표와 같으면 퇴출하지 않는다.
            if (GetAbstainCount() >= highestCount)
            {
                return false;
            }

            exiledPlayerId = leaderId;
            return true;
        }
    }
}
