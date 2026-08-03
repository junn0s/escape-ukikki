using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/balance-and-telemetry.md §4.1 강화 표와 기본값이 일치하는지 확인한다.
    /// </summary>
    public sealed class UpgradeBalanceTests
    {
        private MonsterTierConfig _tierConfig;
        private UpgradeBalanceConfig _upgradeConfig;

        [SetUp]
        public void SetUp()
        {
            _tierConfig = ScriptableObject.CreateInstance<MonsterTierConfig>();
            _upgradeConfig =
                ScriptableObject.CreateInstance<UpgradeBalanceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tierConfig);
            Object.DestroyImmediate(_upgradeConfig);
        }

        [Test]
        public void MonsterCount_MatchesBalanceTable()
        {
            Assert.That(_tierConfig.GetMonsterCount(0), Is.EqualTo(4));
            Assert.That(_tierConfig.GetMonsterCount(1), Is.EqualTo(6));
            Assert.That(_tierConfig.GetMonsterCount(2), Is.EqualTo(8));
        }

        [Test]
        public void ProximityDetectionRadius_MatchesBalanceTable()
        {
            Assert.That(
                _tierConfig.GetProximityDetectionRadius(0),
                Is.EqualTo(5f).Within(0.001f));
            Assert.That(
                _tierConfig.GetProximityDetectionRadius(1),
                Is.EqualTo(7f).Within(0.001f));
            Assert.That(
                _tierConfig.GetProximityDetectionRadius(2),
                Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void InfectionDuration_MatchesBalanceTable()
        {
            Assert.That(
                _tierConfig.GetInfectionDurationSeconds(0),
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                _tierConfig.GetInfectionDurationSeconds(1),
                Is.EqualTo(60f).Within(0.001f));
            Assert.That(
                _tierConfig.GetInfectionDurationSeconds(2),
                Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void UpgradeMissionSeconds_StayInsideBalanceRange()
        {
            foreach (UpgradeAxis axis in
                     System.Enum.GetValues(typeof(UpgradeAxis)))
            {
                var seconds =
                    _upgradeConfig.GetUpgradeMissionSeconds(axis);
                Assert.That(
                    seconds,
                    Is.InRange(
                        _upgradeConfig.UpgradeMissionMinimumSeconds,
                        _upgradeConfig.UpgradeMissionMaximumSeconds),
                    $"{axis} upgrade duration is outside 12~18s.");
            }
        }

        [Test]
        public void UpgradeMissionSeconds_DefaultRangeIsTwelveToEighteen()
        {
            Assert.That(
                _upgradeConfig.UpgradeMissionMinimumSeconds,
                Is.EqualTo(12f).Within(0.001f));
            Assert.That(
                _upgradeConfig.UpgradeMissionMaximumSeconds,
                Is.EqualTo(18f).Within(0.001f));
        }

        [Test]
        public void MonsterSpawnWarning_IsThreeSeconds()
        {
            Assert.That(
                _upgradeConfig.MonsterSpawnWarningSeconds,
                Is.EqualTo(3f).Within(0.001f));
        }
    }
}
