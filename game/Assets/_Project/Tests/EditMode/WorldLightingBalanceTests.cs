using MonkeyLab.Gameplay.Application;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/balance-and-telemetry.md §7.4 프로젝트 복구 조명 표와
    /// 기본값이 일치하는지 확인한다.
    /// </summary>
    public sealed class WorldLightingBalanceTests
    {
        private const float StealthBreakingDarkLuminance = 0.15f;

        private WorldLightingBalanceConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config =
                ScriptableObject.CreateInstance<WorldLightingBalanceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void DarkGlobalIntensity_MatchesBalanceTable()
        {
            Assert.That(
                _config.DarkGlobalIntensityRatio,
                Is.EqualTo(0.12f).Within(0.0001f));
        }

        /// <summary>
        /// Light2D는 강도와 색을 곱한다. 색조가 어두우면 강도를 올려도 검게
        /// 눌리므로 밸런스 판단은 실효 휘도로 한다(밸런스 §7.4).
        /// </summary>
        [Test]
        public void EffectiveDarkLuminance_MatchesBalanceTable()
        {
            Assert.That(
                _config.EffectiveDarkLuminance,
                Is.EqualTo(0.07f).Within(0.01f));
        }

        /// <summary>
        /// 실효 휘도가 0이면 화면이 완전 암흑이 되어 방향 감각을 잃는다.
        /// </summary>
        [Test]
        public void EffectiveDarkLuminance_IsVisible()
        {
            Assert.That(
                _config.EffectiveDarkLuminance,
                Is.GreaterThan(0.02f));
        }

        [Test]
        public void RestoredLightIntensity_MatchesBalanceTable()
        {
            Assert.That(
                _config.RestoredLightIntensityRatio,
                Is.EqualTo(0.15f).Within(0.0001f));
        }

        /// <summary>
        /// GDD 1.6부터 손전등은 감지 조건이 아니므로 상한의 근거는 스텔스가 아니다.
        /// 실효 암부 밝기가 15%에 도달하면 손전등 없이도 월드가 읽혀 탐색 긴장과
        /// 손전등의 존재 이유가 사라진다. 이 선을 넘지 않게 막는다(밸런스 §7.4).
        /// </summary>
        [Test]
        public void EffectiveDarkLuminance_StaysBelowStealthBreakingThreshold()
        {
            Assert.That(
                _config.EffectiveDarkLuminance,
                Is.LessThan(StealthBreakingDarkLuminance));
        }

        /// <summary>
        /// 단계 복구 광원은 전역광보다 밝아야 진행 보상이 인지된다.
        /// </summary>
        [Test]
        public void RestoredLightIntensity_ExceedsDarkGlobalIntensity()
        {
            Assert.That(
                _config.RestoredLightIntensityRatio,
                Is.GreaterThan(_config.DarkGlobalIntensityRatio));
        }
    }
}
