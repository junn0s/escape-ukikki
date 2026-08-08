using System.IO;
using System.Linq;
using MonkeyLab.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 미션 뷰가 조작 주체를 판정하는 방식을 고정한다.
    ///
    /// 뷰는 씬을 만들 때 프로토타입 플레이어(P_Player_Local)를 받지만, 네트워크
    /// 모드에서는 그 오브젝트가 비활성화되고 실제 조작은 소유 플레이어가 한다.
    /// 필드를 직접 비교하면 Host+Client에서 E를 눌러도 미션 화면이 열리지 않는다.
    /// </summary>
    public sealed class MissionViewLocalPlayerTests
    {
        private const string ViewRoot =
            "Assets/_Project/Scripts/Presentation/UI";

        private static string[] MissionViewPaths()
        {
            return Directory
                .GetFiles(ViewRoot, "*View.cs", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    File.ReadAllText(path).Contains("_localPlayer"))
                .ToArray();
        }

        [Test]
        public void MissionViews_ResolveLocalPlayerAtRuntime()
        {
            var paths = MissionViewPaths();
            Assert.That(paths, Is.Not.Empty, "미션 뷰를 찾지 못했다.");
            foreach (var path in paths)
            {
                Assert.That(
                    File.ReadAllText(path),
                    Does.Contain("LocalGameplayPlayer.Resolve(_localPlayer)"),
                    $"{Path.GetFileName(path)}이 조작 주체를 런타임에 " +
                    "해석하지 않는다.");
            }
        }

        [Test]
        public void MissionViews_DoNotCompareThePrototypeFieldDirectly()
        {
            foreach (var path in MissionViewPaths())
            {
                var text = File.ReadAllText(path);
                Assert.That(
                    text,
                    Does.Not.Contain("interactor == _localPlayer"),
                    $"{Path.GetFileName(path)}이 프로토타입 참조를 직접 " +
                    "비교한다. 네트워크 모드에서 화면이 열리지 않는다.");
            }
        }

        [Test]
        public void Adapter_RebindsAntidoteViewsToTheOwnedService()
        {
            // 해독제 화면은 자기 AntidoteService 상태로 열린다. 어댑터가 소유권 획득
            // 시점에 다시 연결하지 않으면 네트워크 모드에서 열리지 않는다(GDD §14).
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/10_Laboratory.unity");

            var adapter = GameObject.Find("[Network] GameplayScene")
                .GetComponent<
                    MonkeyLab.Presentation.Player.
                    NetworkGameplaySceneAdapter>();
            Assert.That(adapter, Is.Not.Null);
            Assert.That(
                adapter.AntidoteTerminalCount,
                Is.GreaterThanOrEqualTo(1),
                "해독제 터미널 뷰가 어댑터에 연결되지 않았다.");
            Assert.That(
                adapter.AntidoteKeypadCount,
                Is.GreaterThanOrEqualTo(1),
                "해독제 제작대 뷰가 어댑터에 연결되지 않았다.");
        }

        [Test]
        public void Resolve_PrefersOwnedPlayerOverPrototype()
        {
            var prototype = new GameObject("Prototype");
            var owned = new GameObject("Owned");
            try
            {
                LocalGameplayPlayer.Set(null);
                Assert.That(
                    LocalGameplayPlayer.Resolve(prototype),
                    Is.SameAs(prototype),
                    "네트워크가 없으면 프로토타입을 써야 한다.");

                LocalGameplayPlayer.Set(owned);
                Assert.That(
                    LocalGameplayPlayer.Resolve(prototype),
                    Is.SameAs(owned),
                    "소유 플레이어가 있으면 그것을 써야 한다.");
            }
            finally
            {
                LocalGameplayPlayer.Set(null);
                Object.DestroyImmediate(owned);
                Object.DestroyImmediate(prototype);
            }
        }
    }
}
