using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class MonsterPerceptionTests
    {
        [Test]
        public void TargetInFrontAndInsideVisionConeIsVisible()
        {
            var result = MonsterPerceptionRules.IsWithinVisionCone(
                Vector3.forward,
                new Vector3(0f, 0f, 6f),
                7f,
                100f);

            Assert.That(result, Is.True);
        }

        [Test]
        public void TargetOutsideVisionAngleIsNotVisible()
        {
            var result = MonsterPerceptionRules.IsWithinVisionCone(
                Vector3.forward,
                Vector3.back * 2f,
                7f,
                100f);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TargetOutsideVisionDistanceIsNotVisible()
        {
            var result = MonsterPerceptionRules.IsWithinVisionCone(
                Vector3.forward,
                Vector3.forward * 7.1f,
                7f,
                100f);

            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectionRadiusUsesHorizontalDistance()
        {
            var result = MonsterPerceptionRules.IsWithinRadius(
                Vector3.zero,
                new Vector3(0.3f, 4f, 0.4f),
                0.5f);

            Assert.That(result, Is.True);
        }

        [Test]
        public void BiteProtectionRejectsRepeatUntilDurationEnds()
        {
            var gameObject = new GameObject("Target");
            try
            {
                var target = gameObject.AddComponent<MonsterTarget>();

                Assert.That(target.TryReceiveBite(null, 10f, 1.5f), Is.True);
                Assert.That(target.TryReceiveBite(null, 11.49f, 1.5f), Is.False);
                Assert.That(target.TryReceiveBite(null, 11.5f, 1.5f), Is.True);
                Assert.That(target.BiteCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GracePeriodBlocksAggressionUntilDevelopmentSkip()
        {
            var config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            var gameObject = new GameObject("RoundPhase");
            gameObject.SetActive(false);
            try
            {
                var roundPhase = gameObject.AddComponent<LocalRoundPhasePrototype>();
                roundPhase.Configure(config);
                roundPhase.ResetForRound();

                Assert.That(roundPhase.IsMonsterAggressionEnabled, Is.False);
                Assert.That(roundPhase.RemainingGracePeriodSeconds, Is.GreaterThan(29f));

                roundPhase.SkipGracePeriodForDevelopment();

                Assert.That(roundPhase.IsMonsterAggressionEnabled, Is.True);
                Assert.That(roundPhase.RemainingGracePeriodSeconds, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void MonsterTierRuntimeUpdatesExistingSmellRadiusImmediately()
        {
            var config = ScriptableObject.CreateInstance<MonsterTierConfig>();
            var gameObject = new GameObject("MonsterTierRuntime");
            gameObject.SetActive(false);
            try
            {
                var runtime = gameObject.AddComponent<MonsterTierRuntime>();
                runtime.Configure(config);

                Assert.That(runtime.CurrentSmellRadius, Is.EqualTo(0.5f));

                runtime.SetSmellTier(1);
                Assert.That(runtime.CurrentSmellRadius, Is.EqualTo(1f));

                runtime.SetSmellTier(2);
                Assert.That(runtime.CurrentSmellRadius, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(config);
            }
        }
    }
}
