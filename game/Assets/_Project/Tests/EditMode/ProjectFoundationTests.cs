using System;
using System.IO;
using System.Linq;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class ProjectFoundationTests
    {
        [Test]
        public void UnityVersion_IsPinnedTo6000_3()
        {
            StringAssert.StartsWith("6000.3.", Application.unityVersion);
        }

        [Test]
        public void RenderPipeline_IsConfigured()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
        }

        [Test]
        public void ReleaseScenes_ExistAndAreEnabled()
        {
            var expectedScenes = new[]
            {
                "00_Bootstrap.unity",
                "01_MainMenu.unity",
                "02_Lobby.unity",
                "10_Laboratory.unity"
            };

            var enabledSceneNames = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileName(scene.path))
                .ToArray();

            CollectionAssert.AreEqual(expectedScenes, enabledSceneNames);
        }

        [Test]
        public void PlayerControls_ContainsFirstPlayableActions()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Settings/PlayerControls.inputactions");

            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Move"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Look"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Interact"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Flashlight"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/UseAntidote"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Cancel"), Is.Not.Null);
        }

        [Test]
        public void BootstrapScene_ContainsOnlineServicesStartupFlow()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_Bootstrap.unity");

            var bootstrap = GameObject.Find("[Core] AppBootstrap");
            var bootstrapCamera = Camera.main;
            var statusView = GameObject.Find("[UI] BootstrapStatus")?
                .GetComponent<BootstrapStatusView>();
            Assert.That(bootstrapCamera, Is.Not.Null);
            Assert.That(bootstrapCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.GetComponent<BootstrapEntryPoint>(), Is.Not.Null);
            Assert.That(bootstrap.GetComponent<UnityServicesInitializer>(), Is.Not.Null);
            Assert.That(
                bootstrap.GetComponent<BootstrapEntryPoint>().StartupTaskCount,
                Is.EqualTo(1));
            Assert.That(statusView, Is.Not.Null);
            Assert.That(
                statusView.EntryPoint,
                Is.SameAs(bootstrap.GetComponent<BootstrapEntryPoint>()));
        }

        [Test]
        public void MainMenuScene_ContainsRelaySessionFlow()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");

            var sessionObject = GameObject.Find("[Network] GameSession");
            var sessionView = GameObject.Find("[UI] MainMenuSession")?
                .GetComponent<MainMenuSessionView>();
            Assert.That(sessionObject, Is.Not.Null);

            var networkManager = sessionObject.GetComponent<NetworkManager>();
            var transport = sessionObject.GetComponent<UnityTransport>();
            var controller = sessionObject.GetComponent<GameSessionController>();
            Assert.That(networkManager, Is.Not.Null);
            Assert.That(transport, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(networkManager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
            Assert.That(controller.NetworkManager, Is.SameAs(networkManager));
            Assert.That(controller.Transport, Is.SameAs(transport));
            Assert.That(sessionView, Is.Not.Null);
            Assert.That(sessionView.Controller, Is.SameAs(controller));
        }

        [Test]
        public void LaboratoryScene_ContainsFirstPlayableComponents()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/10_Laboratory.unity");

            var player = GameObject.Find("P_Player_Local");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInputReader>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerAimController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInteractor>(), Is.Not.Null);
            Assert.That(Camera.main.GetComponent<QuarterViewCamera>(), Is.Not.Null);
            var fuseStation = GameObject.Find("MissionStation_Fuse").GetComponent<FuseStationPrototype>();
            Assert.That(fuseStation, Is.Not.Null);
            Assert.That(fuseStation.Config, Is.Not.Null);
            Assert.That(fuseStation.Config.Id, Is.Not.Empty);
            Assert.That(fuseStation.FuseCount, Is.InRange(
                FuseMissionInstance.MinimumFuseCount,
                FuseMissionInstance.MaximumFuseCount));
            Assert.That(GameObject.Find("[UI] FuseMission").GetComponent<FuseMissionView>(), Is.Not.Null);
            Assert.That(
                Vector3.Distance(fuseStation.transform.position, GameObject.Find("Room_Power").transform.position),
                Is.LessThanOrEqualTo(6f),
                "The fuse station must be located in the power room.");
            var noiseService = GameObject.Find("[Gameplay] NoiseService").GetComponent<NoiseService>();
            Assert.That(noiseService, Is.Not.Null);
            Assert.That(noiseService.Config.Id, Is.Not.Empty);
            Assert.That(noiseService.Config.SmallPathRadius, Is.EqualTo(8f));
            Assert.That(noiseService.Config.MediumPathRadius, Is.EqualTo(14f));
            Assert.That(noiseService.Config.LargePathRadius, Is.EqualTo(24f));
            Assert.That(fuseStation.GetComponent<FuseFailureNoiseEmitter>().NoiseService, Is.SameAs(noiseService));
            Assert.That(GameObject.Find("[UI] NoiseAlert").GetComponent<NoiseAlertView>(), Is.Not.Null);

            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")
                .GetComponent<LocalRoundPhasePrototype>();
            Assert.That(roundPhase.Config.Id, Is.EqualTo("round_default"));
            Assert.That(roundPhase.Config.InitialGracePeriodSeconds, Is.EqualTo(30f));
            Assert.That(GameObject.Find("[UI] GracePeriod").GetComponent<GracePeriodView>(), Is.Not.Null);
            Assert.That(GameObject.Find("[UI] MonsterBiteAlert").GetComponent<MonsterBiteAlertView>(), Is.Not.Null);
            var monsterTierRuntime = GameObject.Find("[Gameplay] MonsterTierRuntime")
                .GetComponent<MonsterTierRuntime>();
            Assert.That(monsterTierRuntime.Config.Id, Is.EqualTo("monster_tier_default"));
            Assert.That(monsterTierRuntime.Config.GetSmellRadius(0), Is.EqualTo(0.5f));
            Assert.That(monsterTierRuntime.Config.GetSmellRadius(1), Is.EqualTo(1f));
            Assert.That(monsterTierRuntime.Config.GetSmellRadius(2), Is.EqualTo(2f));
            Assert.That(monsterTierRuntime.Config.GetMonsterCount(0), Is.EqualTo(4));
            Assert.That(monsterTierRuntime.Config.GetMonsterCount(1), Is.EqualTo(6));
            Assert.That(monsterTierRuntime.Config.GetMonsterCount(2), Is.EqualTo(8));
            Assert.That(monsterTierRuntime.Config.GetInfectionDurationSeconds(0), Is.EqualTo(90f));
            Assert.That(monsterTierRuntime.Config.GetInfectionDurationSeconds(1), Is.EqualTo(60f));
            Assert.That(monsterTierRuntime.Config.GetInfectionDurationSeconds(2), Is.EqualTo(30f));
            Assert.That(monsterTierRuntime.ToxicityTier, Is.Zero);
            Assert.That(monsterTierRuntime.CurrentInfectionDurationSeconds, Is.EqualTo(90f));

            var infectionService = player.GetComponent<InfectionService>();
            var antidoteService = player.GetComponent<AntidoteService>();
            Assert.That(infectionService, Is.Not.Null);
            Assert.That(infectionService.State, Is.EqualTo(PlayerLifeState.AliveHealthy));
            Assert.That(antidoteService, Is.Not.Null);
            Assert.That(antidoteService.Config.Id, Is.EqualTo("antidote_default"));
            Assert.That(antidoteService.Config.UseDurationSeconds, Is.EqualTo(1.5f));
            Assert.That(antidoteService.Config.MaxCarryCount, Is.EqualTo(1));
            Assert.That(antidoteService.CarriedCount, Is.Zero);
            Assert.That(GameObject.Find("[UI] InfectionHud").GetComponent<InfectionHudView>(), Is.Not.Null);

            var monster = GameObject.Find("P_Monster_01");
            var monsterBrain = monster.GetComponent<MonsterBrain>();
            var monsterTarget = GameObject.Find("P_Player_Local").GetComponent<MonsterTarget>();
            Assert.That(monster.GetComponent<NavMeshAgent>(), Is.Not.Null);
            Assert.That(monster.GetComponent<MonsterSenses>(), Is.Not.Null);
            Assert.That(monster.GetComponent<MonsterBiteController>(), Is.Not.Null);
            Assert.That(monsterBrain, Is.Not.Null);
            Assert.That(monsterBrain.Config.Id, Is.Not.Empty);
            Assert.That(monsterBrain.Config.PatrolSpeed, Is.EqualTo(2.6f));
            Assert.That(monsterBrain.Config.NoiseInvestigateSpeed, Is.EqualTo(6f));
            Assert.That(monsterBrain.Config.NoiseAccelerationSeconds, Is.EqualTo(6f));
            Assert.That(monsterBrain.Config.SearchSeconds, Is.EqualTo(3f));
            Assert.That(monsterBrain.Config.VisionDistance, Is.EqualTo(7f));
            Assert.That(monsterBrain.Config.VisionAngleDegrees, Is.EqualTo(100f));
            Assert.That(monsterBrain.Config.BiteDistance, Is.EqualTo(0.9f));
            Assert.That(monsterBrain.Config.BiteWindupSeconds, Is.EqualTo(0.35f));
            Assert.That(monsterBrain.Config.BiteRecoverySeconds, Is.EqualTo(1.2f));
            Assert.That(monsterBrain.Config.BiteProtectionSeconds, Is.EqualTo(1.5f));
            Assert.That(monsterBrain.PatrolPointCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(monsterBrain.RoundPhase, Is.SameAs(roundPhase));
            Assert.That(monsterBrain.Senses.TierRuntime, Is.SameAs(monsterTierRuntime));
            Assert.That(monsterBrain.Senses.Target, Is.SameAs(monsterTarget));
            Assert.That(NavMesh.CalculateTriangulation().vertices.Length, Is.GreaterThan(0));
            Assert.That(GameObject.Find("[Map] RoomWalls").transform.childCount, Is.GreaterThanOrEqualTo(20));

            for (var index = 1; index <= 4; index++)
            {
                var monsterSpawnMarker = GameObject.Find($"MonsterSpawn_{index:00}");
                Assert.That(monsterSpawnMarker, Is.Not.Null);
                Assert.That(monsterSpawnMarker.GetComponent<Renderer>(), Is.Null);
                Assert.That(monsterSpawnMarker.GetComponent<Collider>(), Is.Null);
            }

            Physics.SyncTransforms();
            var hasWalkableFloor = Physics.RaycastAll(
                    player.transform.position + Vector3.up * 2f,
                    Vector3.down,
                    4f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore)
                .Any(hit =>
                    hit.collider.gameObject.name.StartsWith("Room_", StringComparison.Ordinal) ||
                    hit.collider.gameObject.name.StartsWith("Corridor_", StringComparison.Ordinal));
            Assert.That(hasWalkableFloor, Is.True, "The local player must spawn above a walkable floor.");
        }
    }
}
