using MonkeyLab.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// GDD 1.7 §12.1 감지 규칙을 고정한다. 손전등과 이동 여부는 감지 조건이
    /// 아니며, 반경 안에 있는지와 감지 대상인지만 판정에 쓴다.
    /// </summary>
    public sealed class MonsterDetectionRuleTests
    {
        private GameObject _targetObject;
        private MonsterTarget _target;

        [SetUp]
        public void SetUp()
        {
            _targetObject = new GameObject("TestMonsterTarget");
            _target = _targetObject.AddComponent<MonsterTarget>();
            _target.Configure(isDetectable: true, canBeInfected: true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_targetObject);
        }

        /// <summary>
        /// 손전등을 끄고 완전히 정지해도 감지된다. 은신 조작은 존재하지 않는다.
        /// </summary>
        [Test]
        public void Proximity_DetectsTargetWithoutFlashlightOrMovement()
        {
            Assert.That(
                _target.CanBeDetectedBy(MonsterDetectionType.Proximity),
                Is.True,
                "GDD 1.6에서 소등·정지 은신은 없어졌다.");
        }

        /// <summary>
        /// 평상시 근접 감지와 소음 현장 급습이 같은 조건을 쓴다.
        /// </summary>
        [Test]
        public void ProximityAndNoiseAmbush_UseSameCondition()
        {
            Assert.That(
                _target.CanBeDetectedBy(MonsterDetectionType.Proximity),
                Is.EqualTo(
                    _target.CanBeDetectedBy(
                        MonsterDetectionType.NoiseAmbush)));
        }

        [Test]
        public void VillainRemainsDetectableButCannotBeBitten()
        {
            _target.SetCanBeBitten(false);

            Assert.That(
                _target.CanBeDetectedBy(MonsterDetectionType.Proximity),
                Is.True);
            Assert.That(_target.CanBeBitten, Is.False);
        }

        /// <summary>
        /// 감염·유령으로 감지 대상에서 빠진 플레이어는 어느 경로로도 감지되지 않는다.
        /// </summary>
        [Test]
        public void UndetectableTarget_IsNeverDetected()
        {
            _target.SetDetectable(false);

            Assert.That(
                _target.CanBeDetectedBy(MonsterDetectionType.Proximity),
                Is.False);
            Assert.That(
                _target.CanBeDetectedBy(MonsterDetectionType.NoiseAmbush),
                Is.False);
        }
    }
}
