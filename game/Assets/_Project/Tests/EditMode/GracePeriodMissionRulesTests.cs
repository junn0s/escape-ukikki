using System.IO;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 시작 보호 시간에도 미션을 수행할 수 있어야 한다(GDD §6.3).
    ///
    /// 보호는 시작 지점에 괴물이 있을 때를 대비한 안전 장치이고, 미션을 막는
    /// 장치가 아니다. NetworkRoundState는 NetworkBehaviour라 EditMode에서 인스턴스를
    /// 만들 수 없으므로 게이트 조건을 소스 수준에서 고정한다.
    /// </summary>
    public sealed class GracePeriodMissionRulesTests
    {
        private const string RoundStatePath =
            "Assets/_Project/Scripts/Network/NetworkRoundState.cs";

        [Test]
        public void MissionInteraction_IsAllowedDuringGracePeriod()
        {
            var text = File.ReadAllText(RoundStatePath);
            var start = text.IndexOf(
                "public bool AllowsMissionInteraction",
                System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));

            var body = text.Substring(start, 220);
            Assert.That(
                body,
                Does.Contain("RoundPhase.GracePeriod"),
                "보호 시간에 미션이 막히면 시작 지점에서 아무것도 할 수 없다.");
            Assert.That(
                body,
                Does.Contain("RoundPhase.Exploration"),
                "탐색 단계에서도 당연히 허용되어야 한다.");
        }

        [Test]
        public void VillainToolGate_StaysLimitedToExploration()
        {
            // 스피커와 빌런 강화는 보호 시간에 쓸 수 없다(GDD §6.3). 미션 게이트를
            // 함께 쓰면 보호 중에 열려버린다.
            var text = File.ReadAllText(RoundStatePath);
            var start = text.IndexOf(
                "public bool AllowsVillainToolUse",
                System.StringComparison.Ordinal);
            Assert.That(
                start,
                Is.GreaterThanOrEqualTo(0),
                "빌런 도구 게이트가 없다.");

            var body = text.Substring(start, 160);
            Assert.That(
                body,
                Does.Not.Contain("GracePeriod"),
                "보호 시간에 스피커·강화가 열리면 안 된다.");
        }

        [Test]
        public void SpeakerAndUpgrade_UseTheVillainToolGate()
        {
            foreach (var path in new[]
                     {
                         "Assets/_Project/Scripts/Network/" +
                         "NetworkSpeakerAuthority.cs",
                         "Assets/_Project/Scripts/Network/" +
                         "NetworkUpgradeStationAuthority.cs"
                     })
            {
                var text = File.ReadAllText(path);
                Assert.That(
                    text,
                    Does.Contain("AllowsVillainToolUse"),
                    $"{Path.GetFileName(path)}이 미션 게이트를 쓰면 보호 " +
                    "시간에 열린다.");
                Assert.That(
                    text,
                    Does.Not.Contain("AllowsMissionInteraction"),
                    $"{Path.GetFileName(path)}에 미션 게이트가 남아 있다.");
            }
        }
    }
}
