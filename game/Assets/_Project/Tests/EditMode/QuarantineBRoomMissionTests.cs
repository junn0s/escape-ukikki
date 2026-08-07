using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 격리실 B 생존자 미션(GDD §10.2)과 빌런 위장 미션(§13.2)의
    /// 순수 판정 로직을 검증한다. 배선 복구는 격리실 A와 조작을 공유하므로
    /// QuarantineARoomMissionTests에서 이미 검증했다.
    /// </summary>
    public sealed class QuarantineBRoomMissionTests
    {
        // --- 공기 필터 교체 (낡은 필터 빼고 새 필터 꽂기) ---

        [Test]
        public void SwapFilter_CompletesAfterRemoveThenInstall()
        {
            var rules = new SwapFilterMissionRules();

            Assert.That(rules.TryRemoveOldFilter(), Is.True);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.TryInstallNewFilter(), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void SwapFilter_RejectsInstallingBeforeRemoving()
        {
            var rules = new SwapFilterMissionRules();

            Assert.That(
                rules.TryInstallNewFilter(),
                Is.False,
                "낡은 필터를 먼저 빼지 않으면 새 필터를 꽂을 수 없다.");
            Assert.That(rules.IsNewFilterInstalled, Is.False);
        }

        [Test]
        public void SwapFilter_RejectsRemovingTwice()
        {
            var rules = new SwapFilterMissionRules();
            rules.TryRemoveOldFilter();

            Assert.That(rules.TryRemoveOldFilter(), Is.False);
        }

        [Test]
        public void SwapFilter_RejectsInstallingTwice()
        {
            var rules = new SwapFilterMissionRules();
            rules.TryRemoveOldFilter();
            rules.TryInstallNewFilter();

            Assert.That(rules.TryInstallNewFilter(), Is.False);
        }

        [Test]
        public void SwapFilter_ResetClearsBothSteps()
        {
            var rules = new SwapFilterMissionRules();
            rules.TryRemoveOldFilter();
            rules.TryInstallNewFilter();

            rules.Reset();

            Assert.That(rules.IsOldFilterRemoved, Is.False);
            Assert.That(rules.IsNewFilterInstalled, Is.False);
            Assert.That(rules.IsCompleted, Is.False);
        }

        // --- 환풍구 역류 조작 (빌런 8초 누르기) ---

        [Test]
        public void VentBackflow_SharesHoldButtonRulesWithOtherVillainMissions()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();

            Assert.That(rules.Tick(8f, requiredSeconds: 8f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void VillainMissionKind_VentBackflowIsDistinctFromCultureContamination()
        {
            Assert.That(
                VillainMissionKind.VentBackflow,
                Is.Not.EqualTo(VillainMissionKind.CultureContamination));
        }
    }
}
