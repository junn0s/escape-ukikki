using System;
using System.IO;
using System.Linq;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Audio;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.Player;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
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
        public void LaboratoryMapTexture_IsProjectOwnedStaticImage()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Resources/UI/T_LaboratoryMap.png");

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(1024));
            Assert.That(texture.height, Is.GreaterThanOrEqualTo(720));
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
            var lobbyObject = GameObject.Find("[Network] LobbyRoster");
            var sessionView = GameObject.Find("[UI] MainMenuSession")?
                .GetComponent<MainMenuSessionView>();
            Assert.That(sessionObject, Is.Not.Null);
            Assert.That(lobbyObject, Is.Not.Null);

            var networkManager = sessionObject.GetComponent<NetworkManager>();
            var transport = sessionObject.GetComponent<UnityTransport>();
            var controller = sessionObject.GetComponent<GameSessionController>();
            var lobbyRoster = lobbyObject.GetComponent<LobbyRosterNetwork>();
            Assert.That(networkManager, Is.Not.Null);
            Assert.That(transport, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(lobbyObject.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(lobbyRoster, Is.Not.Null);
            Assert.That(networkManager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
            Assert.That(networkManager.NetworkConfig.PlayerPrefab, Is.Not.Null);
            Assert.That(
                networkManager.NetworkConfig.ConnectionApproval,
                Is.True,
                "Unity PlayerId 연결 승인 없이는 새 clientId 재접속을 복원할 수 없다.");
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>(),
                Is.Not.Null);
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkPlayerAvatar>(),
                Is.Not.Null);
            var networkPlayerAvatar = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<NetworkPlayerAvatar>();
            Assert.That(
                networkPlayerAvatar.RoleReadPermission,
                Is.EqualTo(NetworkVariableReadPermission.Owner));
            Assert.That(
                networkPlayerAvatar.RoleWritePermission,
                Is.EqualTo(NetworkVariableWritePermission.Server));
            Assert.That(
                networkPlayerAvatar.MonsterTarget,
                Is.SameAs(
                    networkManager.NetworkConfig.PlayerPrefab
                        .GetComponent<MonsterTarget>()));
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkPlayerPresentation>(),
                Is.Not.Null);
            var networkPlayerInteractor = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<PlayerInteractor>();
            var networkPlayerAim = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<PlayerAimController>();
            var networkPlayerTarget = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<MonsterTarget>();
            var networkPlayerInfection = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<InfectionService>();
            var networkInfectionAuthority =
                networkManager.NetworkConfig.PlayerPrefab
                    .GetComponent<NetworkInfectionAuthority>();
            var networkMissionJournal =
                networkManager.NetworkConfig.PlayerPrefab
                    .GetComponent<NetworkPlayerMissionJournal>();
            var networkPlayerAntidote = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<AntidoteService>();
            var networkPlayerPresentation = networkManager.NetworkConfig.PlayerPrefab
                .GetComponent<NetworkPlayerPresentation>();
            Assert.That(networkPlayerInteractor, Is.Not.Null);
            Assert.That(networkPlayerAim, Is.Not.Null);
            Assert.That(networkPlayerTarget, Is.Not.Null);
            Assert.That(networkPlayerInfection, Is.Not.Null);
            Assert.That(networkInfectionAuthority, Is.Not.Null);
            Assert.That(
                networkInfectionAuthority.InfectionService,
                Is.SameAs(networkPlayerInfection));
            Assert.That(networkMissionJournal, Is.Not.Null);
            Assert.That(
                networkMissionJournal.ReadPermission,
                Is.EqualTo(NetworkVariableReadPermission.Owner));
            Assert.That(
                networkMissionJournal.WritePermission,
                Is.EqualTo(NetworkVariableWritePermission.Server));
            Assert.That(
                networkMissionJournal.ActivityReadPermission,
                Is.EqualTo(NetworkVariableReadPermission.Everyone));
            Assert.That(networkPlayerAntidote, Is.Not.Null);
            Assert.That(
                networkPlayerPresentation.IsOwnerOnlyBehaviour(
                    networkPlayerInteractor),
                Is.True);
            Assert.That(
                networkPlayerPresentation.IsOwnerOnlyBehaviour(networkPlayerAim),
                Is.True);
            Assert.That(
                networkPlayerPresentation.IsOwnerOnlyBehaviour(
                    networkPlayerAntidote),
                Is.True);
            Assert.That(
                networkPlayerPresentation.Body,
                Is.SameAs(
                    networkManager.NetworkConfig.PlayerPrefab
                        .GetComponent<Rigidbody2D>()));
            Assert.That(
                networkPlayerPresentation.Interactor,
                Is.SameAs(networkPlayerInteractor));
            Assert.That(
                networkPlayerPresentation.MissionJournal,
                Is.SameAs(networkMissionJournal));
            Assert.That(networkPlayerPresentation.Aim, Is.SameAs(networkPlayerAim));
            Assert.That(
                networkPlayerPresentation.MonsterTarget,
                Is.SameAs(networkPlayerTarget));
            Assert.That(
                networkPlayerPresentation.InfectionService,
                Is.SameAs(networkPlayerInfection));
            Assert.That(
                networkPlayerPresentation.AntidoteService,
                Is.SameAs(networkPlayerAntidote));
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<Rigidbody2D>(),
                Is.Not.Null);
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<CapsuleCollider2D>(),
                Is.Not.Null);
            Assert.That(
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<CharacterController>(),
                Is.Null);
            Assert.That(controller.NetworkManager, Is.SameAs(networkManager));
            Assert.That(controller.Transport, Is.SameAs(transport));
            Assert.That(sessionView, Is.Not.Null);
            Assert.That(sessionView.Controller, Is.SameAs(controller));
            Assert.That(sessionView.LobbyRoster, Is.SameAs(lobbyRoster));
        }

        [Test]
        public void NetworkGameplayModeKeepsSystemsAndDisablesOnlyLocalPlayer()
        {
            var gameplayRoot = new GameObject("[Prototype] FirstPlayable");
            var localPlayer = new GameObject("P_Player_Local");
            var adapterObject = new GameObject("[Network] GameplayScene");
            localPlayer.transform.SetParent(gameplayRoot.transform);
            try
            {
                var adapter =
                    adapterObject.AddComponent<NetworkGameplaySceneAdapter>();
                adapter.Configure(gameplayRoot, localPlayer);

                adapter.ApplyMode(isNetworkMode: true);

                Assert.That(gameplayRoot.activeSelf, Is.True);
                Assert.That(localPlayer.activeSelf, Is.False);

                adapter.ApplyMode(isNetworkMode: false);

                Assert.That(gameplayRoot.activeSelf, Is.True);
                Assert.That(localPlayer.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(adapterObject);
                UnityEngine.Object.DestroyImmediate(gameplayRoot);
            }
        }

        [Test]
        public void RebindingSameTopDownCameraTargetDoesNotSnapCamera()
        {
            var cameraObject = new GameObject("Camera");
            var targetObject = new GameObject("Target");
            try
            {
                cameraObject.AddComponent<UnityEngine.Camera>();
                var topDownCamera =
                    cameraObject.AddComponent<TopDownCamera>();
                targetObject.transform.position =
                    new Vector3(8f, 4f, 0f);
                topDownCamera.Configure(
                    targetObject.transform,
                    TopDownCamera.DefaultOrthographicSize);

                var positionBeforeRebind =
                    new Vector3(3f, 2f, -10f);
                cameraObject.transform.position = positionBeforeRebind;
                topDownCamera.Configure(
                    targetObject.transform,
                    TopDownCamera.DefaultOrthographicSize);

                Assert.That(
                    cameraObject.transform.position,
                    Is.EqualTo(positionBeforeRebind));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void VaccineMissionEquipmentSprites_AreImported()
        {
            var spritePaths = new[]
            {
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_AntidoteCodeTerminal.png",
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_AntidoteFabricator.png",
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_ContaminatedSyringeDisposal.png",
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_FreezerTemperature.png",
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_VaccineDataDownload.png",
                "Assets/_Project/Art/Sprites/Missions/Vaccine/S_VaccineSampleScanner.png"
            };

            foreach (var spritePath in spritePaths)
            {
                Assert.That(File.Exists(spritePath), Is.True, spritePath);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Sprite>(spritePath),
                    Is.Not.Null,
                    spritePath);
            }
        }

        [Test]
        public void LaboratoryScene_ContainsFirstPlayableComponents()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/10_Laboratory.unity");

            var player = GameObject.Find("P_Player_Local");
            var networkAdapter = GameObject.Find("[Network] GameplayScene")?
                .GetComponent<NetworkGameplaySceneAdapter>();
            Assert.That(player, Is.Not.Null);
            Assert.That(networkAdapter, Is.Not.Null);
            Assert.That(networkAdapter.LocalPrototypeRoot.name, Is.EqualTo("[Prototype] FirstPlayable"));
            Assert.That(networkAdapter.LocalPlayer, Is.SameAs(player));
            Assert.That(networkAdapter.MonsterTierRuntime, Is.Not.Null);
            Assert.That(networkAdapter.InfectionHud, Is.Not.Null);
            Assert.That(networkAdapter.MonsterBiteAlert, Is.Not.Null);
            Assert.That(networkAdapter.InteractionPrompt, Is.Not.Null);
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(player.GetComponent<CapsuleCollider2D>(), Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>(), Is.Null);
            Assert.That(player.GetComponent<PlayerInputReader>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerAimController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInteractor>(), Is.Not.Null);
            Assert.That(
                player.GetComponent<Rigidbody2D>().constraints &
                RigidbodyConstraints2D.FreezeRotation,
                Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(
                player.transform.Find("VisualRoot/AimPivot/FlashlightCone"),
                Is.Not.Null);
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(Camera.main.GetComponent<TopDownCamera>(), Is.Not.Null);
            Assert.That(GameObject.Find("MissionStation_Power"), Is.Null);
            Assert.That(GameObject.Find("MissionBatteryReceiver_Ward"), Is.Null);
            Assert.That(GameObject.Find("[UI] FuseMission"), Is.Null);
            var legacyMissionStations =
                UnityEngine.Object.FindObjectsByType<FuseStationPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(legacyMissionStations, Is.Empty);
            foreach (var equipmentName in new[]
                     {
                         "VaccineDataDownload", "ContaminatedSyringes",
                         "FreezerTemperature", "VaccineSampleScan",
                         "AntidoteTerminal_A", "AntidoteTerminal_B",
                         "AntidoteFabricator_A", "AntidoteFabricator_B"
                     })
            {
                var equipment = GameObject.Find(equipmentName);
                Assert.That(equipment, Is.Not.Null, equipmentName);
                Assert.That(
                    equipment.transform.Find("FinalEquipmentVisual")?
                        .GetComponent<SpriteRenderer>()?.sprite,
                    Is.Not.Null,
                    equipmentName);
                Assert.That(
                    equipment.GetComponent<InteractableHighlight>(),
                    Is.Not.Null,
                    equipmentName);
            }

            var roomIds = new[]
            {
                "VaccineA", "LabA", "QuarantineA", "Storage",
                "Security", "Power", "Ward", "LabB",
                "QuarantineB", "VaccineB"
            };
            var roomFloorColors = roomIds
                .Select(
                    roomId => GameObject.Find("Room_" + roomId)
                        .GetComponent<SpriteRenderer>().color)
                .ToArray();
            Assert.That(
                roomFloorColors.Distinct().Count(),
                Is.EqualTo(roomIds.Length),
                "Every room floor must keep a distinct low-saturation tint.");
            var noiseService = GameObject.Find("[Gameplay] NoiseService").GetComponent<NoiseService>();
            Assert.That(noiseService, Is.Not.Null);
            Assert.That(noiseService.Config.Id, Is.Not.Empty);
            Assert.That(noiseService.Config.SmallPathRadius, Is.EqualTo(12f));
            Assert.That(noiseService.Config.MediumPathRadius, Is.EqualTo(30f));
            Assert.That(noiseService.Config.LargePathRadius, Is.EqualTo(40f));
            Assert.That(GameObject.Find("[UI] NoiseAlert").GetComponent<NoiseAlertView>(), Is.Not.Null);

            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")
                .GetComponent<LocalRoundPhasePrototype>();
            Assert.That(roundPhase.Config.Id, Is.EqualTo("round_default"));
            Assert.That(roundPhase.Config.RoleRevealSeconds, Is.EqualTo(7f));
            Assert.That(roundPhase.Config.InitialGracePeriodSeconds, Is.EqualTo(30f));
            Assert.That(
                roundPhase.Config.ExplorationDurationSeconds,
                Is.EqualTo(900f));
            Assert.That(
                roundPhase.Config.ProjectMaximumPoints,
                Is.EqualTo(10000));
            Assert.That(
                roundPhase.Config.SurvivorPersonalBudgetPoints,
                Is.EqualTo(2000));
            var networkRound = GameObject.Find("[Network] RoundState")
                .GetComponent<NetworkRoundState>();
            Assert.That(networkRound, Is.Not.Null);
            Assert.That(networkRound.Config, Is.SameAs(roundPhase.Config));
            Assert.That(networkRound.MissionStationCount, Is.Zero);
            Assert.That(
                networkRound.SurvivorMissionStationCount,
                Is.EqualTo(22));
            Assert.That(
                networkRound.GetComponent<NetworkObject>(),
                Is.Not.Null);
            Assert.That(
                GameObject.Find("[UI] RoundHud")
                    .GetComponent<RoundHudView>(),
                Is.Not.Null);
            Assert.That(GameObject.Find("[UI] GracePeriod").GetComponent<GracePeriodView>(), Is.Not.Null);
            Assert.That(GameObject.Find("[UI] MonsterBiteAlert").GetComponent<MonsterBiteAlertView>(), Is.Not.Null);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<
                    NetworkFuseStationAuthority>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            var survivorMissionAuthorities = UnityEngine.Object
                .FindObjectsByType<NetworkSurvivorMissionAuthority>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(survivorMissionAuthorities, Has.Length.EqualTo(22));
            Assert.That(
                survivorMissionAuthorities
                    .Select(authority => authority.Kind)
                    .Distinct()
                    .Count(),
                Is.EqualTo(22));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<
                    MissionStationNetworkPresenter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            var patrolRoutes =
                GameObject.Find("[AI] MonsterPatrolRoutes");
            Assert.That(patrolRoutes, Is.Not.Null);
            Assert.That(
                patrolRoutes.transform.childCount,
                Is.EqualTo(4));
            for (var routeIndex = 0;
                 routeIndex < patrolRoutes.transform.childCount;
                 routeIndex++)
            {
                Assert.That(
                    patrolRoutes.transform
                        .GetChild(routeIndex).childCount,
                    Is.EqualTo(3));
            }
            var monsterTierRuntime = GameObject.Find("[Gameplay] MonsterTierRuntime")
                .GetComponent<MonsterTierRuntime>();
            Assert.That(monsterTierRuntime.Config.Id, Is.EqualTo("monster_tier_default"));
            Assert.That(
                monsterTierRuntime.Config.GetProximityDetectionRadius(0),
                Is.EqualTo(1.25f));
            Assert.That(
                monsterTierRuntime.Config.GetProximityDetectionRadius(1),
                Is.EqualTo(1.75f));
            Assert.That(
                monsterTierRuntime.Config.GetProximityDetectionRadius(2),
                Is.EqualTo(2.25f));
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

            // 기본 4마리는 활성, 개체 강화 예비 4마리는 비활성으로 대기한다.
            var monsters = UnityEngine.Object.FindObjectsByType<MonsterBrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(monsters, Has.Length.EqualTo(8));
            Assert.That(
                monsters.Count(item => item.gameObject.activeInHierarchy),
                Is.EqualTo(4),
                "라운드 시작 시 활성 괴물은 4마리여야 한다.");
            var monster = GameObject.Find("P_Monster_01");
            var monsterBrain = monster.GetComponent<MonsterBrain>();
            var monsterTarget = GameObject.Find("P_Player_Local").GetComponent<MonsterTarget>();
            Assert.That(monster.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(monster.GetComponent<CapsuleCollider2D>(), Is.Not.Null);
            Assert.That(monster.GetComponent<MonsterSenses>(), Is.Not.Null);
            Assert.That(monster.GetComponent<MonsterBiteController>(), Is.Not.Null);
            Assert.That(
                monster.GetComponent<Rigidbody2D>().constraints &
                RigidbodyConstraints2D.FreezeRotation,
                Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(monsterBrain, Is.Not.Null);
            Assert.That(monsterBrain.Config.Id, Is.Not.Empty);
            Assert.That(monsterBrain.Config.PatrolSpeed, Is.EqualTo(2.6f));
            Assert.That(monsterBrain.Config.NoiseInvestigateSpeed, Is.EqualTo(6f));
            Assert.That(monsterBrain.Config.NoiseAccelerationSeconds, Is.EqualTo(6f));
            // 밸런스 §4 "수색 시간 5초"
            Assert.That(monsterBrain.Config.SearchSeconds, Is.EqualTo(5f));
            Assert.That(
                monsterBrain.Config.MissionFailureAmbushRadius,
                Is.EqualTo(5.333333f).Within(0.0001f));
            Assert.That(monsterBrain.Config.SpeakerAmbushRadius, Is.EqualTo(8f));
            Assert.That(monsterBrain.Config.ForcedNoiseRoamSeconds, Is.EqualTo(10f));
            Assert.That(monsterBrain.Config.BiteDistance, Is.EqualTo(0.2f));
            Assert.That(monsterBrain.Config.BiteWindupSeconds, Is.EqualTo(0.35f));
            Assert.That(monsterBrain.Config.BiteRecoverySeconds, Is.EqualTo(1.2f));
            Assert.That(monsterBrain.Config.BiteProtectionSeconds, Is.EqualTo(1.5f));
            Assert.That(monsterBrain.PatrolPointCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(monsterBrain.RoundPhase, Is.SameAs(roundPhase));
            Assert.That(monsterBrain.Senses.TierRuntime, Is.SameAs(monsterTierRuntime));
            Assert.That(monsterBrain.Senses.Target, Is.SameAs(monsterTarget));
            Assert.That(monsterBrain.NavigationGraph, Is.Not.Null);
            Assert.That(monsterBrain.NavigationGraph.NodeCount, Is.GreaterThan(30));
            Assert.That(monsterBrain.NavigationGraph.LinkCount, Is.GreaterThan(35));
            foreach (var spawnedMonster in monsters)
            {
                Assert.That(spawnedMonster.Config, Is.SameAs(monsterBrain.Config));
                Assert.That(spawnedMonster.RoundPhase, Is.SameAs(roundPhase));
                Assert.That(spawnedMonster.NavigationGraph, Is.SameAs(monsterBrain.NavigationGraph));
                Assert.That(spawnedMonster.PatrolPointCount, Is.EqualTo(3));
                var networkAuthority =
                    spawnedMonster.GetComponent<NetworkMonsterAuthority>();
                Assert.That(networkAuthority, Is.Not.Null);
                Assert.That(networkAuthority.Brain, Is.SameAs(spawnedMonster));
                Assert.That(networkAuthority.Body, Is.Not.Null);
                Assert.That(networkAuthority.NetworkTransform, Is.Not.Null);
                Assert.That(
                    spawnedMonster.GetComponent<NetworkObject>(),
                    Is.Not.Null);
            }
            Assert.That(
                GameObject.Find("[Map] CollisionWalls")
                    .GetComponentsInChildren<BoxCollider2D>().Length,
                Is.GreaterThanOrEqualTo(20));

            // 축 선택형 강화 스테이션은 GDD 1.9에서 빌런 전용 미션 6종 +
            // 스택형 강화로 대체됐다(§13.2~13.3). 방별 위장 미션 배치는
            // ValidateLabARoomMissions 등 FirstPlayableBuilder의 방별 검증
            // 함수가 담당하므로, 여기서는 스택 권위와 개체 수 스포너만 확인한다.
            var stackAuthority =
                GameObject.Find("[Network] VillainMissionStackAuthority")
                    .GetComponent<NetworkVillainMissionStackAuthority>();
            Assert.That(stackAuthority, Is.Not.Null);
            var populationSpawner =
                GameObject.Find("[Network] MonsterPopulationSpawner")
                    .GetComponent<NetworkMonsterPopulationSpawner>();
            Assert.That(populationSpawner, Is.Not.Null);
            Assert.That(populationSpawner.TierConfig, Is.Not.Null);
            // 배치된 괴물 수가 밸런스 표(4/6/8)와 실제로 일치해야 한다.
            Assert.That(
                populationSpawner.MatchesBalanceTable(0),
                Is.True,
                "기본 단계 괴물 수가 SO_MonsterTier와 다르다.");
            Assert.That(
                populationSpawner.MatchesBalanceTable(1),
                Is.True,
                "1단계 강화 괴물 수가 SO_MonsterTier와 다르다.");
            Assert.That(
                populationSpawner.MatchesBalanceTable(2),
                Is.True,
                "2단계 강화 괴물 수가 SO_MonsterTier와 다르다.");
            Assert.That(populationSpawner.BaseMonsterCount, Is.EqualTo(4));

            // 실험실 A의 빌런 위장 미션 배치는 GDD §13.2를 따라야 한다.
            var villainStation = UnityEngine.Object
                .FindObjectsByType<VillainHoldButtonStation>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(
                    station =>
                        station.Kind ==
                        VillainMissionKind.CultureContamination);
            Assert.That(villainStation, Is.Not.Null);
            Assert.That(villainStation.RoomId, Is.EqualTo("LabA"));
            Assert.That(
                villainStation.GetComponent<
                    NetworkVillainHoldButtonAuthority>(),
                Is.Not.Null);

            var clueMarkers =
                UnityEngine.Object.FindObjectsByType<ClueMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            // 강화 단서 6개(종류별 2개) + 방마다 스피커 LED 10개
            Assert.That(clueMarkers, Has.Length.EqualTo(16));
            Assert.That(
                clueMarkers.Count(
                    marker => marker.Kind == ClueKind.SpeakerRedLed),
                Is.EqualTo(10),
                "스피커 LED 마커는 방마다 하나씩 있어야 한다.");
            foreach (var kind in new[]
                     {
                         ClueKind.VentRedSmoke,
                         ClueKind.BrokenQuarantineLock,
                         ClueKind.EmptySyringe
                     })
            {
                Assert.That(
                    clueMarkers.Count(marker => marker.Kind == kind),
                    Is.EqualTo(2),
                    $"{kind} 마커는 2개여야 한다.");
            }

            // 단서 ID는 고유해야 하고, 시작 시에는 모두 비활성이어야 한다.
            Assert.That(
                clueMarkers.Select(marker => marker.ClueId).Distinct().Count(),
                Is.EqualTo(clueMarkers.Length));
            Assert.That(
                clueMarkers.All(marker => !marker.IsActive),
                Is.True,
                "라운드 시작 시 단서는 모두 비활성이어야 한다.");

            // 단서가 남는 방이 실제 강화 행동과 논리적으로 이어져야 한다.
            var smokeRooms = clueMarkers
                .Where(marker => marker.Kind == ClueKind.VentRedSmoke)
                .Select(marker => marker.RoomId);
            Assert.That(smokeRooms, Is.EquivalentTo(new[] { "LabB", "LabA" }));
            var lockRooms = clueMarkers
                .Where(marker => marker.Kind == ClueKind.BrokenQuarantineLock)
                .Select(marker => marker.RoomId);
            Assert.That(
                lockRooms,
                Is.EquivalentTo(new[] { "QuarantineA", "QuarantineB" }));
            var syringeRooms = clueMarkers
                .Where(marker => marker.Kind == ClueKind.EmptySyringe)
                .Select(marker => marker.RoomId);
            Assert.That(
                syringeRooms,
                Is.EquivalentTo(new[] { "VaccineB", "VaccineA" }));

            var clueAuthority =
                GameObject.Find("[Network] ClueAuthority")
                    .GetComponent<NetworkClueAuthority>();
            Assert.That(clueAuthority, Is.Not.Null);
            Assert.That(clueAuthority.MarkerCount, Is.EqualTo(16));

            var speakerAuthority =
                GameObject.Find("[Network] SpeakerAuthority")
                    .GetComponent<NetworkSpeakerAuthority>();
            Assert.That(speakerAuthority, Is.Not.Null);
            Assert.That(speakerAuthority.Config, Is.Not.Null);
            // 스피커는 10개 방 모두에 있어야 빌런이 어디든 유도할 수 있다.
            Assert.That(speakerAuthority.SpeakerCount, Is.EqualTo(10));

            var speakers =
                UnityEngine.Object.FindObjectsByType<SpeakerPlacement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(
                speakers.Select(speaker => speaker.RoomId).Distinct().Count(),
                Is.EqualTo(10),
                "방마다 스피커가 하나씩이어야 한다.");
            // LED 단서가 실제 스피커가 있는 방에만 있어야 추리가 성립한다.
            var ledRooms = clueMarkers
                .Where(marker => marker.Kind == ClueKind.SpeakerRedLed)
                .Select(marker => marker.RoomId);
            Assert.That(
                ledRooms,
                Is.EquivalentTo(speakers.Select(speaker => speaker.RoomId)));

            Assert.That(
                GameObject.Find("[UI] SpeakerRemote")
                    .GetComponent<SpeakerRemoteView>(),
                Is.Not.Null);

            Assert.That(
                GameObject.Find("[Network] MeetingAuthority")
                    .GetComponent<NetworkMeetingAuthority>(),
                Is.Not.Null);
            Assert.That(
                GameObject.Find("[UI] Meeting")
                    .GetComponent<MeetingView>(),
                Is.Not.Null);

            var securityTerminal =
                GameObject.Find("[Network] SecurityTerminalAuthority")
                    .GetComponent<NetworkSecurityTerminalAuthority>();
            Assert.That(securityTerminal, Is.Not.Null);
            // 프로젝트 50% 전에는 잠겨 있어야 한다.
            Assert.That(securityTerminal.IsUnlocked, Is.False);
            Assert.That(
                GameObject.Find("[UI] SecurityTerminal")
                    .GetComponent<SecurityTerminalView>(),
                Is.Not.Null);

            for (var index = 1; index <= 4; index++)
            {
                var monsterSpawnMarker = GameObject.Find($"MonsterSpawn_{index:00}");
                Assert.That(monsterSpawnMarker, Is.Not.Null);
                Assert.That(monsterSpawnMarker.GetComponent<Renderer>(), Is.Null);
                Assert.That(monsterSpawnMarker.GetComponent<Collider>(), Is.Null);
                Assert.That(monsterSpawnMarker.GetComponent<Collider2D>(), Is.Null);
            }

            var hasWalkableFloor = GameObject.Find("[Map] Laboratory2D")
                .GetComponentsInChildren<SpriteRenderer>()
                .Any(renderer =>
                    (renderer.name.StartsWith("Room_", StringComparison.Ordinal) ||
                     renderer.name.StartsWith("Corridor_", StringComparison.Ordinal)) &&
                    renderer.bounds.Contains(player.transform.position));
            Assert.That(
                hasWalkableFloor,
                Is.True,
                "The local player must spawn on a 2D room or corridor floor.");
        }
    }
}
