using System;
using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class VillainUpgradeStateTests
    {
        [Test]
        public void NewState_StartsAllAxesAtZero()
        {
            var state = new VillainUpgradeState();

            Assert.That(state.ScentLevel, Is.EqualTo(0));
            Assert.That(state.PopulationLevel, Is.EqualTo(0));
            Assert.That(state.ToxicityLevel, Is.EqualTo(0));
            Assert.That(state.TotalUpgradeCount, Is.EqualTo(0));
        }

        [Test]
        public void TryUpgrade_RaisesOnlyTargetAxis()
        {
            var state = new VillainUpgradeState();

            Assert.That(
                state.TryUpgrade(UpgradeAxis.Population, out var newLevel),
                Is.True);
            Assert.That(newLevel, Is.EqualTo(1));
            Assert.That(state.PopulationLevel, Is.EqualTo(1));
            Assert.That(state.ScentLevel, Is.EqualTo(0));
            Assert.That(state.ToxicityLevel, Is.EqualTo(0));
        }

        [Test]
        public void TryUpgrade_StopsAtMaximumLevel()
        {
            var state = new VillainUpgradeState();
            state.TryUpgrade(UpgradeAxis.Scent, out _);
            state.TryUpgrade(UpgradeAxis.Scent, out _);

            Assert.That(state.ScentLevel, Is.EqualTo(2));
            Assert.That(state.CanUpgrade(UpgradeAxis.Scent), Is.False);
            Assert.That(
                state.TryUpgrade(UpgradeAxis.Scent, out var blockedLevel),
                Is.False);
            Assert.That(blockedLevel, Is.EqualTo(2));
        }

        [Test]
        public void TotalUpgradeCount_SumsEveryAxis()
        {
            var state = new VillainUpgradeState();
            state.TryUpgrade(UpgradeAxis.Scent, out _);
            state.TryUpgrade(UpgradeAxis.Population, out _);
            state.TryUpgrade(UpgradeAxis.Population, out _);

            Assert.That(state.TotalUpgradeCount, Is.EqualTo(3));
        }

        [Test]
        public void AxisLevelChanged_RaisedOnlyOnActualChange()
        {
            var state = new VillainUpgradeState();
            var raisedCount = 0;
            state.AxisLevelChanged += (axis, level) => raisedCount++;

            state.TryUpgrade(UpgradeAxis.Toxicity, out _);
            state.TryUpgrade(UpgradeAxis.Toxicity, out _);
            state.TryUpgrade(UpgradeAxis.Toxicity, out _);

            Assert.That(raisedCount, Is.EqualTo(2));
        }

        [Test]
        public void Reset_ReturnsEveryAxisToZero()
        {
            var state = new VillainUpgradeState();
            state.TryUpgrade(UpgradeAxis.Scent, out _);
            state.TryUpgrade(UpgradeAxis.Toxicity, out _);

            state.Reset();

            Assert.That(state.TotalUpgradeCount, Is.EqualTo(0));
        }

        [Test]
        public void SetLevel_RejectsOutOfRangeValues()
        {
            var state = new VillainUpgradeState();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => state.SetLevel(UpgradeAxis.Scent, 3));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => state.SetLevel(UpgradeAxis.Scent, -1));
        }
    }
}
