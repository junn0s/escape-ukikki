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
