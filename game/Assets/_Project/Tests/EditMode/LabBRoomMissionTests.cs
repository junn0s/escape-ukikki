using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 실험실 B 생존자 미션 3종(GDD §10.2)의 순수 판정 로직을 검증한다.
    /// 이 방에는 빌런 위장 미션이 배정되지 않는다.
    /// </summary>
    public sealed class LabBRoomMissionTests
    {
        // --- 현미경 렌즈 초점 (밀어 올려 구간에서 확정) ---

        [Test]
        public void SliderToRange_CompletesWhenConfirmedInsideTarget()
        {
            var rules = new SliderToRangeMissionRules(
                targetMinNormalized: 0.55f,
                targetMaxNormalized: 0.7f);
            rules.Push(0.6f);

            Assert.That(rules.TryConfirm(), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void SliderToRange_RejectsConfirmingOutsideTarget()
        {
            var rules = new SliderToRangeMissionRules(
                targetMinNormalized: 0.55f,
                targetMaxNormalized: 0.7f);
            rules.Push(0.3f);

            Assert.That(rules.TryConfirm(), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void SliderToRange_ClampsPositionToOne()
        {
            var rules = new SliderToRangeMissionRules(
                targetMinNormalized: 0.55f,
                targetMaxNormalized: 0.7f);

            rules.Push(1.5f);

            Assert.That(rules.PositionNormalized, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SliderToRange_CannotPushAfterCompletion()
        {
            var rules = new SliderToRangeMissionRules(
                targetMinNormalized: 0.55f,
                targetMaxNormalized: 0.7f);
            rules.Push(0.6f);
            rules.TryConfirm();

            rules.Push(0.1f);

            Assert.That(
                rules.PositionNormalized,
                Is.EqualTo(0.6f).Within(0.001f),
                "완료 후에는 슬라이더가 더 움직이면 안 된다.");
        }

        // --- 플라스크 용액 채우기 (게이지 채우고 목표 구간에서 손 떼기) ---

        [Test]
        public void FillGauge_CompletesWhenReleasedInsideTargetRange()
        {
            var rules = new FillGaugeMissionRules(
                targetMinNormalized: 0.9f,
                targetMaxNormalized: 1f,
                fillDurationSeconds: 4f);
            rules.BeginHold();

            rules.Tick(3.7f); // 92.5%

            Assert.That(rules.ReleaseHold(), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void FillGauge_FailsAndResetsWhenReleasedTooEarly()
        {
            var rules = new FillGaugeMissionRules(
                targetMinNormalized: 0.9f,
                targetMaxNormalized: 1f,
                fillDurationSeconds: 4f);
            rules.BeginHold();

            rules.Tick(2f); // 50%

            Assert.That(rules.ReleaseHold(), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(
                rules.FilledSeconds,
                Is.Zero,
                "목표 구간 밖에서 손을 떼면 진행률이 초기화되어야 한다.");
        }

        [Test]
        public void FillGauge_MarksOverfilledWhenReachingFullWithoutRelease()
        {
            var rules = new FillGaugeMissionRules(
                targetMinNormalized: 0.9f,
                targetMaxNormalized: 1f,
                fillDurationSeconds: 4f);
            rules.BeginHold();

            rules.Tick(5f);

            Assert.That(rules.IsOverfilled, Is.True);
        }

        [Test]
        public void FillGauge_CannotReleaseWithoutHolding()
        {
            var rules = new FillGaugeMissionRules(
                targetMinNormalized: 0.9f,
                targetMaxNormalized: 1f,
                fillDurationSeconds: 4f);

            Assert.That(rules.ReleaseHold(), Is.False);
        }

        // --- 실험용 쥐 케이지 잠그기 (DragItemsMissionRules 재사용, 4개 클릭) ---

        [Test]
        public void RatCageLock_CompletesWhenAllFourLocksClicked()
        {
            var rules = new DragItemsMissionRules(itemCount: 4);

            rules.TryPlaceItem(0);
            rules.TryPlaceItem(1);
            rules.TryPlaceItem(2);
            Assert.That(rules.IsCompleted, Is.False);

            rules.TryPlaceItem(3);
            Assert.That(rules.IsCompleted, Is.True);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void LabBBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(
                    config.MicroscopeFocusTargetMinNormalized,
                    Is.EqualTo(0.55f).Within(0.001f));
                Assert.That(
                    config.MicroscopeFocusTargetMaxNormalized,
                    Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(
                    config.FlaskFillTargetMinNormalized,
                    Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(
                    config.FlaskFillDurationSeconds,
                    Is.EqualTo(4f).Within(0.001f));
                Assert.That(config.RatCageLockCount, Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
