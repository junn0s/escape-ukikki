using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 호스트가 자기 화면에 미션 진행을 반영하는지 소스 수준에서 고정한다.
    ///
    /// 미션 authority가 <c>ApplyReplicatedState</c>를 <c>IsServer</c>로 건너뛰면
    /// 호스트 화면의 진행 표시가 멈춘 채 완료만 처리된다(예: 주사기 폐기가 0/3에서
    /// 멈추는데 미션 목록에는 완료로 뜬다). 호스트는 서버이면서 플레이어다.
    /// </summary>
    public sealed class MissionHostStateMirrorTests
    {
        private const string NetworkRoot =
            "Assets/_Project/Scripts/Network";

        /// <summary>
        /// 서버가 스테이션 상태를 직접 굴리는 authority다. 이쪽은 호스트에서 이미
        /// 진행이 보이고, 복제 상태를 되씌우면 서버 시뮬레이션과 충돌한다.
        /// </summary>
        private static readonly HashSet<string> ServerDrivenAuthorities = new()
        {
            "NetworkFlaskFillAuthority.cs",
            "NetworkFreezerTemperatureAuthority.cs",
            "NetworkInfectionAuthority.cs",
            "NetworkAntidoteFabricatorAuthority.cs",
            "NetworkAntidoteTerminalAuthority.cs"
        };

        private static string[] AuthorityPaths()
        {
            return Directory
                .GetFiles(
                    NetworkRoot,
                    "Network*Authority.cs",
                    SearchOption.TopDirectoryOnly)
                .Where(path =>
                    File.ReadAllText(path)
                        .Contains("private void ApplyReplicatedState()"))
                .ToArray();
        }

        [Test]
        public void MissionAuthorities_MirrorReplicatedStateOnTheHost()
        {
            var paths = AuthorityPaths();
            Assert.That(paths, Is.Not.Empty);

            var offenders = new List<string>();
            foreach (var path in paths)
            {
                var name = Path.GetFileName(path);
                if (ServerDrivenAuthorities.Contains(name))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                var start = text.IndexOf(
                    "private void ApplyReplicatedState()",
                    System.StringComparison.Ordinal);
                var body = text.Substring(
                    start,
                    System.Math.Min(400, text.Length - start));
                if (body.Contains("if (IsServer"))
                {
                    offenders.Add(name);
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "다음 authority가 호스트 화면에 진행을 반영하지 않는다: " +
                string.Join(", ", offenders));
        }

        [Test]
        public void ServerDrivenAuthorities_StillExist()
        {
            // 예외 목록이 낡으면 위 검사가 의미를 잃는다.
            foreach (var name in ServerDrivenAuthorities)
            {
                Assert.That(
                    File.Exists(Path.Combine(NetworkRoot, name)),
                    Is.True,
                    $"{name}이 없다. 예외 목록을 갱신해야 한다.");
            }
        }
    }
}
