using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 입원실 생존자 미션 2종(GDD §10.2)과 빌런 위장 미션(§13.2)의
    /// 순수 판정 로직을 검증한다.
    /// </summary>
    public sealed class WardRoomMissionTests
    {
        // --- 수액 속도 조절 (왕복 슬라이더 타이밍 정지) ---

        [Test]
        public void TimingStop_CompletesWithinTargetRange()
        {
            var rules = new TimingStopMissionRules(
                targetMinNormalized: 0.42f,
                targetMaxNormalized: 0.58f,
                cycleSeconds: 2f);

            // cycleSeconds 2초(왕복 전체)에서 절반 지점 t=0.5초 → 위치 0.5(목표 중앙)
            Assert.That(rules.TryStop(0.5f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void TimingStop_RejectsOutsideTargetRange()
        {
            var rules = new TimingStopMissionRules(
                targetMinNormalized: 0.42f,
                targetMaxNormalized: 0.58f,
                cycleSeconds: 2f);

            // t=0 → 위치 0(목표 밖)
            Assert.That(rules.TryStop(0f), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void TimingStop_PositionPingPongsBetweenZeroAndOne()
        {
            var rules = new TimingStopMissionRules(
                targetMinNormalized: 0.42f,
                targetMaxNormalized: 0.58f,
                cycleSeconds: 2f);

            // cycleSeconds는 0→1→0으로 돌아오는 전체 왕복 시간이다.
            Assert.That(
                rules.GetPositionNormalized(0f),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                rules.GetPositionNormalized(1f),
                Is.EqualTo(1f).Within(0.001f),
                "절반 주기(t=cycleSeconds/2)에서 반대쪽 끝(위치 1)에 도달해야 한다.");
            Assert.That(
                rules.GetPositionNormalized(2f),
                Is.EqualTo(0f).Within(0.001f),
                "전체 왕복(t=cycleSeconds)이 끝나면 다시 0으로 돌아와야 한다.");
        }

        [Test]
        public void TimingStop_CannotStopTwice()
        {
            var rules = new TimingStopMissionRules(
                targetMinNormalized: 0.42f,
                targetMaxNormalized: 0.58f,
                cycleSeconds: 2f);
            rules.TryStop(0.5f);

            Assert.That(rules.TryStop(0.5f), Is.False);
        }

        [Test]
        public void TimingStop_ResetAllowsRetrying()
        {
            var rules = new TimingStopMissionRules(
                targetMinNormalized: 0.42f,
                targetMaxNormalized: 0.58f,
                cycleSeconds: 2f);
            rules.TryStop(0.5f);

            rules.Reset();

            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(rules.TryStop(0.5f), Is.True);
        }

        // --- 환자 바이탈 기록 (숫자 코드 입력) ---

        [Test]
        public void NumericCode_CompletesOnExactMatch()
        {
            var rules = new NumericCodeMissionRules("4821");

            Assert.That(rules.TrySubmit("4821"), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void NumericCode_RejectsWrongCode()
        {
            var rules = new NumericCodeMissionRules("4821");

            Assert.That(rules.TrySubmit("1234"), Is.False);
            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void NumericCode_CannotSubmitAfterCompletion()
        {
            var rules = new NumericCodeMissionRules("4821");
            rules.TrySubmit("4821");

            Assert.That(rules.TrySubmit("4821"), Is.False);
        }

        [Test]
        public void NumericCode_ResetAllowsRetrying()
        {
            var rules = new NumericCodeMissionRules("4821");
            rules.TrySubmit("4821");

            rules.Reset();

            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(rules.TrySubmit("4821"), Is.True);
        }

        // --- 투약 기록 삭제 (빌런, 드래그 N개 재사용) ---

        [Test]
        public void MedicationRecordWipe_ReusesDragItemsRulesForThreeFolders()
        {
            var rules = new DragItemsMissionRules(itemCount: 3);

            rules.TryPlaceItem(0);
            rules.TryPlaceItem(1);
            rules.TryPlaceItem(2);

            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void VillainMissionKind_MedicationRecordWipeIsDistinctKind()
        {
            Assert.That(
                VillainMissionKind.MedicationRecordWipe,
                Is.Not.EqualTo(VillainMissionKind.CultureContamination));
            Assert.That(
                VillainMissionKind.MedicationRecordWipe,
                Is.Not.EqualTo(VillainMissionKind.VentBackflow));
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void WardBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(
                    config.IvDripTargetHalfWidthNormalized,
                    Is.EqualTo(0.08f).Within(0.001f));
                Assert.That(
                    config.IvDripCycleSeconds,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(config.PatientVitalsCodeLength, Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
