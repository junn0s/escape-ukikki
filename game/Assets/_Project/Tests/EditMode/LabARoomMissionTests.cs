using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 실험실 A 생존자 미션 2종(GDD §10.2)과 빌런 스택형 강화(§13.2~13.3)의
    /// 순수 판정 로직을 검증한다.
    /// </summary>
    public sealed class LabARoomMissionTests
    {
        // --- 슬라이드 글라스 닦기 (얼룩 문지르기) ---

        [Test]
        public void ScrubStains_CompletesWhenAllStainsReachRequiredScrubs()
        {
            var rules = new ScrubStainsMissionRules(
                stainCount: 3,
                requiredScrubsPerStain: 5);

            for (var scrub = 0; scrub < 5; scrub++)
            {
                rules.TryScrub(0);
            }

            Assert.That(rules.IsClean(0), Is.True);
            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void ScrubStains_RejectsScrubbingAlreadyCleanStain()
        {
            var rules = new ScrubStainsMissionRules(
                stainCount: 1,
                requiredScrubsPerStain: 2);
            rules.TryScrub(0);
            rules.TryScrub(0);

            Assert.That(
                rules.TryScrub(0),
                Is.False,
                "이미 지워진 얼룩은 다시 문질러도 반응하지 않아야 한다.");
            Assert.That(rules.GetScrubCount(0), Is.EqualTo(2));
        }

        [Test]
        public void ScrubStains_ResetClearsAllScrubCounts()
        {
            var rules = new ScrubStainsMissionRules(
                stainCount: 2,
                requiredScrubsPerStain: 3);
            rules.TryScrub(0);
            rules.TryScrub(1);

            rules.Reset();

            Assert.That(rules.CleanedCount, Is.Zero);
            Assert.That(rules.GetScrubCount(0), Is.Zero);
        }

        // --- 시약병 분류 (색상 맞춰 드래그) ---

        [Test]
        public void SortReagents_CompletesWhenAllPlacedInTargetBins()
        {
            var rules = new SortReagentsMissionRules(
                targetBinIndices: new[] { 0, 1, 2 });

            Assert.That(rules.TrySort(0, 0), Is.True);
            Assert.That(rules.TrySort(1, 1), Is.True);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.TrySort(2, 2), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void SortReagents_RejectsWrongBin()
        {
            var rules = new SortReagentsMissionRules(
                targetBinIndices: new[] { 0, 1, 2 });

            Assert.That(
                rules.TrySort(0, 1),
                Is.False,
                "목표 칸과 다르면 거부해야 한다.");
            Assert.That(rules.IsSorted(0), Is.False);
        }

        [Test]
        public void SortReagents_RejectsAlreadySortedReagent()
        {
            var rules = new SortReagentsMissionRules(
                targetBinIndices: new[] { 0, 1, 2 });
            rules.TrySort(0, 0);

            Assert.That(rules.TrySort(0, 0), Is.False);
        }

        // --- 빌런 미션 배정 (GDD §13.2) ---

        [Test]
        public void MissionAssignment_PicksFourOfSixKinds()
        {
            var assigned = VillainMissionAssignmentService.Assign(seed: 4242);

            Assert.That(assigned.Length, Is.EqualTo(4));

            var distinctCount = new System.Collections.Generic.HashSet<
                VillainMissionKind>(assigned).Count;
            Assert.That(
                distinctCount,
                Is.EqualTo(4),
                "빌런 미션 4개는 서로 달라야 한다.");
        }

        [Test]
        public void MissionAssignment_IsDeterministicForTheSameSeed()
        {
            var first = VillainMissionAssignmentService.Assign(seed: 1234);
            var second = VillainMissionAssignmentService.Assign(seed: 1234);

            Assert.That(first, Is.EqualTo(second));
        }

        // --- 스택형 강화 (GDD §13.3) ---

        [Test]
        public void MissionClearState_IncrementsUpToMaximum()
        {
            var state = new VillainMissionClearState();

            for (var index = 0; index < 4; index++)
            {
                Assert.That(state.TryIncrement(out var level), Is.True);
                Assert.That(level, Is.EqualTo(index + 1));
            }

            Assert.That(
                state.TryIncrement(out _),
                Is.False,
                "빌런에게 배정되는 미션은 4개뿐이라 5회째는 없어야 한다.");
        }

        [Test]
        public void StackEffectRules_MatchesGddTable()
        {
            Assert.That(
                VillainMissionStackEffectRules.GetPopulationTier(0),
                Is.EqualTo(0));
            Assert.That(
                VillainMissionStackEffectRules.GetPopulationTier(1),
                Is.EqualTo(1),
                "1회 클리어 시 괴물 6마리(tier 1)여야 한다.");
            Assert.That(
                VillainMissionStackEffectRules.GetPopulationTier(2),
                Is.EqualTo(2),
                "2회 클리어 시 괴물 8마리(tier 2)여야 한다.");

            Assert.That(
                VillainMissionStackEffectRules.GetToxicityTier(2),
                Is.EqualTo(0),
                "3회 미만에는 독성 효과가 없어야 한다.");
            Assert.That(
                VillainMissionStackEffectRules.GetToxicityTier(3),
                Is.EqualTo(1),
                "3회 클리어 시 감염 40초(tier 1)여야 한다.");

            Assert.That(
                VillainMissionStackEffectRules.GetProximityDetectionTier(3),
                Is.EqualTo(0),
                "4회 미만에는 근접 감지 효과가 없어야 한다.");
            Assert.That(
                VillainMissionStackEffectRules.GetProximityDetectionTier(4),
                Is.EqualTo(1),
                "4회 클리어 시 근접 감지 1.75m(tier 1)여야 한다.");
        }

        // --- 빌런 8초 누르기 (배양액 오염시키기) ---

        [Test]
        public void VillainHoldButton_CompletesAfterEightSeconds()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();

            Assert.That(rules.Tick(7f, requiredSeconds: 8f), Is.False);
            Assert.That(rules.Tick(1f, requiredSeconds: 8f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void VillainHoldButton_ReleasingResetsProgress()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();
            rules.Tick(4f, requiredSeconds: 8f);

            rules.ReleaseHold();

            Assert.That(rules.HeldSeconds, Is.Zero);
        }
    }
}
