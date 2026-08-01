using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class SpeakerCooldownTests
    {
        [Test]
        public void NewState_IsReadyImmediately()
        {
            var cooldown = new SpeakerCooldownState();

            Assert.That(cooldown.IsReady(0d), Is.True);
            Assert.That(cooldown.GetRemainingSeconds(0d), Is.Zero);
        }

        [Test]
        public void StartCooldown_BlocksUseBefore45Seconds()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);

            Assert.That(cooldown.IsReady(100d), Is.False);
            Assert.That(cooldown.IsReady(144.9d), Is.False);
            Assert.That(
                cooldown.GetRemainingSeconds(120d),
                Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void StartCooldown_AllowsUseAtExactly45Seconds()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);

            Assert.That(cooldown.IsReady(145d), Is.True);
            Assert.That(cooldown.GetRemainingSeconds(145d), Is.Zero);
        }

        [Test]
        public void GetRemainingSeconds_NeverNegative()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);

            Assert.That(cooldown.GetRemainingSeconds(999d), Is.Zero);
        }

        [Test]
        public void StartCooldown_RestartsFromLatestUse()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);
            cooldown.StartCooldown(160d, 45f);

            Assert.That(cooldown.IsReady(180d), Is.False);
            Assert.That(cooldown.IsReady(205d), Is.True);
        }

        [Test]
        public void Reset_MakesSpeakerAvailableAgain()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);

            cooldown.Reset();

            Assert.That(cooldown.IsReady(0d), Is.True);
        }

        [Test]
        public void DefaultBalance_MatchesDocumentedValues()
        {
            var config =
                ScriptableObject.CreateInstance<SpeakerBalanceConfig>();
            try
            {
                Assert.That(
                    config.SpeakerCooldownSeconds,
                    Is.EqualTo(45f).Within(0.001f));
                Assert.That(
                    config.SpeakerPlaybackSeconds,
                    Is.EqualTo(3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
