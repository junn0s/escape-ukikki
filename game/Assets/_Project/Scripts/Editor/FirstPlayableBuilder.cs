using System;
using System.Collections.Generic;
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
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    public static class FirstPlayableBuilder
    {
        private const string LaboratoryScenePath =
            "Assets/_Project/Scenes/10_Laboratory.unity";
        private const string InputActionsPath =
            "Assets/_Project/Settings/PlayerControls.inputactions";
        private const string MovementConfigPath =
            "Assets/_Project/Data/Balance/SO_PlayerMovement_Default.asset";
        private const string FuseMissionConfigPath =
            "Assets/_Project/Data/Missions/SO_FuseMission_Default.asset";
        private const string NoiseBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_NoiseBalance_Default.asset";
        private const string MonsterBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_MonsterBalance_Default.asset";
        private const string MonsterTierConfigPath =
            "Assets/_Project/Data/Balance/SO_MonsterTier_Default.asset";
        private const string AntidoteBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_AntidoteBalance_Default.asset";
        private const string RoundBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_RoundBalance_Default.asset";
        private const string InteractionBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_InteractionBalance_Default.asset";
        private const string UpgradeBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_UpgradeBalance_Default.asset";
        private const string SpeakerBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_SpeakerBalance_Default.asset";
        private const string SpriteRoot =
            "Assets/_Project/Art/Sprites/Generated";
        private const string CharacterSpriteRoot =
            "Assets/_Project/Art/Sprites/Characters";
        private const string UnitSpritePath = SpriteRoot + "/S_UnitSquare.asset";
        private const string PlayerSpritePath =
            CharacterSpriteRoot + "/S_Player_Survivor.png";
        private const string VisorSpritePath = SpriteRoot + "/S_Player_Visor.asset";
        private const string MonsterSpritePath =
            CharacterSpriteRoot + "/S_Monkey_Mutant.png";
        private const string CircleSpritePath = SpriteRoot + "/S_StatusCircle.asset";
        private const string FlashlightSpritePath = SpriteRoot + "/S_FlashlightCone.asset";
        private const string PanelSpritePath = SpriteRoot + "/S_MissionPanel.asset";
        private const float RuntimeMonsterTestTimeoutSeconds = 5f;
        private const float RuntimeAntidoteTestTimeoutSeconds = 3f;
        private const float CorridorWidth = 4.5f;
        private const float WallThickness = 0.32f;

        private static readonly string[] RoomOrder =
        {
            "VaccineA", "LabA", "QuarantineA", "Storage", "Security",
            "Power", "Ward", "LabB", "QuarantineB", "VaccineB"
        };

        private static readonly string[] MonsterSpawnRoomIds =
        {
            "VaccineA", "QuarantineA", "Ward", "QuarantineB"
        };

        private static readonly RoomDefinition[] RoomDefinitions =
        {
            new("VaccineA", new Vector2(-42f, 4f), new Vector2(12f, 15f), "백신실 A"),
            new("LabA", new Vector2(-10f, 24f), new Vector2(15f, 18f), "실험실 A"),
            new("QuarantineA", new Vector2(13f, 24f), new Vector2(12f, 15f), "격리실 A"),
            new("Storage", new Vector2(-25f, -7f), new Vector2(12f, 15f), "액체 보관실"),
            new("Security", new Vector2(-7f, -7f), new Vector2(15f, 18f), "보안실"),
            new("Power", new Vector2(13f, -7f), new Vector2(12f, 15f), "전력 복구실"),
            new("Ward", new Vector2(-7f, -29f), new Vector2(12f, 15f), "입원실"),
            new("LabB", new Vector2(13f, -29f), new Vector2(15f, 18f), "실험실 B"),
            new("QuarantineB", new Vector2(35f, -29f), new Vector2(12f, 15f), "격리실 B"),
            new("VaccineB", new Vector2(55f, -29f), new Vector2(12f, 15f), "백신실 B")
        };

        private static readonly CorridorDefinition[] CorridorDefinitions =
        {
            new(
                "VaccineA", WallSide.North,
                "LabA", WallSide.West,
                new Vector2(-42f, 11.5f),
                new Vector2(-42f, 38f),
                new Vector2(-24f, 38f),
                new Vector2(-24f, 24f),
                new Vector2(-17.5f, 24f)),
            new(
                "VaccineA", WallSide.South,
                "Storage", WallSide.West,
                new Vector2(-42f, -3.5f),
                new Vector2(-42f, -7f),
                new Vector2(-31f, -7f)),
            new(
                "LabA", WallSide.East,
                "QuarantineA", WallSide.West,
                new Vector2(-2.5f, 24f),
                new Vector2(7f, 24f)),
            new(
                "QuarantineA", WallSide.South,
                "Power", WallSide.North,
                new Vector2(13f, 16.5f),
                new Vector2(13f, 0.5f)),
            new(
                "Storage", WallSide.East,
                "Security", WallSide.West,
                new Vector2(-19f, -7f),
                new Vector2(-14.5f, -7f)),
            new(
                "Security", WallSide.East,
                "Power", WallSide.West,
                new Vector2(0.5f, -7f),
                new Vector2(7f, -7f)),
            new(
                "Storage", WallSide.South,
                "Ward", WallSide.West,
                new Vector2(-25f, -14.5f),
                new Vector2(-25f, -18.5f),
                new Vector2(-18f, -18.5f),
                new Vector2(-18f, -29f),
                new Vector2(-13f, -29f)),
            new(
                "Ward", WallSide.East,
                "LabB", WallSide.West,
                new Vector2(-1f, -29f),
                new Vector2(5.5f, -29f)),
            new(
                "Security", WallSide.South,
                "LabB", WallSide.North,
                new Vector2(-3f, -16f),
                new Vector2(-3f, -18f),
                new Vector2(8f, -18f),
                new Vector2(8f, -20f)),
            new(
                "Power", WallSide.South,
                "QuarantineB", WallSide.North,
                new Vector2(16f, -14.5f),
                new Vector2(16f, -17f),
                new Vector2(35f, -17f),
                new Vector2(35f, -21.5f)),
            new(
                "LabB", WallSide.East,
                "QuarantineB", WallSide.West,
                new Vector2(20.5f, -29f),
                new Vector2(29f, -29f)),
            new(
                "QuarantineB", WallSide.East,
                "VaccineB", WallSide.West,
                new Vector2(41f, -29f),
                new Vector2(49f, -29f))
        };

        private static readonly Vector2[] PlayerSpawnPositions =
        {
            new(-25f, -7f), new(-10f, 24f), new(13f, -7f),
            new(-7f, -29f), new(13f, -29f), new(-7f, -7f)
        };

        private static MonsterBrain _runtimeTestMonster;
        private static MonsterTarget _runtimeTestTarget;
        private static InfectionService _runtimeTestInfection;
        private static int _runtimeTestInitialBiteCount;
        private static double _runtimeTestStartedAt;
        private static bool _runtimeTestObservedChase;
        private static bool _runtimeTestObservedPatrolAfterBite;
        private static InfectionService _runtimeAntidoteTestInfection;
        private static AntidoteService _runtimeAntidoteTestService;
        private static double _runtimeAntidoteTestStartedAt;

        [MenuItem("Tools/Monkey Lab/Build First Playable")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before building the first playable.");
            }

            EnsureSpriteAssets();
            var scene = EditorSceneManager.OpenScene(
                LaboratoryScenePath,
                OpenSceneMode.Single);
            ClearOldLaboratoryObjects();

            var mapRoot = new GameObject("[Map] Laboratory2D");
            var rooms = BuildMap(mapRoot.transform);
            var prototypeRoot = new GameObject("[Prototype] FirstPlayable");
            var roundPhase = CreateRoundPhase(prototypeRoot.transform);
            CreateGracePeriodView(prototypeRoot.transform, roundPhase);
            CreateRoundHudView(prototypeRoot.transform);
            CreateVillainUpgradeHudView(prototypeRoot.transform);
            var monsterTierRuntime = CreateMonsterTierRuntime(prototypeRoot.transform);
            var noiseService = CreateNoiseService(prototypeRoot.transform);
            var navigationGraph = CreateNavigationGraph(
                prototypeRoot.transform,
                rooms);
            var fuseStations = new[]
            {
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Power"],
                    "MissionStation_Fuse",
                    new Vector2(3.3f, 3.6f),
                    MissionPrototypeKind.FuseSequence),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Power"],
                    "MissionStation_Breaker",
                    new Vector2(-3.3f, 3.6f),
                    MissionPrototypeKind.BreakerSequence),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Security"],
                    "MissionStation_Cctv",
                    new Vector2(3f, 3f),
                    MissionPrototypeKind.CctvReboot),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Storage"],
                    "MissionStation_Sample_01",
                    new Vector2(-3f, 3f),
                    MissionPrototypeKind.SampleSorting),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["LabA"],
                    "MissionStation_Sample_02",
                    new Vector2(3f, -3f),
                    MissionPrototypeKind.SampleSorting)
            };
            var missionRoomIds = new[]
            {
                "power", "power", "security", "storage", "lab_a"
            };
            var missionAuthorities =
                new NetworkFuseStationAuthority[fuseStations.Length];
            for (var index = 0; index < fuseStations.Length; index++)
            {
                ConfigureFuseStationFeedback(
                    fuseStations[index],
                    noiseService,
                    missionRoomIds[index]);
                CreateFuseMissionView(
                    prototypeRoot.transform,
                    fuseStations[index],
                    index == 0
                        ? "[UI] FuseMission"
                        : $"[UI] Mission_{index + 1:00}");
                missionAuthorities[index] = fuseStations[index]
                    .GetComponent<NetworkFuseStationAuthority>();
            }

            CreateNetworkRoundState(
                prototypeRoot.transform,
                roundPhase,
                missionAuthorities);
            CreateNoiseAlertView(prototypeRoot.transform, noiseService);

            var player = CreatePlayer(
                prototypeRoot.transform,
                PlayerSpawnPositions[0]);
            var monsterTarget = player.GetComponent<MonsterTarget>();
            CreateInfectionPrototype(
                prototypeRoot.transform,
                player,
                monsterTarget,
                monsterTierRuntime);
            CreateMonsterBiteAlertView(prototypeRoot.transform, monsterTarget);
            var baseMonsters = CreateMonsters(
                prototypeRoot.transform,
                rooms,
                navigationGraph,
                noiseService,
                roundPhase,
                monsterTierRuntime,
                monsterTarget);
            CreateUpgradeSystem(
                prototypeRoot.transform,
                rooms,
                navigationGraph,
                noiseService,
                roundPhase,
                monsterTierRuntime,
                monsterTarget,
                baseMonsters);
            ConfigureCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LaboratoryScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Validate();
            Debug.Log(
                "[MonkeyLab] 2D top-down first playable is ready: " +
                "WASD, mouse aim, F flashlight, E interaction, R antidote.");
        }

        internal static void EnsureTopDownArtAssets()
        {
            EnsureSpriteAssets();
        }

        [MenuItem("Tools/Monkey Lab/Build Complete 2D Top Down")]
        public static void BuildCompleteTopDown()
        {
            Build();
            ProjectBootstrap.BuildNetworkPlayerFlow();
            EditorSceneManager.OpenScene(LaboratoryScenePath, OpenSceneMode.Single);
            Debug.Log(
                "[MonkeyLab] Complete 2D conversion finished, including the network player prefab.");
        }

        [MenuItem("Tools/Monkey Lab/Validate First Playable")]
        public static void Validate()
        {
            if (SceneManager.GetActiveScene().path != LaboratoryScenePath)
            {
                EditorSceneManager.OpenScene(
                    LaboratoryScenePath,
                    OpenSceneMode.Single);
            }

            var failures = new List<string>();
            ValidateCorridorLayout(failures);
            var player = GameObject.Find("P_Player_Local");
            RequireComponent<Rigidbody2D>(player, failures);
            RequireComponent<CapsuleCollider2D>(player, failures);
            RequireComponent<PlayerInputReader>(player, failures);
            RequireComponent<PlayerMotor>(player, failures);
            RequireComponent<PlayerAimController>(player, failures);
            RequireComponent<PlayerInteractor>(player, failures);
            RequireComponent<MonsterTarget>(player, failures);
            RequireComponent<InfectionService>(player, failures);
            RequireComponent<AntidoteService>(player, failures);

            if (player != null &&
                (player.GetComponent<CharacterController>() != null ||
                 player.GetComponent<Collider>() != null ||
                 (player.GetComponent<Rigidbody2D>().constraints &
                 RigidbodyConstraints2D.FreezeRotation) == 0 ||
                 player.transform.Find(
                     "VisualRoot/AimPivot/FlashlightCone") == null))
            {
                failures.Add(
                    "P_Player_Local movement, fixed visual or flashlight pivot is incomplete.");
            }

            var mainCamera = Camera.main;
            if (mainCamera == null || !mainCamera.orthographic ||
                mainCamera.GetComponent<TopDownCamera>() == null)
            {
                failures.Add("Main Camera is missing the orthographic TopDownCamera.");
            }

            var graph = GameObject.Find("[Navigation] Laboratory2D")?
                .GetComponent<TopDownNavigationGraph>();
            if (graph == null ||
                graph.NodeCount <
                RoomDefinitions.Length + CorridorDefinitions.Length * 2 ||
                graph.LinkCount < CorridorDefinitions.Length * 3)
            {
                failures.Add("The 2D laboratory navigation graph is incomplete.");
            }

            var walls = GameObject.Find("[Map] CollisionWalls");
            if (walls == null || walls.GetComponentsInChildren<BoxCollider2D>().Length < 20)
            {
                failures.Add("The 2D room and corridor collision walls are missing.");
            }

            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (roundPhase == null || roundPhase.Config == null ||
                !Mathf.Approximately(
                    roundPhase.Config.InitialGracePeriodSeconds,
                    30f))
            {
                failures.Add("The local 30 second grace period is missing.");
            }

            var station = GameObject.Find("MissionStation_Fuse")?
                .GetComponent<FuseStationPrototype>();
            if (station == null || station.Config == null ||
                station.GetComponent<Collider2D>() == null)
            {
                failures.Add("The 2D fuse mission station is incomplete.");
            }

            var noiseService = GameObject.Find("[Gameplay] NoiseService")?
                .GetComponent<NoiseService>();
            if (noiseService == null || noiseService.Config == null)
            {
                failures.Add("NoiseService or its config is missing.");
            }

            var monster = GameObject.Find("P_Monster_01");
            var monsterBrain = monster?.GetComponent<MonsterBrain>();
            var monsterBrains = UnityEngine.Object.FindObjectsByType<MonsterBrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            // 기본 4마리 + 개체 강화 예비 4마리(2단계 × 2마리)를 모두 센다.
            const int reinforcementMonsterCount = 4;
            if (monsterBrains.Length !=
                    MonsterSpawnRoomIds.Length + reinforcementMonsterCount ||
                monsterBrain == null || monsterBrain.Config == null ||
                monsterBrain.PatrolPointCount < 3 ||
                monster.GetComponent<Rigidbody2D>() == null ||
                monster.GetComponent<CapsuleCollider2D>() == null ||
                monster.GetComponent<MonsterSenses>() == null ||
                monster.GetComponent<MonsterBiteController>() == null ||
                (monster.GetComponent<Rigidbody2D>().constraints &
                 RigidbodyConstraints2D.FreezeRotation) == 0 ||
                monsterBrain.NavigationGraph != graph)
            {
                failures.Add("The 2D monster AI setup is incomplete.");
            }

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            if (inputActions == null ||
                inputActions.FindAction("Gameplay/Move") == null ||
                inputActions.FindAction("Gameplay/Look") == null ||
                inputActions.FindAction("Gameplay/Interact") == null ||
                inputActions.FindAction("Gameplay/Flashlight") == null ||
                inputActions.FindAction("Gameplay/UseAntidote") == null ||
                inputActions.FindAction("Gameplay/Cancel") == null)
            {
                failures.Add("Required player input actions are missing.");
            }

            if (GameObject.Find("[UI] GracePeriod")?.GetComponent<GracePeriodView>() == null ||
                GameObject.Find("[UI] FuseMission")?.GetComponent<FuseMissionView>() == null ||
                GameObject.Find("[UI] NoiseAlert")?.GetComponent<NoiseAlertView>() == null ||
                GameObject.Find("[UI] MonsterBiteAlert")?
                    .GetComponent<MonsterBiteAlertView>() == null ||
                GameObject.Find("[UI] InfectionHud")?.GetComponent<InfectionHudView>() == null)
            {
                failures.Add("One or more local gameplay HUD presenters are missing.");
            }

            var upgradeStations =
                UnityEngine.Object.FindObjectsByType<UpgradeStationPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var hasEveryUpgradeAxis =
                upgradeStations.Length == 3 &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Scent) &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Population) &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Toxicity);
            if (!hasEveryUpgradeAxis ||
                Array.Exists(
                    upgradeStations,
                    item =>
                        item.Config == null ||
                        item.GetComponent<Collider2D>() == null ||
                        item.GetComponent<NetworkUpgradeStationAuthority>() ==
                            null))
            {
                failures.Add("The villain upgrade stations are incomplete.");
            }

            var upgradeAuthority =
                GameObject.Find("[Network] VillainUpgradeAuthority")?
                    .GetComponent<NetworkVillainUpgradeAuthority>();
            var populationSpawner =
                GameObject.Find("[Network] MonsterPopulationSpawner")?
                    .GetComponent<NetworkMonsterPopulationSpawner>();
            if (upgradeAuthority == null || upgradeAuthority.Config == null ||
                populationSpawner == null ||
                populationSpawner.TierConfig == null ||
                !populationSpawner.MatchesBalanceTable(0) ||
                !populationSpawner.MatchesBalanceTable(1) ||
                !populationSpawner.MatchesBalanceTable(2))
            {
                failures.Add(
                    "The villain upgrade authority setup does not match the monster tier table.");
            }

            if (GameObject.Find("[UI] VillainUpgradeHud")?
                    .GetComponent<VillainUpgradeHudView>() == null)
            {
                failures.Add("The villain upgrade HUD presenter is missing.");
            }

            var clueMarkers =
                UnityEngine.Object.FindObjectsByType<ClueMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var clueAuthority =
                GameObject.Find("[Network] ClueAuthority")?
                    .GetComponent<NetworkClueAuthority>();
            if (clueMarkers.Length != 16 ||
                clueAuthority == null ||
                clueAuthority.MarkerCount != clueMarkers.Length ||
                Array.Exists(clueMarkers, marker => marker.IsActive) ||
                Array.Exists(
                    clueMarkers,
                    marker => string.IsNullOrEmpty(marker.RoomId)))
            {
                failures.Add("The scene clue setup is incomplete.");
            }

            var speakerAuthority =
                GameObject.Find("[Network] SpeakerAuthority")?
                    .GetComponent<NetworkSpeakerAuthority>();
            if (speakerAuthority == null ||
                speakerAuthority.Config == null ||
                speakerAuthority.SpeakerCount != RoomOrder.Length ||
                GameObject.Find("[UI] SpeakerRemote")?
                    .GetComponent<SpeakerRemoteView>() == null)
            {
                failures.Add("The speaker remote setup is incomplete.");
            }

            if (GameObject.Find("[Network] MeetingAuthority")?
                    .GetComponent<NetworkMeetingAuthority>() == null ||
                GameObject.Find("[UI] Meeting")?
                    .GetComponent<MeetingView>() == null)
            {
                failures.Add("The meeting setup is incomplete.");
            }

            var securityTerminal =
                GameObject.Find("[Network] SecurityTerminalAuthority")?
                    .GetComponent<NetworkSecurityTerminalAuthority>();
            if (securityTerminal == null ||
                GameObject.Find("[UI] SecurityTerminal")?
                    .GetComponent<SecurityTerminalView>() == null)
            {
                failures.Add("The security terminal setup is incomplete.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, failures));
            }

            Debug.Log("[MonkeyLab] 2D first playable validation passed.");
        }

        [MenuItem("Tools/Monkey Lab/Test Fuse Failure Noise")]
        public static void TestFuseFailureNoise()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing fuse failure noise.");
            }

            var player = GameObject.Find("P_Player_Local");
            var station = GameObject.Find("MissionStation_Fuse")?
                .GetComponent<FuseStationPrototype>();
            var nearbyMonster = GameObject.Find("P_Monster_01")?
                .GetComponent<MonsterBrain>();
            var secondNearbyMonster = GameObject.Find("P_Monster_02")?
                .GetComponent<MonsterBrain>();
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            var securityRoom = GameObject.Find("Room_Security");
            var powerRoom = GameObject.Find("Room_Power");
            if (player == null || station == null || nearbyMonster == null ||
                secondNearbyMonster == null || securityRoom == null ||
                powerRoom == null ||
                roundPhase == null)
            {
                throw new InvalidOperationException(
                    "Runtime fuse noise test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            nearbyMonster.transform.position = securityRoom.transform.position;
            secondNearbyMonster.transform.position = powerRoom.transform.position;
            station.Interact(player);
            if (!station.IsMissionActive || station.RequiredOrder.Count == 0)
            {
                throw new InvalidOperationException(
                    "Fuse mission did not start during the runtime test.");
            }

            var expectedFuseId = station.RequiredOrder[0];
            station.SubmitFuse(expectedFuseId == 1 ? 2 : 1);
            if (nearbyMonster.State != MonsterState.InvestigateNoise ||
                secondNearbyMonster.State != MonsterState.InvestigateNoise)
            {
                throw new InvalidOperationException(
                    "Every monster inside the Medium path radius must investigate " +
                    $"the fuse noise. Current states: {nearbyMonster.State}, " +
                    $"{secondNearbyMonster.State}.");
            }

            Debug.Log(
                "[MonkeyLab] 2D fuse noise validation passed: " +
                $"noise={nearbyMonster.CurrentNoiseId}, responders=2.");
        }

        [MenuItem("Tools/Monkey Lab/Test Monster Chase And Bite")]
        public static void TestMonsterChaseAndBite()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing monster chase and bite.");
            }

            StopRuntimeMonsterTest();
            var player = GameObject.Find("P_Player_Local");
            var target = player?.GetComponent<MonsterTarget>();
            var playerCollider = player?.GetComponent<Collider2D>();
            var monster = GameObject.Find("P_Monster_01");
            var body = monster?.GetComponent<Rigidbody2D>();
            var monsterCollider = monster?.GetComponent<Collider2D>();
            var brain = monster?.GetComponent<MonsterBrain>();
            var infectionService = player?.GetComponent<InfectionService>();
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (player == null || target == null || playerCollider == null ||
                monster == null || body == null || monsterCollider == null ||
                brain == null || infectionService == null || roundPhase == null)
            {
                throw new InvalidOperationException(
                    "Runtime monster chase and bite test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            var centerSeparation = Mathf.Max(
                0.2f,
                brain.Senses.TierRuntime.CurrentProximityDetectionRadius -
                0.1f);
            var desiredPosition = (Vector2)player.transform.position -
                                  Vector2.up * centerSeparation;
            monster.transform.position = desiredPosition;
            body.position = desiredPosition;
            body.rotation = 0f;
            brain.Senses.SetFacingDirection(Vector2.up);
            Physics2D.SyncTransforms();
            var initiallyDetected = brain.Senses.TryDetectTarget(
                out var initialDetectionType);
            Debug.Log(
                $"[MonkeyLab] Runtime monster test placed monster={body.position}, " +
                $"target={player.transform.position}, detected={initiallyDetected}, " +
                $"detection={initialDetectionType}, " +
                $"pathClear={brain.Senses.HasClearPathToTarget()}, " +
                $"blocker={brain.Senses.LastPathBlocker?.name ?? "none"}.");
            if (!initiallyDetected)
            {
                throw new InvalidOperationException(
                    "Runtime monster test placement could not detect the target.");
            }

            _runtimeTestMonster = brain;
            _runtimeTestTarget = target;
            _runtimeTestInfection = infectionService;
            _runtimeTestInitialBiteCount = target.BiteCount;
            _runtimeTestStartedAt = EditorApplication.timeSinceStartup;
            _runtimeTestObservedChase = false;
            _runtimeTestObservedPatrolAfterBite = false;
            brain.StateChanged += HandleRuntimeMonsterStateChanged;
            EditorApplication.update += MonitorRuntimeMonsterTest;
        }

        [MenuItem("Tools/Monkey Lab/Test Infection And Antidote")]
        public static void TestInfectionAndAntidote()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing infection and antidote use.");
            }

            StopRuntimeAntidoteTest();
            var player = GameObject.Find("P_Player_Local");
            var target = player?.GetComponent<MonsterTarget>();
            var infectionService = player?.GetComponent<InfectionService>();
            var antidoteService = player?.GetComponent<AntidoteService>();
            var tierRuntime = GameObject.Find("[Gameplay] MonsterTierRuntime")?
                .GetComponent<MonsterTierRuntime>();
            if (target == null || infectionService == null ||
                antidoteService == null || tierRuntime == null)
            {
                throw new InvalidOperationException(
                    "Runtime infection test objects are missing.");
            }

            if (infectionService.State != PlayerLifeState.AliveHealthy ||
                antidoteService.HasAntidote)
            {
                throw new InvalidOperationException(
                    "Start the infection test from a fresh Play Mode session.");
            }

            tierRuntime.SetToxicityTier(MonsterTierConfig.MinimumTier);
            if (!antidoteService.TryAddAntidote() ||
                !target.TryReceiveBite(null, Time.time, 0f) ||
                !infectionService.IsInfected ||
                !Mathf.Approximately(
                    infectionService.DurationAtBiteSeconds,
                    90f) ||
                !antidoteService.TryBeginUse(Time.time))
            {
                throw new InvalidOperationException(
                    "Infection or antidote use did not start correctly.");
            }

            _runtimeAntidoteTestInfection = infectionService;
            _runtimeAntidoteTestService = antidoteService;
            _runtimeAntidoteTestStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += MonitorRuntimeAntidoteTest;
        }

        private static Dictionary<string, RoomDefinition> BuildMap(Transform mapRoot)
        {
            var unitSprite = LoadSprite(UnitSpritePath);
            var rooms = new Dictionary<string, RoomDefinition>();
            var walkableAreas = new List<Rect>(
                RoomDefinitions.Length + CorridorDefinitions.Length * 4);
            foreach (var definition in RoomDefinitions)
            {
                rooms[definition.Id] = definition;
            }

            var corridorRoot = new GameObject("[Map] Corridors").transform;
            corridorRoot.SetParent(mapRoot);
            var collisionRoot =
                new GameObject("[Map] CollisionWalls").transform;
            collisionRoot.SetParent(mapRoot);
            foreach (var corridor in CorridorDefinitions)
            {
                CreateCorridor(
                    corridor,
                    unitSprite,
                    corridorRoot,
                    walkableAreas);
            }

            var floorRoot = new GameObject("[Map] Rooms").transform;
            floorRoot.SetParent(mapRoot);
            foreach (var room in RoomDefinitions)
            {
                var floorColor = GetRoomColor(room.Id);
                CreateSpriteObject(
                    "Room_" + room.Id,
                    unitSprite,
                    room.Position,
                    room.Size,
                    floorColor,
                    0,
                    floorRoot);
                CreateRoomLabel(room, floorRoot);
                walkableAreas.Add(CreateRect(room.Position, room.Size));
            }

            CreateCollisionBoundary(
                walkableAreas,
                unitSprite,
                collisionRoot);
            CreateSpawnMarkers(mapRoot, rooms);
            return rooms;
        }

        private static void CreateCorridor(
            CorridorDefinition definition,
            Sprite unitSprite,
            Transform floorRoot,
            List<Rect> walkableAreas)
        {
            var name = definition.A + "_to_" + definition.B;
            var path = definition.PathPoints;
            for (var index = 1; index < path.Count; index++)
            {
                CreateCorridorSegment(
                    name,
                    index - 1,
                    path[index - 1],
                    path[index],
                    unitSprite,
                    floorRoot,
                    walkableAreas);
            }

            for (var index = 1; index < path.Count - 1; index++)
            {
                CreateSpriteObject(
                    $"CorridorJoint_{name}_{index:00}",
                    unitSprite,
                    path[index],
                    new Vector2(CorridorWidth, CorridorWidth),
                    new Color(0.10f, 0.17f, 0.23f),
                    0,
                    floorRoot);
                walkableAreas.Add(CreateRect(
                    path[index],
                    new Vector2(CorridorWidth, CorridorWidth)));
            }
        }

        private static void CreateCorridorSegment(
            string name,
            int segmentIndex,
            Vector2 start,
            Vector2 end,
            Sprite unitSprite,
            Transform floorRoot,
            List<Rect> walkableAreas)
        {
            var length = Vector2.Distance(start, end);
            if (length <= 0.01f)
            {
                return;
            }

            var midpoint = (start + end) * 0.5f;
            var delta = end - start;
            var isHorizontal = Mathf.Abs(delta.y) <= 0.001f;
            var isVertical = Mathf.Abs(delta.x) <= 0.001f;
            if (!isHorizontal && !isVertical)
            {
                throw new InvalidOperationException(
                    $"Corridor {name} segment {segmentIndex} is not axis aligned.");
            }

            var walkableSize = isHorizontal
                ? new Vector2(length, CorridorWidth)
                : new Vector2(CorridorWidth, length);
            var renderSize = isHorizontal
                ? new Vector2(length + 0.08f, CorridorWidth)
                : new Vector2(CorridorWidth, length + 0.08f);
            CreateSpriteObject(
                $"Corridor_{name}_{segmentIndex:00}",
                unitSprite,
                midpoint,
                renderSize,
                new Color(0.10f, 0.17f, 0.23f),
                0,
                floorRoot);
            walkableAreas.Add(CreateRect(midpoint, walkableSize));
        }

        private static Rect CreateRect(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        private static void CreateCollisionBoundary(
            IReadOnlyList<Rect> walkableAreas,
            Sprite unitSprite,
            Transform parent)
        {
            var xCoordinates = new List<float>(walkableAreas.Count * 2);
            var yCoordinates = new List<float>(walkableAreas.Count * 2);
            foreach (var area in walkableAreas)
            {
                AddDistinctCoordinate(xCoordinates, area.xMin);
                AddDistinctCoordinate(xCoordinates, area.xMax);
                AddDistinctCoordinate(yCoordinates, area.yMin);
                AddDistinctCoordinate(yCoordinates, area.yMax);
            }

            xCoordinates.Sort();
            yCoordinates.Sort();
            var walkable = new bool[
                xCoordinates.Count - 1,
                yCoordinates.Count - 1];
            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    var midpoint = new Vector2(
                        (xCoordinates[x] + xCoordinates[x + 1]) * 0.5f,
                        (yCoordinates[y] + yCoordinates[y + 1]) * 0.5f);
                    walkable[x, y] =
                        IsPointInsideAnyArea(midpoint, walkableAreas);
                }
            }

            var edges = new List<BoundaryEdge>(walkableAreas.Count * 8);
            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    if (!walkable[x, y])
                    {
                        continue;
                    }

                    var xMin = xCoordinates[x];
                    var xMax = xCoordinates[x + 1];
                    var yMin = yCoordinates[y];
                    var yMax = yCoordinates[y + 1];
                    if (x == 0 || !walkable[x - 1, y])
                    {
                        edges.Add(new BoundaryEdge(
                            false,
                            xMin,
                            yMin,
                            yMax));
                    }

                    if (x == xCoordinates.Count - 2 ||
                        !walkable[x + 1, y])
                    {
                        edges.Add(new BoundaryEdge(
                            false,
                            xMax,
                            yMin,
                            yMax));
                    }

                    if (y == 0 || !walkable[x, y - 1])
                    {
                        edges.Add(new BoundaryEdge(
                            true,
                            yMin,
                            xMin,
                            xMax));
                    }

                    if (y == yCoordinates.Count - 2 ||
                        !walkable[x, y + 1])
                    {
                        edges.Add(new BoundaryEdge(
                            true,
                            yMax,
                            xMin,
                            xMax));
                    }
                }
            }

            edges.Sort(CompareBoundaryEdges);
            var mergedEdges = MergeBoundaryEdges(edges);
            for (var index = 0; index < mergedEdges.Count; index++)
            {
                CreateBoundaryWall(
                    mergedEdges[index],
                    index,
                    unitSprite,
                    parent);
            }
        }

        private static void AddDistinctCoordinate(
            List<float> coordinates,
            float value)
        {
            foreach (var existing in coordinates)
            {
                if (Mathf.Approximately(existing, value))
                {
                    return;
                }
            }

            coordinates.Add(value);
        }

        private static bool IsPointInsideAnyArea(
            Vector2 point,
            IReadOnlyList<Rect> areas)
        {
            foreach (var area in areas)
            {
                if (point.x > area.xMin && point.x < area.xMax &&
                    point.y > area.yMin && point.y < area.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareBoundaryEdges(
            BoundaryEdge left,
            BoundaryEdge right)
        {
            var orientationComparison =
                left.IsHorizontal.CompareTo(right.IsHorizontal);
            if (orientationComparison != 0)
            {
                return orientationComparison;
            }

            var fixedComparison =
                left.FixedCoordinate.CompareTo(right.FixedCoordinate);
            return fixedComparison != 0
                ? fixedComparison
                : left.Start.CompareTo(right.Start);
        }

        private static List<BoundaryEdge> MergeBoundaryEdges(
            IReadOnlyList<BoundaryEdge> sortedEdges)
        {
            var merged = new List<BoundaryEdge>(sortedEdges.Count);
            foreach (var edge in sortedEdges)
            {
                if (merged.Count == 0)
                {
                    merged.Add(edge);
                    continue;
                }

                var previous = merged[^1];
                if (previous.IsHorizontal == edge.IsHorizontal &&
                    Mathf.Approximately(
                        previous.FixedCoordinate,
                        edge.FixedCoordinate) &&
                    edge.Start <= previous.End + 0.001f)
                {
                    merged[^1] = new BoundaryEdge(
                        previous.IsHorizontal,
                        previous.FixedCoordinate,
                        previous.Start,
                        Mathf.Max(previous.End, edge.End));
                    continue;
                }

                merged.Add(edge);
            }

            return merged;
        }

        private static void CreateBoundaryWall(
            BoundaryEdge edge,
            int index,
            Sprite sprite,
            Transform parent)
        {
            var length = edge.End - edge.Start;
            var center = (edge.Start + edge.End) * 0.5f;
            var position = edge.IsHorizontal
                ? new Vector2(center, edge.FixedCoordinate)
                : new Vector2(edge.FixedCoordinate, center);
            var size = edge.IsHorizontal
                ? new Vector2(length + WallThickness, WallThickness)
                : new Vector2(WallThickness, length + WallThickness);
            CreateWall(
                $"Wall_Boundary_{index:000}",
                position,
                size,
                sprite,
                parent);
        }

        private static GameObject CreateWall(
            string name,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Transform parent)
        {
            var wall = CreateSpriteObject(
                name,
                sprite,
                position,
                size,
                new Color(0.045f, 0.09f, 0.13f),
                20,
                parent);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return wall;
        }

        private static void CreateRoomLabel(
            RoomDefinition room,
            Transform parent)
        {
            var labelObject = new GameObject("Label_" + room.Id);
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(
                room.Position.x,
                room.Position.y + room.Size.y * 0.36f,
                0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException(
                    "Unity built-in LegacyRuntime font could not be loaded.");
            }

            var label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.text = room.DisplayName;
            label.fontSize = 56;
            label.characterSize = 0.085f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.55f, 0.78f, 0.84f, 0.85f);
            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 3;
        }

        private static void CreateSpawnMarkers(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var spawnRoot = new GameObject("[Map] SpawnPoints").transform;
            spawnRoot.SetParent(parent);
            for (var index = 0; index < PlayerSpawnPositions.Length; index++)
            {
                var marker = new GameObject($"PlayerSpawn_{index + 1:00}");
                marker.transform.SetParent(spawnRoot);
                marker.transform.position = PlayerSpawnPositions[index];
            }

            for (var index = 0; index < MonsterSpawnRoomIds.Length; index++)
            {
                var marker = new GameObject($"MonsterSpawn_{index + 1:00}");
                marker.transform.SetParent(spawnRoot);
                marker.transform.position =
                    rooms[MonsterSpawnRoomIds[index]].Position;
            }
        }

        private static TopDownNavigationGraph CreateNavigationGraph(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var graphObject = new GameObject("[Navigation] Laboratory2D");
            graphObject.transform.SetParent(parent);
            var nodeRoot = new GameObject("Nodes").transform;
            nodeRoot.SetParent(graphObject.transform);
            var nodes = new List<Transform>(RoomOrder.Length * 4);
            var roomIndices = new Dictionary<string, int>();
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                var roomId = RoomOrder[index];
                roomIndices[roomId] = nodes.Count;
                var node = new GameObject("Node_" + roomId);
                node.transform.SetParent(nodeRoot);
                node.transform.position = rooms[roomId].Position;
                nodes.Add(node.transform);
            }

            var links = new List<TopDownNavigationGraph.Link>(
                CorridorDefinitions.Length * 5);
            for (var index = 0;
                 index < CorridorDefinitions.Length;
                 index++)
            {
                var corridor = CorridorDefinitions[index];
                var corridorPath = corridor.PathPoints;
                var previousIndex = roomIndices[corridor.A];
                for (var pathIndex = 0;
                     pathIndex < corridorPath.Count;
                     pathIndex++)
                {
                    var pathNode = new GameObject(
                        $"Node_{corridor.A}_{corridor.B}_{pathIndex:00}");
                    pathNode.transform.SetParent(nodeRoot);
                    pathNode.transform.position = corridorPath[pathIndex];
                    var currentIndex = nodes.Count;
                    nodes.Add(pathNode.transform);
                    links.Add(new TopDownNavigationGraph.Link(
                        previousIndex,
                        currentIndex));
                    previousIndex = currentIndex;
                }

                links.Add(new TopDownNavigationGraph.Link(
                    previousIndex,
                    roomIndices[corridor.B]));
            }

            var graph = graphObject.AddComponent<TopDownNavigationGraph>();
            graph.Configure(nodes.ToArray(), links.ToArray());
            return graph;
        }

        private static GameObject CreatePlayer(
            Transform parent,
            Vector2 spawnPosition)
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            var movementConfig =
                AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                    MovementConfigPath);
            var interactionConfig = EnsureInteractionBalanceConfig();
            if (inputActions == null || movementConfig == null ||
                interactionConfig == null)
            {
                throw new InvalidOperationException(
                    "Player input or movement config is missing.");
            }

            var player = new GameObject("P_Player_Local");
            player.transform.SetParent(parent);
            player.transform.position = spawnPosition;
            var body = player.AddComponent<Rigidbody2D>();
            ConfigureDynamicBody(body);
            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1.05f, 1.45f);

            var input = player.AddComponent<PlayerInputReader>();
            input.Configure(inputActions);
            var motor = player.AddComponent<PlayerMotor>();
            motor.Configure(input, body, movementConfig);
            var aim = player.AddComponent<PlayerAimController>();
            aim.Configure(input, Camera.main, movementConfig);
            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.Configure(
                input,
                interactionConfig.GeneralInteractionRangeMeters);
            player.AddComponent<MonsterTarget>().Configure(true, true);

            CreatePlayerVisuals(
                player.transform,
                new Color(0.12f, 0.56f, 0.96f),
                input,
                out _);
            var promptObject = new GameObject("[UI] InteractionPrompt");
            promptObject.transform.SetParent(parent);
            promptObject.AddComponent<InteractionPromptView>()
                .Configure(interactor);
            return player;
        }

        internal static GameObject CreatePlayerVisuals(
            Transform parent,
            Color bodyColor,
            PlayerInputReader input,
            out FlashlightController flashlightController)
        {
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(parent, false);
            var bodyObject = CreateSpriteObject(
                "Body",
                LoadSprite(PlayerSpritePath),
                Vector2.zero,
                new Vector2(2f, 2f),
                bodyColor,
                40,
                visualRoot.transform);
            bodyObject.transform.localPosition = Vector3.zero;

            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(visualRoot.transform, false);
            var cone = CreateSpriteObject(
                "FlashlightCone",
                LoadSprite(FlashlightSpritePath),
                new Vector2(0f, 0.55f),
                new Vector2(3.25f, 3.20f),
                Color.white,
                6,
                aimPivot.transform);
            cone.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            flashlightController =
                parent.gameObject.AddComponent<FlashlightController>();
            flashlightController.Configure(
                input,
                parent.GetComponent<PlayerAimController>(),
                aimPivot.transform,
                cone,
                true);
            return visualRoot;
        }

        private static void ConfigureCamera(Transform player)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.008f, 0.016f, 0.026f);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 9f;
            mainCamera.transform.rotation = Quaternion.identity;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                mainCamera.gameObject);

            var follow = mainCamera.GetComponent<TopDownCamera>() ??
                         mainCamera.gameObject.AddComponent<TopDownCamera>();
            follow.Configure(player, 9f, 0.12f);

            var aim = player.GetComponent<PlayerAimController>();
            aim.Configure(
                player.GetComponent<PlayerInputReader>(),
                mainCamera,
                AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                    MovementConfigPath));
        }

        private static FuseStationPrototype CreateFuseStation(
            Transform parent,
            RoomDefinition room,
            string stationName,
            Vector2 localOffset,
            MissionPrototypeKind kind)
        {
            var missionConfig = AssetDatabase.LoadAssetAtPath<FuseMissionConfig>(
                FuseMissionConfigPath);
            if (missionConfig == null)
            {
                missionConfig = ScriptableObject.CreateInstance<FuseMissionConfig>();
                missionConfig.name = "SO_FuseMission_Default";
                AssetDatabase.CreateAsset(missionConfig, FuseMissionConfigPath);
            }

            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.1f, 1.75f),
                GetMissionStationColor(kind),
                30,
                parent);
            var collider = station.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var fuseStation = station.AddComponent<FuseStationPrototype>();
            fuseStation.Configure(
                station.GetComponent<SpriteRenderer>(),
                null,
                missionConfig,
                kind);
            station.AddComponent<NetworkObject>();
            var authority =
                station.AddComponent<NetworkFuseStationAuthority>();
            authority.Configure(
                fuseStation,
                EnsureInteractionBalanceConfig());
            station.AddComponent<MissionStationNetworkPresenter>()
                .Configure(
                    authority,
                    station.GetComponent<SpriteRenderer>());
            return fuseStation;
        }

        private static Color GetMissionStationColor(
            MissionPrototypeKind kind)
        {
            return kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    new Color(0.96f, 0.42f, 0.08f),
                MissionPrototypeKind.BreakerSequence =>
                    new Color(0.94f, 0.72f, 0.12f),
                MissionPrototypeKind.CctvReboot =>
                    new Color(0.10f, 0.72f, 0.86f),
                MissionPrototypeKind.SampleSorting =>
                    new Color(0.48f, 0.78f, 0.30f),
                _ => Color.white
            };
        }

        private static void ConfigureFuseStationFeedback(
            FuseStationPrototype station,
            NoiseService noiseService,
            string roomId)
        {
            station.gameObject.AddComponent<FuseFailureNoiseEmitter>()
                .Configure(station, noiseService, roomId);
            var audioSource = station.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.65f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 32f;
            audioSource.volume = 0.9f;
            station.gameObject.AddComponent<FuseFailureFeedback>()
                .Configure(station, audioSource);
        }

        private static InteractionBalanceConfig
            EnsureInteractionBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<InteractionBalanceConfig>(
                    InteractionBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config =
                ScriptableObject.CreateInstance<InteractionBalanceConfig>();
            config.name = "SO_InteractionBalance_Default";
            AssetDatabase.CreateAsset(
                config,
                InteractionBalanceConfigPath);
            return config;
        }

        /// <summary>
        /// 현장 단서 마커를 배치한다. 강화는 축마다 2회까지 가능하므로
        /// 종류마다 마커를 2개씩 두고, 두 번째 강화는 다른 위치에 흔적을 남긴다(SDD §14.2).
        /// 마커는 비활성 상태로 시작해 강화 성공 시 서버가 켠다.
        /// </summary>
        private static ClueMarker[] CreateClueSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var clueRoot = new GameObject("[Clue] SceneClues").transform;
            clueRoot.SetParent(parent);

            var definitions = new[]
            {
                // 후각 강화 → 해당 실험실 환풍구의 붉은 연기
                (ClueKind.VentRedSmoke, "LabB", new Vector2(-3.2f, 5.2f)),
                (ClueKind.VentRedSmoke, "LabA", new Vector2(3.2f, 5.2f)),
                // 개체 강화 → 격리실 A·B의 파손된 잠금장치
                (ClueKind.BrokenQuarantineLock, "QuarantineA", new Vector2(0f, 5.2f)),
                (ClueKind.BrokenQuarantineLock, "QuarantineB", new Vector2(0f, 5.2f)),
                // 독성 강화 → 백신실 바닥의 빈 주사기
                (ClueKind.EmptySyringe, "VaccineB", new Vector2(3.2f, 1.2f)),
                (ClueKind.EmptySyringe, "VaccineA", new Vector2(-3.2f, 1.2f))
            };

            var markers = new ClueMarker[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var (kind, roomId, offset) = definitions[index];
                var marker = CreateClueMarker(
                    clueRoot,
                    kind,
                    clueId: index + 1,
                    roomId,
                    rooms[roomId].Position + offset);
                markers[index] = marker;
            }

            return markers;
        }

        private static ClueMarker CreateClueMarker(
            Transform parent,
            ClueKind kind,
            int clueId,
            string roomId,
            Vector2 position)
        {
            var (sprite, size, color) = GetClueVisual(kind);
            var markerObject = CreateSpriteObject(
                $"Clue_{kind}_{clueId:00}",
                LoadSprite(sprite),
                position,
                size,
                color,
                33,
                parent);
            var marker = markerObject.AddComponent<ClueMarker>();
            marker.Configure(
                markerObject.GetComponent<SpriteRenderer>(),
                kind,
                clueId,
                roomId);
            // 생성 전에는 보이지 않는다. 서버가 활성화할 때 켜진다.
            markerObject.GetComponent<SpriteRenderer>().enabled = false;
            return marker;
        }

        private static (string sprite, Vector2 size, Color color) GetClueVisual(
            ClueKind kind)
        {
            return kind switch
            {
                ClueKind.VentRedSmoke => (
                    CircleSpritePath,
                    new Vector2(1.9f, 1.9f),
                    new Color(0.95f, 0.2f, 0.15f, 0.7f)),
                ClueKind.BrokenQuarantineLock => (
                    PanelSpritePath,
                    new Vector2(1.5f, 1.1f),
                    new Color(1f, 0.7f, 0.1f, 0.9f)),
                ClueKind.EmptySyringe => (
                    PanelSpritePath,
                    new Vector2(1.2f, 0.5f),
                    new Color(0.85f, 0.95f, 1f, 0.95f)),
                ClueKind.SpeakerRedLed => (
                    CircleSpritePath,
                    new Vector2(0.42f, 0.42f),
                    new Color(1f, 0.12f, 0.12f, 1f)),
                _ => (
                    CircleSpritePath,
                    new Vector2(1f, 1f),
                    new Color(0.95f, 0.2f, 0.15f, 0.8f))
            };
        }

        private static SpeakerBalanceConfig EnsureSpeakerBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SpeakerBalanceConfig>(
                SpeakerBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SpeakerBalanceConfig>();
                config.name = "SO_SpeakerBalance_Default";
                AssetDatabase.CreateAsset(config, SpeakerBalanceConfigPath);
            }

            return config;
        }

        /// <summary>
        /// 방마다 스피커 하나와 그 스피커의 붉은 LED 단서 마커를 배치한다.
        /// LED는 방마다 따로 남아야 어느 방에서 울렸는지 추리할 수 있다(GDD §13.1).
        /// </summary>
        private static void CreateSpeakerSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            NoiseService noiseService,
            ClueMarker[] upgradeClueMarkers,
            out ClueMarker[] allClueMarkers)
        {
            var speakerRoot = new GameObject("[Speaker] RoomSpeakers").transform;
            speakerRoot.SetParent(parent);

            var speakers = new SpeakerPlacement[RoomOrder.Length];
            var ledMarkers = new ClueMarker[RoomOrder.Length];
            // 강화 단서 ID와 겹치지 않도록 뒤 번호를 쓴다.
            var nextClueId = upgradeClueMarkers.Length + 1;
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                var roomId = RoomOrder[index];
                var room = rooms[roomId];
                var speakerPosition = room.Position + new Vector2(0f, -4.6f);

                var speakerObject = CreateSpriteObject(
                    $"Speaker_{roomId}",
                    LoadSprite(PanelSpritePath),
                    speakerPosition,
                    new Vector2(1.1f, 0.8f),
                    new Color(0.55f, 0.58f, 0.62f, 1f),
                    31,
                    speakerRoot);
                var placement = speakerObject.AddComponent<SpeakerPlacement>();
                placement.Configure(
                    speakerObject.GetComponent<SpriteRenderer>(),
                    roomId,
                    room.DisplayName);
                speakers[index] = placement;

                ledMarkers[index] = CreateClueMarker(
                    speakerRoot,
                    ClueKind.SpeakerRedLed,
                    nextClueId++,
                    roomId,
                    speakerPosition + new Vector2(0.42f, 0.28f));
            }

            var authorityObject = new GameObject("[Network] SpeakerAuthority");
            authorityObject.transform.SetParent(parent);
            authorityObject.AddComponent<NetworkObject>();
            authorityObject.AddComponent<NetworkSpeakerAuthority>().Configure(
                EnsureSpeakerBalanceConfig(),
                noiseService,
                speakers);
            authorityObject.AddComponent<SpeakerActivationPresenter>()
                .Configure(speakers);

            var viewObject = new GameObject("[UI] SpeakerRemote");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<SpeakerRemoteView>();

            var meetingAuthorityObject =
                new GameObject("[Network] MeetingAuthority");
            meetingAuthorityObject.transform.SetParent(parent);
            meetingAuthorityObject.AddComponent<NetworkObject>();
            meetingAuthorityObject.AddComponent<NetworkMeetingAuthority>();

            var meetingViewObject = new GameObject("[UI] Meeting");
            meetingViewObject.transform.SetParent(parent);
            meetingViewObject.AddComponent<MeetingView>();

            // CCTV·서버 로그는 프로젝트 50% 이후에 열린다(SDD §14.3).
            var roomDisplayNames = new string[RoomOrder.Length];
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                roomDisplayNames[index] = rooms[RoomOrder[index]].DisplayName;
            }

            var terminalObject =
                new GameObject("[Network] SecurityTerminalAuthority");
            terminalObject.transform.SetParent(parent);
            terminalObject.AddComponent<NetworkObject>();
            terminalObject.AddComponent<NetworkSecurityTerminalAuthority>()
                .Configure(RoomOrder, roomDisplayNames);

            var terminalViewObject = new GameObject("[UI] SecurityTerminal");
            terminalViewObject.transform.SetParent(parent);
            terminalViewObject.AddComponent<SecurityTerminalView>();

            var combined =
                new ClueMarker[upgradeClueMarkers.Length + ledMarkers.Length];
            upgradeClueMarkers.CopyTo(combined, 0);
            ledMarkers.CopyTo(combined, upgradeClueMarkers.Length);
            allClueMarkers = combined;
        }

        private static MonsterBalanceConfig EnsureMonsterBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterBalanceConfig>(
                MonsterBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterBalanceConfig>();
                config.name = "SO_MonsterBalance_Default";
                AssetDatabase.CreateAsset(config, MonsterBalanceConfigPath);
            }

            return config;
        }

        private static UpgradeBalanceConfig EnsureUpgradeBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<UpgradeBalanceConfig>(
                UpgradeBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UpgradeBalanceConfig>();
                config.name = "SO_UpgradeBalance_Default";
                AssetDatabase.CreateAsset(config, UpgradeBalanceConfigPath);
            }

            return config;
        }

        /// <summary>
        /// 빌런 전용 강화 스테이션을 만든다. 축마다 하나씩 배치한다.
        /// </summary>
        private static UpgradeStationPrototype CreateUpgradeStation(
            Transform parent,
            RoomDefinition room,
            string stationName,
            Vector2 localOffset,
            UpgradeAxis axis,
            string roomId)
        {
            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.1f, 1.75f),
                new Color(0.65f, 0.2f, 0.85f, 1f),
                30,
                parent);
            var collider = station.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var upgradeStation =
                station.AddComponent<UpgradeStationPrototype>();
            upgradeStation.Configure(
                station.GetComponent<SpriteRenderer>(),
                EnsureUpgradeBalanceConfig(),
                axis,
                roomId);
            station.AddComponent<NetworkObject>();
            station.AddComponent<NetworkUpgradeStationAuthority>().Configure(
                upgradeStation,
                EnsureInteractionBalanceConfig());
            return upgradeStation;
        }

        /// <summary>
        /// 개체 강화로 추가되는 괴물과 강화 권위 오브젝트를 만든다.
        /// 1단계는 격리실 A, 2단계는 격리실 B에서 두 마리씩 활성화한다.
        /// </summary>
        private static void CreateUpgradeSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target,
            NetworkMonsterAuthority[] baseMonsters)
        {
            // 위치는 GDD §13.2~13.4를 따른다.
            // 개체 강화 패널은 격리실과 떨어진 보안실에 둔다(§13.3).
            CreateUpgradeStation(
                parent,
                rooms["LabB"],
                "UpgradeStation_Scent",
                new Vector2(-3.2f, 3.4f),
                UpgradeAxis.Scent,
                "LabB");
            CreateUpgradeStation(
                parent,
                rooms["Security"],
                "UpgradeStation_Population",
                new Vector2(-3.2f, -3.4f),
                UpgradeAxis.Population,
                "Security");
            CreateUpgradeStation(
                parent,
                rooms["VaccineB"],
                "UpgradeStation_Toxicity",
                new Vector2(3.2f, 3.4f),
                UpgradeAxis.Toxicity,
                "VaccineB");
            var upgradeClueMarkers = CreateClueSystem(parent, rooms);
            CreateSpeakerSystem(
                parent,
                rooms,
                noiseService,
                upgradeClueMarkers,
                out var allClueMarkers);

            var clueAuthorityObject = new GameObject("[Network] ClueAuthority");
            clueAuthorityObject.transform.SetParent(parent);
            clueAuthorityObject.AddComponent<NetworkObject>();
            clueAuthorityObject.AddComponent<NetworkClueAuthority>()
                .Configure(allClueMarkers);

            var config = EnsureMonsterBalanceConfig();
            var reinforcementRoot =
                new GameObject("[AI] MonsterReinforcements").transform;
            reinforcementRoot.SetParent(parent);
            var patrolRoutes =
                CreateReinforcementPatrolRoutes(reinforcementRoot, rooms);
            var tierOne = CreateReinforcementWave(
                reinforcementRoot,
                rooms["QuarantineA"].Position,
                patrolRoutes[0],
                waveIndex: 1,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                monsterTierRuntime,
                target);
            var tierTwo = CreateReinforcementWave(
                reinforcementRoot,
                rooms["QuarantineB"].Position,
                patrolRoutes[1],
                waveIndex: 2,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                monsterTierRuntime,
                target);

            var spawnerObject = new GameObject("[Network] MonsterPopulationSpawner");
            spawnerObject.transform.SetParent(parent);
            spawnerObject.AddComponent<NetworkObject>();
            var spawner =
                spawnerObject.AddComponent<NetworkMonsterPopulationSpawner>();
            spawner.Configure(
                baseMonsters,
                tierOne,
                tierTwo,
                monsterTierRuntime.Config);

            var authorityObject = new GameObject("[Network] VillainUpgradeAuthority");
            authorityObject.transform.SetParent(parent);
            authorityObject.AddComponent<NetworkObject>();
            authorityObject.AddComponent<NetworkVillainUpgradeAuthority>()
                .Configure(
                    monsterTierRuntime,
                    EnsureUpgradeBalanceConfig(),
                    spawner);
        }

        private static NetworkMonsterAuthority[] CreateReinforcementWave(
            Transform parent,
            Vector2 spawnPosition,
            Transform[] patrolPoints,
            int waveIndex,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            const int monstersPerWave = 2;
            var wave = new NetworkMonsterAuthority[monstersPerWave];
            for (var index = 0; index < monstersPerWave; index++)
            {
                var offset = new Vector2(index * 2.4f - 1.2f, 0f);
                var monster = CreateMonsterInstance(
                    parent,
                    100 * waveIndex + index,
                    spawnPosition + offset,
                    patrolPoints,
                    navigationGraph,
                    noiseService,
                    config,
                    roundPhase,
                    monsterTierRuntime,
                    target);
                wave[index] = monster.GetComponent<NetworkMonsterAuthority>();
                monster.SetActive(false);
            }

            return wave;
        }

        private static Transform[][] CreateReinforcementPatrolRoutes(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var routeRoomIds = new[]
            {
                new[] { "QuarantineA", "LabA", "Power" },
                new[] { "QuarantineB", "VaccineB", "LabB" }
            };
            var routes = new Transform[routeRoomIds.Length][];
            for (var routeIndex = 0;
                 routeIndex < routeRoomIds.Length;
                 routeIndex++)
            {
                var routeRoot =
                    new GameObject(
                        $"ReinforcementRoute_{routeIndex + 1:00}")
                        .transform;
                routeRoot.SetParent(parent);
                var roomIds = routeRoomIds[routeIndex];
                routes[routeIndex] = new Transform[roomIds.Length];
                for (var pointIndex = 0;
                     pointIndex < roomIds.Length;
                     pointIndex++)
                {
                    var point =
                        new GameObject(
                            $"Patrol_{pointIndex + 1:00}_{roomIds[pointIndex]}");
                    point.transform.SetParent(routeRoot);
                    point.transform.position =
                        rooms[roomIds[pointIndex]].Position;
                    routes[routeIndex][pointIndex] = point.transform;
                }
            }

            return routes;
        }

        private static NetworkMonsterAuthority[] CreateMonsters(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            var config = EnsureMonsterBalanceConfig();
            var patrolRoutes = CreateMonsterPatrolRoutes(parent, rooms);
            var baseMonsters =
                new NetworkMonsterAuthority[MonsterSpawnRoomIds.Length];
            for (var index = 0; index < MonsterSpawnRoomIds.Length; index++)
            {
                var monster = CreateMonsterInstance(
                    parent,
                    index,
                    rooms[MonsterSpawnRoomIds[index]].Position,
                    patrolRoutes[index],
                    navigationGraph,
                    noiseService,
                    config,
                    roundPhase,
                    monsterTierRuntime,
                    target);
                baseMonsters[index] =
                    monster.GetComponent<NetworkMonsterAuthority>();
            }

            return baseMonsters;
        }

        private static GameObject CreateMonsterInstance(
            Transform parent,
            int monsterIndex,
            Vector2 spawnPosition,
            Transform[] patrolPoints,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            var monster = new GameObject($"P_Monster_{monsterIndex + 1:00}");
            monster.transform.SetParent(parent);
            monster.transform.position = spawnPosition;
            var body = monster.AddComponent<Rigidbody2D>();
            ConfigureDynamicBody(body);
            var collider = monster.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1.65f, 1.7f);

            var visual = CreateSpriteObject(
                "Visual",
                LoadSprite(MonsterSpritePath),
                Vector2.zero,
                new Vector2(2.3f, 2.3f),
                Color.white,
                41,
                monster.transform);
            visual.transform.localPosition = Vector3.zero;
            var eye = CreateSpriteObject(
                "RX9Eye",
                LoadSprite(CircleSpritePath),
                new Vector2(0f, 0.32f),
                new Vector2(0.26f, 0.16f),
                new Color(1f, 0.16f, 0.2f),
                43,
                monster.transform);
            eye.transform.localPosition = new Vector3(0f, 0.32f, 0f);

            var senses = monster.AddComponent<MonsterSenses>();
            senses.Configure(
                config,
                monsterTierRuntime,
                target,
                Physics2D.DefaultRaycastLayers,
                navigationGraph);
            var biteController = monster.AddComponent<MonsterBiteController>();
            biteController.Configure(config, senses, target);
            var brain = monster.AddComponent<MonsterBrain>();
            brain.Configure(
                body,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                senses,
                biteController,
                patrolPoints);
            monster.AddComponent<MonsterPrototypePresenter>()
                .Configure(brain, eye.GetComponent<SpriteRenderer>(), null);
            var networkObject = monster.AddComponent<NetworkObject>();
            networkObject.ActiveSceneSynchronization = true;
            var networkTransform = monster.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode =
                NetworkTransform.AuthorityModes.Server;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = false;
            networkTransform.SyncRotAngleZ = false;
            networkTransform.SyncPositionZ = false;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;
            networkTransform.UseUnreliableDeltas = true;
            monster.AddComponent<NetworkMonsterAuthority>().Configure(
                brain,
                body,
                networkTransform);
            return monster;
        }

        private static Transform[][] CreateMonsterPatrolRoutes(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var root = new GameObject("[AI] MonsterPatrolRoutes").transform;
            root.SetParent(parent);
            var routeRoomIds = new[]
            {
                new[] { "VaccineA", "LabA", "Storage" },
                new[] { "QuarantineA", "Power", "Security" },
                new[] { "Ward", "LabB", "Security" },
                new[] { "QuarantineB", "VaccineB", "LabB" }
            };
            var routes = new Transform[routeRoomIds.Length][];
            for (var routeIndex = 0;
                 routeIndex < routeRoomIds.Length;
                 routeIndex++)
            {
                var routeRoot =
                    new GameObject(
                        $"MonsterPatrolRoute_{routeIndex + 1:00}")
                        .transform;
                routeRoot.SetParent(root);
                var roomIds = routeRoomIds[routeIndex];
                routes[routeIndex] = new Transform[roomIds.Length];
                for (var pointIndex = 0;
                     pointIndex < roomIds.Length;
                     pointIndex++)
                {
                    var point =
                        new GameObject(
                            $"Patrol_{pointIndex + 1:00}_{roomIds[pointIndex]}");
                    point.transform.SetParent(routeRoot);
                    point.transform.position =
                        rooms[roomIds[pointIndex]].Position;
                    routes[routeIndex][pointIndex] = point.transform;
                }
            }

            return routes;
        }

        private static void CreateInfectionPrototype(
            Transform parent,
            GameObject player,
            MonsterTarget target,
            MonsterTierRuntime monsterTierRuntime)
        {
            var config = AssetDatabase.LoadAssetAtPath<AntidoteBalanceConfig>(
                AntidoteBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AntidoteBalanceConfig>();
                config.name = "SO_AntidoteBalance_Default";
                AssetDatabase.CreateAsset(config, AntidoteBalanceConfigPath);
            }

            var infectionService = player.AddComponent<InfectionService>();
            infectionService.Configure(target, monsterTierRuntime);
            var antidoteService = player.AddComponent<AntidoteService>();
            antidoteService.Configure(
                config,
                infectionService,
                player.GetComponent<PlayerInputReader>(),
                player.GetComponent<PlayerMotor>());
            var hudObject = new GameObject("[UI] InfectionHud");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<InfectionHudView>()
                .Configure(infectionService, antidoteService);
        }

        private static NoiseService CreateNoiseService(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<NoiseBalanceConfig>(
                NoiseBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<NoiseBalanceConfig>();
                config.name = "SO_NoiseBalance_Default";
                AssetDatabase.CreateAsset(config, NoiseBalanceConfigPath);
            }

            var serviceObject = new GameObject("[Gameplay] NoiseService");
            serviceObject.transform.SetParent(parent);
            var service = serviceObject.AddComponent<NoiseService>();
            service.Configure(config);
            return service;
        }

        private static LocalRoundPhasePrototype CreateRoundPhase(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<RoundBalanceConfig>(
                RoundBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
                config.name = "SO_RoundBalance_Default";
                AssetDatabase.CreateAsset(config, RoundBalanceConfigPath);
            }

            EditorUtility.SetDirty(config);
            var roundObject = new GameObject("[Gameplay] LocalRoundPhase");
            roundObject.transform.SetParent(parent);
            var round = roundObject.AddComponent<LocalRoundPhasePrototype>();
            round.Configure(config);
            return round;
        }

        private static NetworkRoundState CreateNetworkRoundState(
            Transform parent,
            LocalRoundPhasePrototype localRoundPhase,
            NetworkFuseStationAuthority[] missionStations)
        {
            var roundObject = new GameObject("[Network] RoundState");
            roundObject.transform.SetParent(parent);
            roundObject.AddComponent<NetworkObject>();
            var networkRound =
                roundObject.AddComponent<NetworkRoundState>();
            networkRound.Configure(
                localRoundPhase.Config,
                localRoundPhase,
                missionStations);
            return networkRound;
        }

        private static MonsterTierRuntime CreateMonsterTierRuntime(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterTierConfig>(
                MonsterTierConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterTierConfig>();
                config.name = "SO_MonsterTier_Default";
                AssetDatabase.CreateAsset(config, MonsterTierConfigPath);
            }

            var runtimeObject = new GameObject("[Gameplay] MonsterTierRuntime");
            runtimeObject.transform.SetParent(parent);
            var runtime = runtimeObject.AddComponent<MonsterTierRuntime>();
            runtime.Configure(config);
            return runtime;
        }

        private static void CreateGracePeriodView(
            Transform parent,
            LocalRoundPhasePrototype roundPhase)
        {
            var viewObject = new GameObject("[UI] GracePeriod");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<GracePeriodView>().Configure(roundPhase);
        }

        private static void CreateRoundHudView(Transform parent)
        {
            var viewObject = new GameObject("[UI] RoundHud");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<RoundHudView>();
        }

        private static void CreateVillainUpgradeHudView(Transform parent)
        {
            var viewObject = new GameObject("[UI] VillainUpgradeHud");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<VillainUpgradeHudView>();
        }

        private static void CreateFuseMissionView(
            Transform parent,
            FuseStationPrototype station,
            string viewName)
        {
            var viewObject = new GameObject(viewName);
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<FuseMissionView>().Configure(station);
        }

        private static void CreateNoiseAlertView(
            Transform parent,
            NoiseService noiseService)
        {
            var viewObject = new GameObject("[UI] NoiseAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<NoiseAlertView>().Configure(noiseService);
        }

        private static void CreateMonsterBiteAlertView(
            Transform parent,
            MonsterTarget target)
        {
            var viewObject = new GameObject("[UI] MonsterBiteAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<MonsterBiteAlertView>().Configure(target);
        }

        private static GameObject CreateSpriteObject(
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent);
            instance.transform.position = position;
            instance.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return instance;
        }

        private static void ConfigureDynamicBody(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 8f;
            body.angularDamping = 8f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private static void ClearOldLaboratoryObjects()
        {
            foreach (var objectName in new[]
                     {
                         "[Prototype] FirstPlayable", "[Map] LaboratoryBlockout",
                         "[Map] Laboratory2D", "[Map] RoomWalls",
                         "[Network] GameplayScene", "Directional Light",
                         "[UI] SceneInfo"
                     })
            {
                var target = GameObject.Find(objectName);
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void EnsureSpriteAssets()
        {
            EnsureFolder("Assets/_Project/Art", "Sprites");
            EnsureFolder("Assets/_Project/Art/Sprites", "Generated");
            EnsureFolder("Assets/_Project/Art/Sprites", "Characters");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImportedSprite(PlayerSpritePath, 1024f);
            ConfigureImportedSprite(MonsterSpritePath, 1024f);
            EnsureSprite(
                UnitSpritePath,
                "S_UnitSquare",
                8,
                8,
                (_, _) => new Color32(255, 255, 255, 255),
                new Vector2(0.5f, 0.5f),
                8f);
            EnsureSprite(
                VisorSpritePath,
                "S_Player_Visor",
                64,
                32,
                (x, y) => IsRoundedRect(x, y, 64, 32, 12)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                64f);
            EnsureSprite(
                CircleSpritePath,
                "S_StatusCircle",
                32,
                32,
                (x, y) => IsInsideEllipse(x, y, 15.5f, 15.5f, 14f, 14f)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                32f);
            EnsureSprite(
                PanelSpritePath,
                "S_MissionPanel",
                64,
                52,
                (x, y) => IsRoundedRect(x, y, 64, 52, 8)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                64f);
            EnsureSprite(
                FlashlightSpritePath,
                "S_FlashlightCone",
                128,
                160,
                CreateFlashlightPixel,
                new Vector2(0.5f, 0f),
                64f);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureImportedSprite(
            string path,
            float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    "Character sprite texture is missing: " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Color32 CreateFlashlightPixel(int x, int y)
        {
            var normalizedY = y / 159f;
            var halfWidth = Mathf.Lerp(4f, 62f, normalizedY);
            var distanceFromCenter = Mathf.Abs(x - 63.5f);
            if (distanceFromCenter > halfWidth)
            {
                return new Color32(0, 0, 0, 0);
            }

            var edgeFade = 1f - Mathf.Clamp01(
                distanceFromCenter / Mathf.Max(halfWidth, 1f));
            var distanceFade = 1f - normalizedY * 0.72f;
            var alpha = (byte)Mathf.RoundToInt(
                62f * edgeFade * distanceFade);
            return new Color32(118, 225, 255, alpha);
        }

        private static Sprite EnsureSprite(
            string path,
            string spriteName,
            int width,
            int height,
            Func<int, int, Color32> pixelFactory,
            Vector2 pivot,
            float pixelsPerUnit)
        {
            var existing = LoadSprite(path, false);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "T_" + spriteName[2..],
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixelFactory(x, y);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }

        private static Sprite LoadSprite(string path, bool throwIfMissing = true)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }

            if (throwIfMissing)
            {
                throw new InvalidOperationException(
                    "Generated sprite is missing: " + path);
            }

            return null;
        }

        private static bool IsRoundedRect(
            int x,
            int y,
            int width,
            int height,
            int radius)
        {
            var clampedX = Mathf.Clamp(x, radius, width - radius - 1);
            var clampedY = Mathf.Clamp(y, radius, height - radius - 1);
            var dx = x - clampedX;
            var dy = y - clampedY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool IsInsideEllipse(
            float x,
            float y,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY)
        {
            var dx = (x - centerX) / radiusX;
            var dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy <= 1f;
        }

        private static float DistanceToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                                  lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            var path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Color GetRoomColor(string roomId)
        {
            return roomId switch
            {
                "VaccineA" or "VaccineB" => new Color(0.14f, 0.30f, 0.31f),
                "QuarantineA" or "QuarantineB" => new Color(0.28f, 0.16f, 0.20f),
                "Power" => new Color(0.30f, 0.25f, 0.12f),
                "Security" => new Color(0.13f, 0.22f, 0.31f),
                "Ward" => new Color(0.20f, 0.27f, 0.28f),
                _ => new Color(0.15f, 0.23f, 0.27f)
            };
        }

        private static void ValidateCorridorLayout(List<string> failures)
        {
            foreach (var corridor in CorridorDefinitions)
            {
                if (!TryGetRoomDefinition(corridor.A, out var roomA) ||
                    !TryGetRoomDefinition(corridor.B, out var roomB))
                {
                    failures.Add(
                        $"Corridor {corridor.A}-{corridor.B} references an unknown room.");
                    continue;
                }

                if (corridor.PathPoints.Count < 2 ||
                    !IsEndpointOnRoomSide(
                        roomA,
                        corridor.Start,
                        corridor.SideA) ||
                    !IsEndpointOnRoomSide(
                        roomB,
                        corridor.End,
                        corridor.SideB))
                {
                    failures.Add(
                        $"Corridor {corridor.A}-{corridor.B} has an invalid room connection.");
                    continue;
                }

                for (var index = 1;
                     index < corridor.PathPoints.Count;
                     index++)
                {
                    var start = corridor.PathPoints[index - 1];
                    var end = corridor.PathPoints[index];
                    var delta = end - start;
                    if (Mathf.Abs(delta.x) > 0.001f &&
                        Mathf.Abs(delta.y) > 0.001f)
                    {
                        failures.Add(
                            $"Corridor {corridor.A}-{corridor.B} contains a diagonal segment.");
                        break;
                    }

                    var size = Mathf.Abs(delta.x) > 0.001f
                        ? new Vector2(Mathf.Abs(delta.x), CorridorWidth)
                        : new Vector2(CorridorWidth, Mathf.Abs(delta.y));
                    var segmentArea = CreateRect(
                        (start + end) * 0.5f,
                        size);
                    foreach (var room in RoomDefinitions)
                    {
                        if (room.Id == corridor.A ||
                            room.Id == corridor.B)
                        {
                            continue;
                        }

                        if (segmentArea.Overlaps(
                                CreateRect(room.Position, room.Size)))
                        {
                            failures.Add(
                                $"Corridor {corridor.A}-{corridor.B} crosses room {room.Id}.");
                            break;
                        }
                    }
                }
            }
        }

        private static bool TryGetRoomDefinition(
            string roomId,
            out RoomDefinition definition)
        {
            foreach (var room in RoomDefinitions)
            {
                if (room.Id != roomId)
                {
                    continue;
                }

                definition = room;
                return true;
            }

            definition = default;
            return false;
        }

        private static bool IsEndpointOnRoomSide(
            RoomDefinition room,
            Vector2 endpoint,
            WallSide side)
        {
            var halfSize = room.Size * 0.5f;
            return side switch
            {
                WallSide.North =>
                    Mathf.Approximately(
                        endpoint.y,
                        room.Position.y + halfSize.y) &&
                    Mathf.Abs(endpoint.x - room.Position.x) <= halfSize.x,
                WallSide.South =>
                    Mathf.Approximately(
                        endpoint.y,
                        room.Position.y - halfSize.y) &&
                    Mathf.Abs(endpoint.x - room.Position.x) <= halfSize.x,
                WallSide.East =>
                    Mathf.Approximately(
                        endpoint.x,
                        room.Position.x + halfSize.x) &&
                    Mathf.Abs(endpoint.y - room.Position.y) <= halfSize.y,
                WallSide.West =>
                    Mathf.Approximately(
                        endpoint.x,
                        room.Position.x - halfSize.x) &&
                    Mathf.Abs(endpoint.y - room.Position.y) <= halfSize.y,
                _ => false
            };
        }

        private static void RequireComponent<T>(
            GameObject source,
            List<string> failures)
            where T : Component
        {
            if (source == null || source.GetComponent<T>() == null)
            {
                failures.Add(
                    "P_Player_Local is missing " + typeof(T).Name + ".");
            }
        }

        private static void MonitorRuntimeMonsterTest()
        {
            if (!EditorApplication.isPlaying || _runtimeTestMonster == null ||
                _runtimeTestTarget == null || _runtimeTestInfection == null)
            {
                StopRuntimeMonsterTest();
                return;
            }

            if (_runtimeTestMonster.State is MonsterState.Chase or MonsterState.Bite)
            {
                _runtimeTestObservedChase = true;
            }

            if (_runtimeTestTarget.BiteCount > _runtimeTestInitialBiteCount)
            {
                if (!_runtimeTestObservedChase ||
                    !_runtimeTestInfection.IsInfected ||
                    !Mathf.Approximately(
                        _runtimeTestInfection.DurationAtBiteSeconds,
                        90f))
                {
                    StopRuntimeMonsterTest();
                    throw new InvalidOperationException(
                        "The 2D chase, bite or infection result was invalid.");
                }

                if (_runtimeTestMonster.State == MonsterState.Patrol)
                {
                    _runtimeTestObservedPatrolAfterBite = true;
                }

                if (_runtimeTestObservedPatrolAfterBite)
                {
                    Debug.Log(
                        "[MonkeyLab] 2D monster chase, bite, infection and patrol release passed.");
                    StopRuntimeMonsterTest();
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _runtimeTestStartedAt >
                RuntimeMonsterTestTimeoutSeconds)
            {
                var state = _runtimeTestMonster.State;
                var canDetect = _runtimeTestMonster.Senses.TryDetectTarget(
                    out var detectionType);
                var monsterCollider =
                    _runtimeTestMonster.GetComponent<Collider2D>();
                var targetCollider =
                    _runtimeTestTarget.GetComponent<Collider2D>();
                var surfaceDistance =
                    monsterCollider != null && targetCollider != null
                        ? monsterCollider.Distance(targetCollider).distance
                        : float.NaN;
                var hasClearPath =
                    _runtimeTestMonster.Senses.HasClearPathToTarget();
                var isInBiteRange = _runtimeTestMonster.Senses
                    .IsTargetInBiteRange();
                var biteController = _runtimeTestMonster.BiteController;
                var biteTargetMatches =
                    biteController.Target == _runtimeTestTarget;
                var isBiteProtected =
                    _runtimeTestTarget.IsBiteProtected(Time.time);
                var monsterPosition = _runtimeTestMonster.transform.position;
                var targetPosition = _runtimeTestTarget.transform.position;
                StopRuntimeMonsterTest();
                throw new InvalidOperationException(
                    $"2D monster chase and bite timed out. State={state}, " +
                    $"canDetect={canDetect}, detection={detectionType}, " +
                    $"pathClear={hasClearPath}, " +
                    $"biteRange={isInBiteRange}, " +
                    $"bitePending={biteController.IsPending}, " +
                    $"biteTargetMatches={biteTargetMatches}, " +
                    $"biteProtected={isBiteProtected}, " +
                    $"surfaceDistance={surfaceDistance:0.00}, " +
                    $"monster={monsterPosition}, target={targetPosition}.");
            }
        }

        private static void HandleRuntimeMonsterStateChanged(
            MonsterBrain monster,
            MonsterState state)
        {
            Debug.Log(
                $"[MonkeyLab] Runtime monster test state={state}, " +
                $"position={monster.transform.position}.");
        }

        private static void StopRuntimeMonsterTest()
        {
            EditorApplication.update -= MonitorRuntimeMonsterTest;
            if (_runtimeTestMonster != null)
            {
                _runtimeTestMonster.StateChanged -=
                    HandleRuntimeMonsterStateChanged;
            }

            _runtimeTestMonster = null;
            _runtimeTestTarget = null;
            _runtimeTestInfection = null;
        }

        private static void MonitorRuntimeAntidoteTest()
        {
            if (!EditorApplication.isPlaying ||
                _runtimeAntidoteTestInfection == null ||
                _runtimeAntidoteTestService == null)
            {
                StopRuntimeAntidoteTest();
                return;
            }

            if (_runtimeAntidoteTestInfection.State ==
                    PlayerLifeState.AliveHealthy &&
                !_runtimeAntidoteTestService.HasAntidote &&
                !_runtimeAntidoteTestService.IsUsing)
            {
                Debug.Log(
                    "[MonkeyLab] 2D infection and antidote validation passed.");
                StopRuntimeAntidoteTest();
                return;
            }

            if (EditorApplication.timeSinceStartup -
                _runtimeAntidoteTestStartedAt >
                RuntimeAntidoteTestTimeoutSeconds)
            {
                StopRuntimeAntidoteTest();
                throw new InvalidOperationException(
                    "Infection and antidote validation timed out.");
            }
        }

        private static void StopRuntimeAntidoteTest()
        {
            EditorApplication.update -= MonitorRuntimeAntidoteTest;
            _runtimeAntidoteTestInfection = null;
            _runtimeAntidoteTestService = null;
        }

        private readonly struct RoomDefinition
        {
            public RoomDefinition(
                string id,
                Vector2 position,
                Vector2 size,
                string displayName)
            {
                Id = id;
                Position = position;
                Size = size;
                DisplayName = displayName;
            }

            public string Id { get; }
            public Vector2 Position { get; }
            public Vector2 Size { get; }
            public string DisplayName { get; }
        }

        private readonly struct CorridorDefinition
        {
            public CorridorDefinition(
                string a,
                WallSide sideA,
                string b,
                WallSide sideB,
                params Vector2[] pathPoints)
            {
                A = a;
                SideA = sideA;
                B = b;
                SideB = sideB;
                PathPoints = pathPoints;
            }

            public string A { get; }
            public WallSide SideA { get; }
            public string B { get; }
            public WallSide SideB { get; }
            public IReadOnlyList<Vector2> PathPoints { get; }
            public Vector2 Start => PathPoints[0];
            public Vector2 End => PathPoints[^1];
        }

        private readonly struct BoundaryEdge
        {
            public BoundaryEdge(
                bool isHorizontal,
                float fixedCoordinate,
                float start,
                float end)
            {
                IsHorizontal = isHorizontal;
                FixedCoordinate = fixedCoordinate;
                Start = start;
                End = end;
            }

            public bool IsHorizontal { get; }
            public float FixedCoordinate { get; }
            public float Start { get; }
            public float End { get; }
        }

        private enum WallSide
        {
            North,
            South,
            East,
            West
        }
    }
}
