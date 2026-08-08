using MonkeyLab.Network;
using MonkeyLab.Presentation.VFX;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 실험실 씬을 바로 재생했을 때 쓰는 1인 연습 모드 구성을 확인한다.
    /// 세션이 없으면 미션이 배정되지 않아 E 상호작용을 확인할 수 없다.
    /// </summary>
    public sealed class DeveloperSoloSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/10_Laboratory.unity");
        }

        [Test]
        public void SoloSession_IsWiredToAnInactiveNetworkRoot()
        {
            var session = GameObject.Find("[Dev] SoloSession")?
                .GetComponent<DeveloperSoloSession>();
            Assert.That(session, Is.Not.Null);
            Assert.That(session.SoloNetworkRoot, Is.Not.Null);

            // 활성 상태로 두면 정상 세션으로 실험실에 들어올 때 메인 메뉴의
            // NetworkManager와 싱글턴이 충돌한다.
            Assert.That(
                session.SoloNetworkRoot.activeSelf,
                Is.False,
                "연습용 NetworkManager는 비활성이어야 한다.");
        }

        [Test]
        public void SoloNetworkRoot_HasPlayerPrefabAndTransport()
        {
            var session = GameObject.Find("[Dev] SoloSession")
                .GetComponent<DeveloperSoloSession>();
            var manager = session.SoloNetworkRoot
                .GetComponent<NetworkManager>();
            Assert.That(manager, Is.Not.Null);
            Assert.That(
                manager.NetworkConfig.NetworkTransport,
                Is.Not.Null);
            Assert.That(
                manager.NetworkConfig.PlayerPrefab,
                Is.Not.Null,
                "플레이어 프리팹이 없으면 연습 모드에서 스폰되지 않는다.");
        }

        [Test]
        public void LocalPlayer_HasAssignedMissionHighlightDriver()
        {
            var player = GameObject.Find("P_Player_Local");
            Assert.That(player, Is.Not.Null);
            Assert.That(
                player.GetComponent<AssignedMissionHighlightDriver>(),
                Is.Not.Null,
                "배정된 미션 강조 드라이버가 없다.");
        }

        [Test]
        public void NetworkPlayerPrefab_HasAssignedMissionHighlightDriver()
        {
            // 네트워크 모드에서는 P_Player_Local이 비활성화되므로 프리팹 쪽에
            // 없으면 배정 강조가 아예 돌지 않는다.
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Players/P_Player_Network.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponent<AssignedMissionHighlightDriver>(),
                Is.Not.Null,
                "네트워크 플레이어 프리팹에 배정 강조 드라이버가 없다.");
        }

        [Test]
        public void MissionStations_HaveHighlightAndMissionAuthority()
        {
            // 강조 판정은 NetworkSurvivorMissionAuthority의 MissionId로 대조한다.
            var missions = Object
                .FindObjectsByType<NetworkSurvivorMissionAuthority>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(missions, Is.Not.Empty);
            foreach (var mission in missions)
            {
                Assert.That(
                    mission.GetComponent<InteractableHighlight>(),
                    Is.Not.Null,
                    $"{mission.name}에 테두리 강조가 없다.");
            }
        }
    }
}
