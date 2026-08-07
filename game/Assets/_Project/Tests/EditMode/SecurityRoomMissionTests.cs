using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 중앙 보안 광장 생존자 미션 2종(GDD §10.2)과 빌런 위장 미션(§13.2)의
    /// 순수 판정 로직을 검증한다.
    /// </summary>
    public sealed class SecurityRoomMissionTests
    {
        // --- ID 카드 긁기 (속도 판정) ---

        [Test]
        public void SwipeSpeed_CompletesWithinRange()
        {
            var rules = new SwipeSpeedMissionRules(
                minDurationSeconds: 0.4f,
                maxDurationSeconds: 1.2f);

            Assert.That(rules.TrySwipe(0.8f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void SwipeSpeed_RejectsTooFast()
        {
            var rules = new SwipeSpeedMissionRules(
                minDurationSeconds: 0.4f,
                maxDurationSeconds: 1.2f);

            Assert.That(rules.TrySwipe(0.1f), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(rules.FailedAttemptCount, Is.EqualTo(1));
        }

        [Test]
        public void SwipeSpeed_RejectsTooSlow()
        {
            var rules = new SwipeSpeedMissionRules(
                minDurationSeconds: 0.4f,
                maxDurationSeconds: 1.2f);

            Assert.That(rules.TrySwipe(2f), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void SwipeSpeed_CannotRetryAfterCompletion()
        {
            var rules = new SwipeSpeedMissionRules(
                minDurationSeconds: 0.4f,
                maxDurationSeconds: 1.2f);
            rules.TrySwipe(0.8f);

            Assert.That(rules.TrySwipe(0.8f), Is.False);
        }

        [Test]
        public void SwipeSpeed_ResetClearsFailuresAndCompletion()
        {
            var rules = new SwipeSpeedMissionRules(
                minDurationSeconds: 0.4f,
                maxDurationSeconds: 1.2f);
            rules.TrySwipe(0.1f);

            rules.Reset();

            Assert.That(rules.FailedAttemptCount, Is.Zero);
            Assert.That(rules.IsCompleted, Is.False);
        }

        // --- CCTV 화면 닦기 (누적 진행률) ---

        [Test]
        public void ScrubProgress_CompletesAfterRequiredScrubs()
        {
            var rules = new ScrubProgressMissionRules(requiredScrubs: 10);

            for (var index = 0; index < 9; index++)
            {
                Assert.That(rules.TryScrub(), Is.False);
            }

            Assert.That(rules.TryScrub(), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void ScrubProgress_ProgressNormalizedTracksScrubCount()
        {
            var rules = new ScrubProgressMissionRules(requiredScrubs: 10);
            rules.TryScrub();
            rules.TryScrub();

            Assert.That(
                rules.GetProgressNormalized(),
                Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void ScrubProgress_CannotScrubAfterCompletion()
        {
            var rules = new ScrubProgressMissionRules(requiredScrubs: 2);
            rules.TryScrub();
            rules.TryScrub();

            Assert.That(rules.TryScrub(), Is.False);
        }

        // --- 보안 카메라 선 꼬기 (색 무관, 모두 단락 단자로) ---

        [Test]
        public void TangleWires_CompletesWhenAllWiresPlugged()
        {
            var rules = new TangleWiresMissionRules(wireCount: 4);

            rules.TryPlug(0);
            rules.TryPlug(1);
            rules.TryPlug(2);
            Assert.That(rules.IsCompleted, Is.False);

            rules.TryPlug(3);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void TangleWires_AcceptsAnyWireRegardlessOfColor()
        {
            // WireConnectMissionRules와 달리 색 구분이 없다 — 인덱스만으로 판정한다.
            var rules = new TangleWiresMissionRules(wireCount: 4);

            Assert.That(rules.TryPlug(2), Is.True);
            Assert.That(rules.TryPlug(0), Is.True);
        }

        [Test]
        public void TangleWires_RejectsAlreadyPluggedWire()
        {
            var rules = new TangleWiresMissionRules(wireCount: 4);
            rules.TryPlug(0);

            Assert.That(rules.TryPlug(0), Is.False);
        }

        [Test]
        public void VillainMissionKind_SecurityWireTangleIsDistinctKind()
        {
            Assert.That(
                VillainMissionKind.SecurityWireTangle,
                Is.Not.EqualTo(VillainMissionKind.CultureContamination));
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void SecurityBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(
                    config.IdCardSwipeMinSeconds,
                    Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(
                    config.IdCardSwipeMaxSeconds,
                    Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(config.CctvScreenScrubCount, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
