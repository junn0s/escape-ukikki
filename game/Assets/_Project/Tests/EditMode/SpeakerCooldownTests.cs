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
        public void Pause_FreezesRemainingTimeDuringMeeting()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);

            // 120초에 회의가 시작되면 남은 25초가 그대로 멈춘다(GDD §16.2).
            cooldown.SetPaused(true, 120d);

            Assert.That(cooldown.IsPaused, Is.True);
            Assert.That(
                cooldown.GetRemainingSeconds(200d),
                Is.EqualTo(25f).Within(0.001f),
                "회의 중에는 서버 시각이 흘러도 남은 시간이 줄지 않아야 한다.");
            Assert.That(
                cooldown.IsReady(200d),
                Is.False,
                "회의 중 시간 경과만으로 쿨타임이 끝나면 안 된다.");
        }

        [Test]
        public void Resume_ContinuesFromRemainingTimeAfterMeeting()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);
            cooldown.SetPaused(true, 120d);

            // 회의가 125초 동안 이어진 뒤 245초에 탐색이 재개된다.
            cooldown.SetPaused(false, 245d);

            Assert.That(
                cooldown.GetRemainingSeconds(245d),
                Is.EqualTo(25f).Within(0.001f),
                "회의 직후에도 남은 값은 25초여야 한다.");
            Assert.That(cooldown.IsReady(269.9d), Is.False);
            Assert.That(cooldown.IsReady(270d), Is.True);
        }

        [Test]
        public void StartCooldown_DuringMeetingUsesPausedTime()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.SetPaused(true, 300d);

            cooldown.StartCooldown(999d, 45f);

            Assert.That(
                cooldown.GetRemainingSeconds(999d),
                Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void SetPaused_IgnoresRepeatedSameState()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);
            cooldown.SetPaused(true, 120d);
            cooldown.SetPaused(true, 500d);
            cooldown.SetPaused(false, 220d);

            // 두 번째 정지 요청이 무시되므로 정지 기준은 120초다.
            Assert.That(
                cooldown.GetRemainingSeconds(220d),
                Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void Reset_ClearsPausedState()
        {
            var cooldown = new SpeakerCooldownState();
            cooldown.StartCooldown(100d, 45f);
            cooldown.SetPaused(true, 120d);

            cooldown.Reset();

            Assert.That(cooldown.IsPaused, Is.False);
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
