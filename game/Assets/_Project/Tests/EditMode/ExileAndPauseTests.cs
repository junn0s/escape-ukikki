using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 회의 퇴출로 인한 유령 전환과 회의 중 감염 타이머 정지를 고정한다.
    /// GDD §16.4, SDD §4 상태표.
    /// </summary>
    public sealed class ExileAndPauseTests
    {
        private GameObject _playerObject;
        private InfectionService _infection;
        private MonsterTarget _target;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("ExileTestPlayer");
            _target = _playerObject.AddComponent<MonsterTarget>();
            _target.Configure(isDetectable: true, canBeInfected: true);
            _infection = _playerObject.AddComponent<InfectionService>();
            _infection.Configure(_target, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
        }

        [Test]
        public void PostMeetingProtection_BlocksBiteForConfiguredSeconds()
        {
            var config =
                ScriptableObject.CreateInstance<RoundBalanceConfig>();
            try
            {
                var protectionSeconds =
                    config.PostMeetingBiteProtectionSeconds;
                Assert.That(
                    protectionSeconds,
                    Is.EqualTo(2f).Within(0.001f),
                    "밸런스 §2의 회의 종료 물기 보호는 2초다.");

                _target.ApplyBiteProtection(100f, protectionSeconds);

                Assert.That(_target.IsBiteProtected(101.9f), Is.True);
                Assert.That(
                    _target.TryReceiveBite(null, 101.9f, 1.5f),
                    Is.False,
                    "회의 재개 직후에는 물리지 않아야 한다.");
                Assert.That(_target.IsBiteProtected(102f), Is.False);
                Assert.That(
                    _target.TryReceiveBite(null, 102f, 1.5f),
                    Is.True,
                    "2초가 지나면 다시 물릴 수 있다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ApplyBiteProtection_DoesNotShortenExistingProtection()
        {
            _target.ApplyBiteProtection(100f, 5f);
            _target.ApplyBiteProtection(100f, 2f);

            Assert.That(
                _target.IsBiteProtected(104f),
                Is.True,
                "더 짧은 보호가 기존 보호를 줄이면 안 된다.");
        }

        [Test]
        public void ApplyBiteProtection_IgnoresNonPositiveDuration()
        {
            _target.ApplyBiteProtection(100f, 0f);

            Assert.That(_target.IsBiteProtected(100f), Is.False);
        }

        [Test]
        public void Exile_TurnsHealthySurvivorIntoGhost()
        {
            Assert.That(
                _infection.State,
                Is.EqualTo(PlayerLifeState.AliveHealthy));

            Assert.That(_infection.TryExile(), Is.True);
            Assert.That(
                _infection.State,
                Is.EqualTo(PlayerLifeState.DeadGhost));
        }

        [Test]
        public void Exile_RemovesPlayerFromMonsterDetection()
        {
            _infection.TryExile();

            // 유령은 감지 대상이 아니다(SDD §10.2).
            Assert.That(_target.IsDetectable, Is.False);
        }

        [Test]
        public void Exile_IsIgnoredWhenAlreadyGhost()
        {
            _infection.TryExile();

            Assert.That(_infection.TryExile(), Is.False);
            Assert.That(
                _infection.State,
                Is.EqualTo(PlayerLifeState.DeadGhost));
        }

        [Test]
        public void PausedInfection_DoesNotLoseRemainingTime()
        {
            _infection.ApplyAuthoritativeSnapshot(
                PlayerLifeState.AliveInfected,
                durationAtBiteSeconds: 90f,
                remainingSeconds: 60f,
                toxicityTierAtBite: 0);
            _infection.SetExternallyDriven(false);
            var remainingBefore = _infection.RemainingSeconds;

            _infection.SetPaused(true);
            _infection.Tick(10f);

            Assert.That(
                _infection.RemainingSeconds,
                Is.EqualTo(remainingBefore).Within(0.001f),
                "회의 중에는 감염 타이머가 줄지 않아야 한다.");
        }

        [Test]
        public void ResumedInfection_ContinuesFromSavedValue()
        {
            _infection.ApplyAuthoritativeSnapshot(
                PlayerLifeState.AliveInfected,
                durationAtBiteSeconds: 90f,
                remainingSeconds: 60f,
                toxicityTierAtBite: 0);
            _infection.SetExternallyDriven(false);

            _infection.SetPaused(true);
            _infection.Tick(10f);
            _infection.SetPaused(false);
            _infection.Tick(5f);

            // 정지 중 흐른 10초는 무시되고 재개 후 5초만 반영된다.
            Assert.That(
                _infection.RemainingSeconds,
                Is.EqualTo(55f).Within(0.001f));
        }
    }
}
