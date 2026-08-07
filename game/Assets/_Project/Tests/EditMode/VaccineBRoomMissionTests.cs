using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 백신실 B 생존자 미션 2종(GDD §10.2)의 순수 판정 로직을 검증한다.
    /// 이 방에는 빌런 위장 미션이 배정되지 않는다.
    /// </summary>
    public sealed class VaccineBRoomMissionTests
    {
        // --- 냉동고 온도 조절 (위/아래로 목표 온도에 맞추고 유지) ---

        [Test]
        public void FreezerTemperature_CompletesAfterHoldingAtTarget()
        {
            var rules = new FreezerTemperatureMissionRules(
                targetTemperature: -20,
                minTemperature: -30,
                maxTemperature: 10);
            for (var i = 0; i < 20; i++)
            {
                rules.Adjust(-1);
            }

            Assert.That(rules.IsAtTarget, Is.True);
            Assert.That(rules.Tick(1.5f, 3f), Is.False);
            Assert.That(rules.Tick(1.5f, 3f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void FreezerTemperature_ResetsHoldWhenDriftingOffTarget()
        {
            var rules = new FreezerTemperatureMissionRules(
                targetTemperature: -20,
                minTemperature: -30,
                maxTemperature: 10);
            for (var i = 0; i < 20; i++)
            {
                rules.Adjust(-1);
            }

            rules.Tick(2f, 3f);
            rules.Adjust(1); // -19도로 이탈

            Assert.That(rules.IsAtTarget, Is.False);
            Assert.That(
                rules.HeldSecondsAtTarget,
                Is.Zero,
                "목표 온도를 벗어나면 유지 시간이 초기화되어야 한다.");
        }

        [Test]
        public void FreezerTemperature_ClampsWithinConfiguredRange()
        {
            var rules = new FreezerTemperatureMissionRules(
                targetTemperature: -20,
                minTemperature: -5,
                maxTemperature: 10);

            for (var i = 0; i < 50; i++)
            {
                rules.Adjust(-1);
            }

            Assert.That(rules.CurrentTemperature, Is.EqualTo(-5));
        }

        [Test]
        public void FreezerTemperature_CannotAdjustAfterCompletion()
        {
            var rules = new FreezerTemperatureMissionRules(
                targetTemperature: -20,
                minTemperature: -30,
                maxTemperature: 10);
            for (var i = 0; i < 20; i++)
            {
                rules.Adjust(-1);
            }

            rules.Tick(3f, 3f);
            Assert.That(rules.IsCompleted, Is.True);

            rules.Adjust(5);

            Assert.That(rules.CurrentTemperature, Is.EqualTo(-20));
        }

        // --- 백신 샘플 스캔 (순서대로 스캔) ---

        [Test]
        public void SampleScan_CompletesWhenScannedInOrder()
        {
            var rules = new VaccineSampleScanMissionRules(sampleCount: 3);

            Assert.That(rules.TryScan(0), Is.True);
            Assert.That(rules.TryScan(1), Is.True);
            Assert.That(rules.IsCompleted, Is.False);
            Assert.That(rules.TryScan(2), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void SampleScan_RejectsOutOfOrderScan()
        {
            var rules = new VaccineSampleScanMissionRules(sampleCount: 3);

            Assert.That(rules.TryScan(1), Is.False);
            Assert.That(rules.ScannedCount, Is.Zero);
        }

        [Test]
        public void SampleScan_RejectsRepeatedScanOfSameSample()
        {
            var rules = new VaccineSampleScanMissionRules(sampleCount: 3);
            rules.TryScan(0);

            Assert.That(rules.TryScan(0), Is.False);
            Assert.That(rules.ScannedCount, Is.EqualTo(1));
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void VaccineBBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(config.FreezerTargetTemperature, Is.EqualTo(-20));
                Assert.That(config.FreezerHoldSeconds, Is.EqualTo(3f));
                Assert.That(config.VaccineSampleCount, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
