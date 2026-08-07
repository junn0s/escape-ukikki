using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 전력 복구실 생존자 미션 2종(GDD §10.2)과 빌런 위장 미션(§13.2)의
    /// 순수 판정 로직을 검증한다. 세 미션 모두 다른 방에서 이미 검증한
    /// 판정 클래스를 재사용하므로, 재사용 자체가 올바른지에 집중한다.
    /// </summary>
    public sealed class PowerRoomMissionTests
    {
        // --- 차단기 올리기 (DragItemsMissionRules 재사용, 4개 클릭) ---

        [Test]
        public void CircuitBreaker_CompletesWhenAllFourSwitchesFlipped()
        {
            var rules = new DragItemsMissionRules(itemCount: 4);

            rules.TryPlaceItem(0);
            rules.TryPlaceItem(1);
            rules.TryPlaceItem(2);
            Assert.That(rules.IsCompleted, Is.False);

            rules.TryPlaceItem(3);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void CircuitBreaker_RejectsFlippingSameSwitchTwice()
        {
            var rules = new DragItemsMissionRules(itemCount: 4);
            rules.TryPlaceItem(0);

            Assert.That(rules.TryPlaceItem(0), Is.False);
        }

        // --- 퓨즈 교체 (SwapFilterMissionRules 재사용, 뽑기 → 꽂기) ---

        [Test]
        public void FuseSwap_CompletesAfterRemoveThenInstall()
        {
            var rules = new SwapFilterMissionRules();

            Assert.That(rules.TryRemoveOldFilter(), Is.True);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.TryInstallNewFilter(), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void FuseSwap_RejectsInstallingBeforeRemoving()
        {
            var rules = new SwapFilterMissionRules();

            Assert.That(
                rules.TryInstallNewFilter(),
                Is.False,
                "탄 퓨즈를 먼저 뽑지 않으면 새 퓨즈를 꽂을 수 없다.");
        }

        // --- 메인 전력선 절단 (빌런, DragItemsMissionRules 재사용, 3개 클릭) ---

        [Test]
        public void PowerLineCut_CompletesWhenAllThreeWiresCut()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);

            rules.TryPlaceItem(0);
            rules.TryPlaceItem(1);
            Assert.That(rules.IsCompleted, Is.False);

            rules.TryPlaceItem(2);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void VillainMissionKind_MainPowerLineCutIsDistinctKind()
        {
            Assert.That(
                VillainMissionKind.MainPowerLineCut,
                Is.Not.EqualTo(VillainMissionKind.SecurityWireTangle));
        }

        // --- 빌런 6종 전체 배정 가능성 확인 ---

        [Test]
        public void MissionAssignment_CanIncludeMainPowerLineCut()
        {
            // 6종 중 4종을 배정하므로 시드에 따라 포함되지 않을 수도 있다.
            // 여러 시드를 시도해 적어도 한 번은 포함되는지 확인한다.
            var found = false;
            for (var seed = 1; seed <= 50 && !found; seed++)
            {
                var assigned = VillainMissionAssignmentService.Assign(seed);
                found = System.Array.IndexOf(
                    assigned,
                    VillainMissionKind.MainPowerLineCut) >= 0;
            }

            Assert.That(
                found,
                Is.True,
                "메인 전력선 절단이 배정 후보에서 완전히 배제되면 안 된다.");
        }
    }
}
