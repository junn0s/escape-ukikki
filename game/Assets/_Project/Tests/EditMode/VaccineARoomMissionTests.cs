using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 백신실 A 생존자 미션(GDD §10.2)의 순수 판정 로직을 검증한다.
    /// </summary>
    public sealed class VaccineARoomMissionTests
    {
        // --- 백신 데이터 다운로드 (누르고 있기) ---

        [Test]
        public void HoldButton_CompletesAfterRequiredSeconds()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();

            Assert.That(rules.Tick(7f, requiredSeconds: 8f), Is.False);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.Tick(1f, requiredSeconds: 8f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void HoldButton_ReleasingResetsProgressToZero()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();
            rules.Tick(5f, requiredSeconds: 8f);

            rules.ReleaseHold();

            Assert.That(
                rules.HeldSeconds,
                Is.Zero,
                "손을 떼면 진행률이 0으로 초기화되어야 한다(GDD §10.2).");
            Assert.That(rules.IsHolding, Is.False);
        }

        [Test]
        public void HoldButton_TickDoesNothingWhenNotHolding()
        {
            var rules = new HoldButtonMissionRules();

            Assert.That(rules.Tick(8f, requiredSeconds: 8f), Is.False);
            Assert.That(rules.HeldSeconds, Is.Zero);
        }

        [Test]
        public void HoldButton_TickDoesNothingAfterCompletion()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();
            rules.Tick(8f, requiredSeconds: 8f);

            var changed = rules.Tick(1f, requiredSeconds: 8f);

            Assert.That(changed, Is.False);
            Assert.That(rules.HeldSeconds, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void HoldButton_ProgressNormalizedClampsToOne()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();
            rules.Tick(8f, requiredSeconds: 8f);

            Assert.That(
                rules.GetProgressNormalized(8f),
                Is.EqualTo(1f).Within(0.001f));
        }

        // --- 오염된 주사기 폐기 (드래그 N개) ---

        [Test]
        public void DragItems_CompletesWhenAllItemsPlaced()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);

            Assert.That(rules.TryPlaceItem(0), Is.True);
            Assert.That(rules.TryPlaceItem(1), Is.True);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.TryPlaceItem(2), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
            Assert.That(rules.PlacedCount, Is.EqualTo(3));
        }

        [Test]
        public void DragItems_RejectsSameItemTwice()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);
            rules.TryPlaceItem(0);

            Assert.That(
                rules.TryPlaceItem(0),
                Is.False,
                "같은 주사기를 두 번 놓을 수 없다.");
            Assert.That(rules.PlacedCount, Is.EqualTo(1));
        }

        [Test]
        public void DragItems_RejectsOutOfRangeIndex()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);

            Assert.That(rules.TryPlaceItem(-1), Is.False);
            Assert.That(rules.TryPlaceItem(3), Is.False);
        }

        [Test]
        public void DragItems_ResetClearsAllPlacedFlags()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);
            rules.TryPlaceItem(0);
            rules.TryPlaceItem(1);

            rules.Reset();

            Assert.That(rules.PlacedCount, Is.Zero);
            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(rules.IsPlaced(0), Is.False);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void SurvivorMissionBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(
                    config.VaccineDataDownloadHoldSeconds,
                    Is.EqualTo(8f).Within(0.001f));
                Assert.That(config.ContaminatedSyringeCount, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
