using MonkeyLab.Gameplay.Meeting;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/game-design-document.md §16.3 투표 규칙을 고정한다.
    /// </summary>
    public sealed class VoteTallyTests
    {
        private static VoteTally CreateTally(int voterCount = 5)
        {
            var voters = new ulong[voterCount];
            for (var index = 0; index < voterCount; index++)
            {
                voters[index] = (ulong)index;
            }

            return new VoteTally(voters);
        }

        [Test]
        public void SoleHighestVote_ExilesThatPlayer()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, 3);
            tally.TryCastVote(1, 3);
            tally.TryCastVote(2, 3);
            tally.TryCastVote(3, 0);
            tally.TryCastVote(4, 0);

            Assert.That(tally.TryResolveExile(out var exiled), Is.True);
            Assert.That(exiled, Is.EqualTo(3ul));
        }

        [Test]
        public void TiedVotes_ExileNobody()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, 2);
            tally.TryCastVote(1, 2);
            tally.TryCastVote(2, 3);
            tally.TryCastVote(3, 3);
            tally.TryCastVote(4, VoteTally.AbstainTargetId);

            Assert.That(tally.TryResolveExile(out _), Is.False);
        }

        [Test]
        public void AbstainMajority_ExilesNobody()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, VoteTally.AbstainTargetId);
            tally.TryCastVote(1, VoteTally.AbstainTargetId);
            tally.TryCastVote(2, VoteTally.AbstainTargetId);
            tally.TryCastVote(3, 4);
            tally.TryCastVote(4, 0);

            Assert.That(tally.GetAbstainCount(), Is.EqualTo(3));
            Assert.That(tally.TryResolveExile(out _), Is.False);
        }

        [Test]
        public void AbstainEqualToHighest_ExilesNobody()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, 4);
            tally.TryCastVote(1, 4);
            tally.TryCastVote(2, VoteTally.AbstainTargetId);
            tally.TryCastVote(3, VoteTally.AbstainTargetId);
            tally.TryCastVote(4, 0);

            Assert.That(tally.TryResolveExile(out _), Is.False);
        }

        [Test]
        public void NotVoting_CountsAsAbstain()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, 1);

            // 나머지 4명은 투표하지 않았으므로 기권 4표다.
            Assert.That(tally.GetAbstainCount(), Is.EqualTo(4));
            Assert.That(tally.TryResolveExile(out _), Is.False);
        }

        [Test]
        public void ChangingVote_KeepsOnlyLastChoice()
        {
            var tally = CreateTally();
            tally.TryCastVote(0, 1);
            tally.TryCastVote(0, 2);
            tally.TryCastVote(0, 3);

            Assert.That(tally.CastVoteCount, Is.EqualTo(1));
            Assert.That(tally.GetVoteCount(1), Is.Zero);
            Assert.That(tally.GetVoteCount(3), Is.EqualTo(1));
        }

        [Test]
        public void SelfVote_IsAllowed()
        {
            var tally = CreateTally();

            Assert.That(tally.TryCastVote(2, 2), Is.True);
            Assert.That(tally.GetVoteCount(2), Is.EqualTo(1));
        }

        [Test]
        public void IneligibleVoter_CannotVote()
        {
            var tally = CreateTally();

            Assert.That(tally.TryCastVote(99, 1), Is.False);
            Assert.That(tally.CastVoteCount, Is.Zero);
        }

        [Test]
        public void VoteForIneligibleTarget_IsRejected()
        {
            var tally = CreateTally();

            // 죽었거나 참가하지 않은 대상에게는 투표할 수 없다.
            Assert.That(tally.TryCastVote(0, 99), Is.False);
        }

        [Test]
        public void AbstainVote_IsAlwaysAccepted()
        {
            var tally = CreateTally();

            Assert.That(
                tally.TryCastVote(0, VoteTally.AbstainTargetId),
                Is.True);
        }

        [Test]
        public void NoVotesAtAll_ExilesNobody()
        {
            var tally = CreateTally();

            Assert.That(tally.TryResolveExile(out _), Is.False);
            Assert.That(tally.GetAbstainCount(), Is.EqualTo(5));
        }

        [Test]
        public void SingleVoteWithFewAbstains_StillExiles()
        {
            // 3명 중 2명이 같은 대상에 투표하고 1명만 기권하면 퇴출된다.
            var tally = new VoteTally(new ulong[] { 0, 1, 2 });
            tally.TryCastVote(0, 2);
            tally.TryCastVote(1, 2);
            tally.TryCastVote(2, VoteTally.AbstainTargetId);

            Assert.That(tally.TryResolveExile(out var exiled), Is.True);
            Assert.That(exiled, Is.EqualTo(2ul));
        }

        [Test]
        public void RebindPlayer_PreservesVotingRightsAndVotes()
        {
            var tally = new VoteTally(new ulong[] { 1, 2, 3 });
            tally.TryCastVote(1, 2);
            tally.TryCastVote(2, 2);

            Assert.That(tally.RebindPlayer(2, 20), Is.True);
            Assert.That(tally.IsEligible(2), Is.False);
            Assert.That(tally.IsEligible(20), Is.True);
            Assert.That(tally.TryGetVote(1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(20ul));
            Assert.That(tally.TryGetVote(20, out target), Is.True);
            Assert.That(target, Is.EqualTo(20ul));
        }

        [Test]
        public void RebindPlayer_DoesNotRemoveVoterWhenNewIdAlreadyExists()
        {
            var tally = new VoteTally(new ulong[] { 1, 2, 3 });

            Assert.That(tally.RebindPlayer(1, 2), Is.False);
            Assert.That(tally.IsEligible(1), Is.True);
            Assert.That(tally.EligibleVoterCount, Is.EqualTo(3));
        }
    }
}
