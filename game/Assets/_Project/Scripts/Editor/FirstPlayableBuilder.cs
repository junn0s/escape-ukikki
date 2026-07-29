using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    public static class FirstPlayableBuilder
    {
        private const string LaboratoryScenePath = "Assets/_Project/Scenes/10_Laboratory.unity";
        private const string InputActionsPath = "Assets/_Project/Settings/PlayerControls.inputactions";
        private const string MovementConfigPath = "Assets/_Project/Data/Balance/SO_PlayerMovement_Default.asset";
        private const string FuseMissionConfigPath = "Assets/_Project/Data/Missions/SO_FuseMission_Default.asset";
        private const string NoiseBalanceConfigPath = "Assets/_Project/Data/Balance/SO_NoiseBalance_Default.asset";
        private const string MonsterBalanceConfigPath = "Assets/_Project/Data/Balance/SO_MonsterBalance_Default.asset";
        private const string MonsterTierConfigPath = "Assets/_Project/Data/Balance/SO_MonsterTier_Default.asset";
        private const string RoundBalanceConfigPath = "Assets/_Project/Data/Balance/SO_RoundBalance_Default.asset";
        private const string LaboratoryNavMeshPath = "Assets/_Project/Data/Maps/NavMesh_Laboratory.asset";
        private const string MaterialRoot = "Assets/_Project/Art/Materials";
        private const float FloorTop = 0.15f;
        private const double RuntimeMonsterTestTimeoutSeconds = 5d;

        private static MonsterBrain _runtimeTestMonster;
        private static MonsterTarget _runtimeTestTarget;
        private static int _runtimeTestInitialBiteCount;
        private static double _runtimeTestStartedAt;
        private static bool _runtimeTestObservedChase;

        private static readonly (string A, string B)[] RoomLinks =
        {
            ("VaccineA", "LabA"), ("LabA", "QuarantineA"), ("VaccineA", "Storage"),
            ("LabA", "Security"), ("QuarantineA", "Power"), ("Storage", "Security"),
            ("Security", "Power"), ("Storage", "Ward"), ("Ward", "LabB"),
            ("Security", "LabB"), ("Power", "QuarantineB"), ("LabB", "QuarantineB"),
            ("LabB", "VaccineB"), ("QuarantineB", "VaccineB")
        };

        [MenuItem("Tools/Monkey Lab/Build First Playable")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Exit Play Mode before building the first playable.");
            }

            var scene = EditorSceneManager.OpenScene(LaboratoryScenePath, OpenSceneMode.Single);
            var oldPrototype = GameObject.Find("[Prototype] FirstPlayable");
            if (oldPrototype != null)
            {
                UnityEngine.Object.DestroyImmediate(oldPrototype);
            }

            var prototypeRoot = new GameObject("[Prototype] FirstPlayable");
            var spawnPosition = ConvertSpawnMarkers();
            CreateRoomWalls(prototypeRoot.transform);
            var roundPhase = CreateRoundPhase(prototypeRoot.transform);
            CreateGracePeriodView(prototypeRoot.transform, roundPhase);
            var monsterTierRuntime = CreateMonsterTierRuntime(prototypeRoot.transform);
            var noiseService = CreateNoiseService(prototypeRoot.transform);
            BuildNavigation(prototypeRoot.transform);
            var fuseStation = CreateFuseStation(prototypeRoot.transform);
            fuseStation.gameObject.AddComponent<FuseFailureNoiseEmitter>()
                .Configure(fuseStation, noiseService, "power");
            CreateFuseMissionView(prototypeRoot.transform, fuseStation);
            CreateNoiseAlertView(prototypeRoot.transform, noiseService);
            var player = CreatePlayer(prototypeRoot.transform, spawnPosition);
            var monsterTarget = player.GetComponent<MonsterTarget>();
            CreateMonsterBiteAlertView(prototypeRoot.transform, monsterTarget);
            CreateMonster(
                prototypeRoot.transform,
                noiseService,
                roundPhase,
                monsterTierRuntime,
                monsterTarget);
            ConfigureCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LaboratoryScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;

            Validate();
            Debug.Log("[MonkeyLab] M1 first playable is ready: WASD, mouse aim, F flashlight, E interaction.");
        }

        [MenuItem("Tools/Monkey Lab/Validate First Playable")]
        public static void Validate()
        {
            if (SceneManager.GetActiveScene().path != LaboratoryScenePath)
            {
                EditorSceneManager.OpenScene(LaboratoryScenePath, OpenSceneMode.Single);
            }

            var failures = new List<string>();
            var player = GameObject.Find("P_Player_Local");
            RequireComponent<CharacterController>(player, failures);
            RequireComponent<PlayerInputReader>(player, failures);
            RequireComponent<PlayerMotor>(player, failures);
            RequireComponent<PlayerAimController>(player, failures);
            RequireComponent<PlayerInteractor>(player, failures);
            RequireComponent<MonsterTarget>(player, failures);

            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (roundPhase == null || roundPhase.Config == null ||
                string.IsNullOrEmpty(roundPhase.Config.Id) ||
                !Mathf.Approximately(roundPhase.Config.InitialGracePeriodSeconds, 30f))
            {
                failures.Add("Local round phase or its 30 second grace config is missing.");
            }

            if (GameObject.Find("[UI] GracePeriod")?.GetComponent<GracePeriodView>() == null ||
                GameObject.Find("[UI] MonsterBiteAlert")?.GetComponent<MonsterBiteAlertView>() == null)
            {
                failures.Add("Grace period or monster bite feedback view is missing.");
            }

            var monsterTierRuntime = GameObject.Find("[Gameplay] MonsterTierRuntime")?
                .GetComponent<MonsterTierRuntime>();
            if (monsterTierRuntime == null || monsterTierRuntime.Config == null ||
                monsterTierRuntime.Config.Id != "monster_tier_default" ||
                !Mathf.Approximately(monsterTierRuntime.Config.GetSmellRadius(0), 0.5f) ||
                !Mathf.Approximately(monsterTierRuntime.Config.GetSmellRadius(1), 1f) ||
                !Mathf.Approximately(monsterTierRuntime.Config.GetSmellRadius(2), 2f))
            {
                failures.Add("Monster tier runtime or smell tier balance values are missing.");
            }

            var mainCamera = Camera.main;
            if (mainCamera == null || mainCamera.GetComponent<QuarterViewCamera>() == null)
            {
                failures.Add("Main Camera is missing QuarterViewCamera.");
            }

            var wallRoot = GameObject.Find("[Map] RoomWalls");
            if (wallRoot == null || wallRoot.transform.childCount < 20)
            {
                failures.Add("Room collision walls were not generated.");
            }

            var station = GameObject.Find("MissionStation_Fuse");
            var fuseStation = station != null ? station.GetComponent<FuseStationPrototype>() : null;
            if (fuseStation == null || fuseStation.Config == null)
            {
                failures.Add("Fuse mission station or config is missing.");
            }

            if (GameObject.Find("[UI] FuseMission")?.GetComponent<FuseMissionView>() == null)
            {
                failures.Add("Fuse mission view is missing.");
            }

            var noiseService = GameObject.Find("[Gameplay] NoiseService")?.GetComponent<NoiseService>();
            if (noiseService == null || noiseService.Config == null ||
                string.IsNullOrEmpty(noiseService.Config.Id))
            {
                failures.Add("NoiseService or its stable config is missing.");
            }

            if (fuseStation != null &&
                fuseStation.GetComponent<FuseFailureNoiseEmitter>()?.NoiseService != noiseService)
            {
                failures.Add("Fuse failure is not connected to NoiseService.");
            }

            if (GameObject.Find("[UI] NoiseAlert")?.GetComponent<NoiseAlertView>() == null)
            {
                failures.Add("Noise alert view is missing.");
            }

            var navigation = GameObject.Find("[Navigation] Laboratory")?.GetComponent<NavMeshSurface>();
            if (navigation == null || navigation.navMeshData == null)
            {
                failures.Add("Laboratory NavMesh was not built.");
            }

            var monster = GameObject.Find("P_Monster_01");
            var monsterBrain = monster != null ? monster.GetComponent<MonsterBrain>() : null;
            if (monsterBrain == null || monsterBrain.Config == null ||
                string.IsNullOrEmpty(monsterBrain.Config.Id) || monsterBrain.PatrolPointCount < 3 ||
                monster.GetComponent<NavMeshAgent>() == null ||
                monster.GetComponent<MonsterSenses>() == null ||
                monster.GetComponent<MonsterBiteController>() == null ||
                monsterBrain.RoundPhase != roundPhase ||
                monsterBrain.Senses?.TierRuntime != monsterTierRuntime ||
                monsterBrain.Senses?.Target != player?.GetComponent<MonsterTarget>())
            {
                failures.Add("Prototype monster chase, bite, config or patrol setup is missing.");
            }

            if (monster != null && !NavMesh.SamplePosition(
                    monster.transform.position,
                    out _,
                    monster.GetComponent<NavMeshAgent>()?.height ?? 2f,
                    NavMesh.AllAreas))
            {
                failures.Add("P_Monster_01 is not positioned on the NavMesh.");
            }

            var powerRoom = GameObject.Find("Room_Power");
            if (station != null && powerRoom != null &&
                Vector3.Distance(station.transform.position, powerRoom.transform.position) > 6f)
            {
                failures.Add("Fuse mission station must be located in the power room.");
            }

            if (player != null && !HasWalkableFloorBelow(player.transform.position))
            {
                failures.Add("P_Player_Local is not positioned above a room or corridor floor.");
            }

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null ||
                inputActions.FindAction("Gameplay/Move") == null ||
                inputActions.FindAction("Gameplay/Look") == null ||
                inputActions.FindAction("Gameplay/Interact") == null ||
                inputActions.FindAction("Gameplay/Flashlight") == null ||
                inputActions.FindAction("Gameplay/Cancel") == null)
            {
                failures.Add("Required player input actions are missing.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }

            Debug.Log("[MonkeyLab] First playable validation passed.");
        }

        [MenuItem("Tools/Monkey Lab/Test Fuse Failure Noise")]
        public static void TestFuseFailureNoise()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode before testing fuse failure noise.");
            }

            var player = GameObject.Find("P_Player_Local");
            var station = GameObject.Find("MissionStation_Fuse")?.GetComponent<FuseStationPrototype>();
            var monster = GameObject.Find("P_Monster_01")?.GetComponent<MonsterBrain>();
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (player == null || station == null || monster == null || roundPhase == null)
            {
                throw new InvalidOperationException("Runtime fuse noise test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            station.Interact(player);
            if (!station.IsMissionActive || station.RequiredOrder.Count == 0)
            {
                throw new InvalidOperationException("Fuse mission did not start during the runtime test.");
            }

            var expectedFuseId = station.RequiredOrder[0];
            var wrongFuseId = expectedFuseId == 1 ? 2 : 1;
            station.SubmitFuse(wrongFuseId);
            if (monster.State != MonsterState.InvestigateNoise)
            {
                throw new InvalidOperationException(
                    $"Monster did not investigate the fuse noise. Current state: {monster.State}.");
            }

            Debug.Log(
                $"[MonkeyLab] Runtime fuse noise validation passed: monster target noise={monster.CurrentNoiseId}.");
        }

        [MenuItem("Tools/Monkey Lab/Test Monster Chase And Bite")]
        public static void TestMonsterChaseAndBite()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode before testing monster chase and bite.");
            }

            StopRuntimeMonsterTest();
            var player = GameObject.Find("P_Player_Local");
            var target = player != null ? player.GetComponent<MonsterTarget>() : null;
            var monster = GameObject.Find("P_Monster_01");
            var brain = monster != null ? monster.GetComponent<MonsterBrain>() : null;
            var agent = monster != null ? monster.GetComponent<NavMeshAgent>() : null;
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (player == null || target == null || monster == null || brain == null ||
                agent == null || roundPhase == null)
            {
                throw new InvalidOperationException("Runtime monster chase and bite test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            var desiredPosition = player.transform.position - Vector3.forward *
                (brain.Config.BiteDistance * 0.8f);
            if (!NavMesh.SamplePosition(
                    desiredPosition,
                    out var hit,
                    brain.Config.BiteDistance * 2f,
                    NavMesh.AllAreas) ||
                !agent.Warp(hit.position))
            {
                throw new InvalidOperationException("Monster could not be moved near the player on the NavMesh.");
            }

            var facing = Vector3.ProjectOnPlane(player.transform.position - monster.transform.position, Vector3.up);
            if (facing.sqrMagnitude > Mathf.Epsilon)
            {
                monster.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }

            agent.ResetPath();
            Physics.SyncTransforms();
            _runtimeTestMonster = brain;
            _runtimeTestTarget = target;
            _runtimeTestInitialBiteCount = target.BiteCount;
            _runtimeTestStartedAt = EditorApplication.timeSinceStartup;
            _runtimeTestObservedChase = false;
            EditorApplication.update += MonitorRuntimeMonsterTest;
        }

        private static void MonitorRuntimeMonsterTest()
        {
            if (!EditorApplication.isPlaying || _runtimeTestMonster == null || _runtimeTestTarget == null)
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
                if (!_runtimeTestObservedChase)
                {
                    StopRuntimeMonsterTest();
                    throw new InvalidOperationException("Monster bit the player without entering chase.");
                }

                Debug.Log(
                    $"[MonkeyLab] Runtime monster validation passed: " +
                    $"detection={_runtimeTestMonster.LastDetectionType}, bites={_runtimeTestTarget.BiteCount}.");
                StopRuntimeMonsterTest();
                return;
            }

            if (EditorApplication.timeSinceStartup - _runtimeTestStartedAt > RuntimeMonsterTestTimeoutSeconds)
            {
                var state = _runtimeTestMonster.State;
                StopRuntimeMonsterTest();
                throw new InvalidOperationException(
                    $"Monster chase and bite validation timed out. Current state: {state}.");
            }
        }

        private static void StopRuntimeMonsterTest()
        {
            EditorApplication.update -= MonitorRuntimeMonsterTest;
            _runtimeTestMonster = null;
            _runtimeTestTarget = null;
        }

        private static GameObject CreatePlayer(Transform parent, Vector3 spawnPosition)
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException("PlayerControls.inputactions could not be loaded.");
            }

            var movementConfig = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(MovementConfigPath);
            if (movementConfig == null)
            {
                movementConfig = ScriptableObject.CreateInstance<PlayerMovementConfig>();
                movementConfig.name = "SO_PlayerMovement_Default";
                AssetDatabase.CreateAsset(movementConfig, MovementConfigPath);
            }

            var characterMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/M_BlockoutCharacter.mat");
            var accentMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/M_BlockoutAccent.mat");

            var player = new GameObject("P_Player_Local");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(spawnPosition.x, FloorTop + 0.01f, spawnPosition.z);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.42f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;

            var input = player.AddComponent<PlayerInputReader>();
            input.Configure(inputActions);
            var motor = player.AddComponent<PlayerMotor>();
            motor.Configure(input, controller, movementConfig);
            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.Configure(input, 1.5f);
            player.AddComponent<MonsterTarget>().Configure(true, true);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.85f, 0.9f, 0.85f);
            visual.GetComponent<Renderer>().sharedMaterial = characterMaterial;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

            var facingMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facingMarker.name = "FacingMarker";
            facingMarker.transform.SetParent(player.transform, false);
            facingMarker.transform.localPosition = new Vector3(0f, 1.05f, 0.47f);
            facingMarker.transform.localScale = new Vector3(0.24f, 0.24f, 0.3f);
            facingMarker.GetComponent<Renderer>().sharedMaterial = accentMaterial;
            UnityEngine.Object.DestroyImmediate(facingMarker.GetComponent<Collider>());

            var flashlightObject = new GameObject("Flashlight");
            flashlightObject.transform.SetParent(player.transform, false);
            flashlightObject.transform.localPosition = new Vector3(0f, 1.25f, 0.35f);
            flashlightObject.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            var flashlight = flashlightObject.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 14f;
            flashlight.spotAngle = 55f;
            flashlight.intensity = 8f;
            flashlight.shadows = LightShadows.Soft;
            var flashlightController = flashlightObject.AddComponent<FlashlightController>();
            flashlightController.Configure(input, flashlight, true);

            var promptObject = new GameObject("[UI] InteractionPrompt");
            promptObject.transform.SetParent(parent);
            promptObject.AddComponent<InteractionPromptView>().Configure(interactor);
            return player;
        }

        private static void ConfigureCamera(Transform player)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                throw new InvalidOperationException("Main Camera was not found.");
            }

            var follow = mainCamera.GetComponent<QuarterViewCamera>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<QuarterViewCamera>();
            }

            follow.Configure(player, new Vector3(0f, 14f, -11f), 0.16f);
            var aim = player.GetComponent<PlayerAimController>() ?? player.gameObject.AddComponent<PlayerAimController>();
            aim.Configure(player.GetComponent<PlayerInputReader>(), mainCamera, player.GetComponent<PlayerMotor>() != null
                ? AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(MovementConfigPath)
                : null);
        }

        private static Vector3 ConvertSpawnMarkers()
        {
            var firstSpawn = Vector3.zero;
            for (var index = 1; index <= ProjectBootstrap.LaboratoryPlayerSpawnPositions.Length; index++)
            {
                var marker = GameObject.Find($"PlayerSpawn_{index:00}");
                if (marker == null)
                {
                    continue;
                }

                var configuredPosition = ProjectBootstrap.LaboratoryPlayerSpawnPositions[index - 1];
                marker.transform.position = new Vector3(configuredPosition.x, FloorTop, configuredPosition.z);

                if (index == 1)
                {
                    firstSpawn = marker.transform.position;
                }

                foreach (var collider in marker.GetComponents<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                foreach (var renderer in marker.GetComponents<Renderer>())
                {
                    UnityEngine.Object.DestroyImmediate(renderer);
                }

                foreach (var filter in marker.GetComponents<MeshFilter>())
                {
                    UnityEngine.Object.DestroyImmediate(filter);
                }

            }

            return firstSpawn;
        }

        private static bool HasWalkableFloorBelow(Vector3 position)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(
                position + Vector3.up * 2f,
                Vector3.down,
                4f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                var objectName = hit.collider.gameObject.name;
                if (objectName.StartsWith("Room_", StringComparison.Ordinal) ||
                    objectName.StartsWith("Corridor_", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CreateRoomWalls(Transform parent)
        {
            var oldWalls = GameObject.Find("[Map] RoomWalls");
            if (oldWalls != null)
            {
                UnityEngine.Object.DestroyImmediate(oldWalls);
            }

            var wallRoot = new GameObject("[Map] RoomWalls");
            wallRoot.transform.SetParent(parent);
            var wallMaterial = EnsureMaterial("M_BlockoutWall", new Color(0.08f, 0.16f, 0.18f), 0.1f, 0.25f);

            var rooms = new Dictionary<string, Transform>();
            foreach (var roomName in new[]
                     {
                         "VaccineA", "LabA", "QuarantineA", "Storage", "Security",
                         "Power", "Ward", "LabB", "QuarantineB", "VaccineB"
                     })
            {
                var roomObject = GameObject.Find("Room_" + roomName);
                if (roomObject == null)
                {
                    throw new InvalidOperationException("Missing room block: " + roomName);
                }

                rooms[roomName] = roomObject.transform;
            }

            var openings = new Dictionary<string, HashSet<WallSide>>();
            foreach (var roomName in rooms.Keys)
            {
                openings[roomName] = new HashSet<WallSide>();
            }

            foreach (var link in RoomLinks)
            {
                openings[link.A].Add(GetSide(rooms[link.A].position, rooms[link.B].position));
                openings[link.B].Add(GetSide(rooms[link.B].position, rooms[link.A].position));
            }

            foreach (var room in rooms)
            {
                CreateRoomShell(room.Key, room.Value, openings[room.Key], wallMaterial, wallRoot.transform);
            }
        }

        private static void CreateRoomShell(
            string roomName,
            Transform room,
            HashSet<WallSide> openings,
            Material material,
            Transform parent)
        {
            const float height = 2.8f;
            const float thickness = 0.25f;
            const float doorWidth = 2.2f;
            var width = Mathf.Abs(room.localScale.x);
            var depth = Mathf.Abs(room.localScale.z);
            var position = room.position;

            CreateWallSide(roomName, WallSide.North, position, width, depth, height, thickness, doorWidth, openings, material, parent);
            CreateWallSide(roomName, WallSide.South, position, width, depth, height, thickness, doorWidth, openings, material, parent);
            CreateWallSide(roomName, WallSide.East, position, width, depth, height, thickness, doorWidth, openings, material, parent);
            CreateWallSide(roomName, WallSide.West, position, width, depth, height, thickness, doorWidth, openings, material, parent);
        }

        private static void CreateWallSide(
            string roomName,
            WallSide side,
            Vector3 roomPosition,
            float width,
            float depth,
            float height,
            float thickness,
            float doorWidth,
            HashSet<WallSide> openings,
            Material material,
            Transform parent)
        {
            var horizontal = side is WallSide.North or WallSide.South;
            var length = horizontal ? width : depth;
            var sideOffset = horizontal ? depth * 0.5f : width * 0.5f;
            var hasOpening = openings.Contains(side);
            var centerY = FloorTop + height * 0.5f;

            if (!hasOpening)
            {
                var center = roomPosition + SideDirection(side) * sideOffset;
                center.y = centerY;
                var scale = horizontal
                    ? new Vector3(length, height, thickness)
                    : new Vector3(thickness, height, length);
                CreateWall(roomName + "_" + side, center, scale, material, parent);
                return;
            }

            var segmentLength = (length - doorWidth) * 0.5f;
            var tangentOffset = (doorWidth + segmentLength) * 0.5f;
            var baseCenter = roomPosition + SideDirection(side) * sideOffset;
            baseCenter.y = centerY;
            var tangent = horizontal ? Vector3.right : Vector3.forward;
            var segmentScale = horizontal
                ? new Vector3(segmentLength, height, thickness)
                : new Vector3(thickness, height, segmentLength);
            CreateWall(roomName + "_" + side + "_A", baseCenter - tangent * tangentOffset, segmentScale, material, parent);
            CreateWall(roomName + "_" + side + "_B", baseCenter + tangent * tangentOffset, segmentScale, material, parent);
        }

        private static void CreateWall(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_" + name;
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static FuseStationPrototype CreateFuseStation(Transform parent)
        {
            var powerRoom = GameObject.Find("Room_Power");
            if (powerRoom == null)
            {
                throw new InvalidOperationException("Room_Power was not found.");
            }

            var stationMaterial = EnsureMaterial("M_FuseStation", new Color(0.95f, 0.4f, 0.05f), 0.2f, 0.4f);
            var missionConfig = AssetDatabase.LoadAssetAtPath<FuseMissionConfig>(FuseMissionConfigPath);
            if (missionConfig == null)
            {
                missionConfig = ScriptableObject.CreateInstance<FuseMissionConfig>();
                missionConfig.name = "SO_FuseMission_Default";
                AssetDatabase.CreateAsset(missionConfig, FuseMissionConfigPath);
            }

            var station = GameObject.CreatePrimitive(PrimitiveType.Cube);
            station.name = "MissionStation_Fuse";
            station.transform.SetParent(parent);
            station.transform.position = powerRoom.transform.position + new Vector3(2.5f, FloorTop + 0.6f, 2.5f);
            station.transform.localScale = new Vector3(1.2f, 1.2f, 0.7f);
            var renderer = station.GetComponent<Renderer>();
            renderer.sharedMaterial = stationMaterial;

            var indicatorObject = new GameObject("IndicatorLight");
            indicatorObject.transform.SetParent(station.transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var indicator = indicatorObject.AddComponent<Light>();
            indicator.type = LightType.Point;
            indicator.range = 4f;
            indicator.intensity = 2f;
            indicator.color = new Color(1f, 0.15f, 0.05f);
            var fuseStation = station.AddComponent<FuseStationPrototype>();
            fuseStation.Configure(renderer, indicator, missionConfig);
            return fuseStation;
        }

        private static void CreateFuseMissionView(Transform parent, FuseStationPrototype station)
        {
            var viewObject = new GameObject("[UI] FuseMission");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<FuseMissionView>().Configure(station);
        }

        private static NoiseService CreateNoiseService(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<NoiseBalanceConfig>(NoiseBalanceConfigPath);
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
            var config = AssetDatabase.LoadAssetAtPath<RoundBalanceConfig>(RoundBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
                config.name = "SO_RoundBalance_Default";
                AssetDatabase.CreateAsset(config, RoundBalanceConfigPath);
            }

            var roundObject = new GameObject("[Gameplay] LocalRoundPhase");
            roundObject.transform.SetParent(parent);
            var roundPhase = roundObject.AddComponent<LocalRoundPhasePrototype>();
            roundPhase.Configure(config);
            return roundPhase;
        }

        private static MonsterTierRuntime CreateMonsterTierRuntime(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterTierConfig>(MonsterTierConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterTierConfig>();
                config.name = "SO_MonsterTier_Default";
                AssetDatabase.CreateAsset(config, MonsterTierConfigPath);
            }
            EditorUtility.SetDirty(config);

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

        private static void CreateMonsterBiteAlertView(
            Transform parent,
            MonsterTarget target)
        {
            var viewObject = new GameObject("[UI] MonsterBiteAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<MonsterBiteAlertView>().Configure(target);
        }

        private static void CreateNoiseAlertView(Transform parent, NoiseService noiseService)
        {
            var viewObject = new GameObject("[UI] NoiseAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<NoiseAlertView>().Configure(noiseService);
        }

        private static void BuildNavigation(Transform parent)
        {
            var navigationObject = new GameObject("[Navigation] Laboratory");
            navigationObject.transform.SetParent(parent);
            var surface = navigationObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = Physics.AllLayers;
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                throw new InvalidOperationException("Laboratory NavMesh could not be built.");
            }

            var builtData = surface.navMeshData;
            var savedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(LaboratoryNavMeshPath);
            if (savedData == null)
            {
                builtData.name = "NavMesh_Laboratory";
                AssetDatabase.CreateAsset(builtData, LaboratoryNavMeshPath);
            }
            else
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(builtData, savedData);
                savedData.name = "NavMesh_Laboratory";
                surface.navMeshData = savedData;
                UnityEngine.Object.DestroyImmediate(builtData);
                surface.AddData();
                EditorUtility.SetDirty(savedData);
            }

            EditorUtility.SetDirty(surface);
        }

        private static void CreateMonster(
            Transform parent,
            NoiseService noiseService,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterBalanceConfig>(MonsterBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterBalanceConfig>();
                config.name = "SO_MonsterBalance_Default";
                AssetDatabase.CreateAsset(config, MonsterBalanceConfigPath);
            }
            EditorUtility.SetDirty(config);

            var patrolPoints = CreateMonsterPatrolPoints(parent);
            var monster = new GameObject("P_Monster_01");
            monster.transform.SetParent(parent);
            monster.transform.position = patrolPoints[0].position;

            var agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.38f;
            agent.height = 1.5f;
            agent.baseOffset = 0f;
            agent.stoppingDistance = agent.radius;
            agent.speed = config.PatrolSpeed;
            agent.autoBraking = true;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(monster.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = EnsureMaterial(
                "M_MonsterPrototype",
                new Color(0.68f, 0.10f, 0.12f),
                0.05f,
                0.18f);

            var indicatorObject = new GameObject("StateIndicator");
            indicatorObject.transform.SetParent(monster.transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var indicator = indicatorObject.AddComponent<Light>();
            indicator.type = LightType.Point;
            indicator.range = 4f;
            indicator.intensity = 2f;
            indicator.color = new Color(0.68f, 0.10f, 0.12f);

            var senses = monster.AddComponent<MonsterSenses>();
            senses.Configure(config, monsterTierRuntime, target, Physics.DefaultRaycastLayers);
            var biteController = monster.AddComponent<MonsterBiteController>();
            biteController.Configure(config, senses, target);
            var brain = monster.AddComponent<MonsterBrain>();
            brain.Configure(
                agent,
                noiseService,
                config,
                roundPhase,
                senses,
                biteController,
                patrolPoints);
            monster.AddComponent<MonsterPrototypePresenter>().Configure(brain, renderer, indicator);
        }

        private static Transform[] CreateMonsterPatrolPoints(Transform parent)
        {
            var patrolRoot = new GameObject("[AI] MonsterPatrolPoints");
            patrolRoot.transform.SetParent(parent);
            var definitions = new[]
            {
                (Room: "QuarantineA", Offset: new Vector3(0f, 0f, -3f)),
                (Room: "Power", Offset: new Vector3(-2.5f, 0f, 3f)),
                (Room: "Power", Offset: new Vector3(-2.5f, 0f, -3f)),
                (Room: "QuarantineB", Offset: new Vector3(0f, 0f, 3f))
            };
            var points = new Transform[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var room = GameObject.Find("Room_" + definitions[index].Room);
                if (room == null)
                {
                    throw new InvalidOperationException("Missing monster patrol room: " + definitions[index].Room);
                }

                var targetPosition = room.transform.position + definitions[index].Offset;
                var sampleDistance = Mathf.Max(room.transform.localScale.x, room.transform.localScale.z);
                if (!NavMesh.SamplePosition(targetPosition, out var hit, sampleDistance, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(
                        $"No NavMesh point was found for monster patrol point {index + 1}.");
                }

                var point = new GameObject($"MonsterPatrol_{index + 1:00}");
                point.transform.SetParent(patrolRoot.transform);
                point.transform.position = hit.position;
                points[index] = point.transform;
            }

            return points;
        }

        private static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static WallSide GetSide(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
            {
                return delta.x >= 0f ? WallSide.East : WallSide.West;
            }

            return delta.z >= 0f ? WallSide.North : WallSide.South;
        }

        private static Vector3 SideDirection(WallSide side)
        {
            return side switch
            {
                WallSide.North => Vector3.forward,
                WallSide.South => Vector3.back,
                WallSide.East => Vector3.right,
                WallSide.West => Vector3.left,
                _ => Vector3.zero
            };
        }

        private static void RequireComponent<T>(GameObject source, List<string> failures) where T : Component
        {
            if (source == null || source.GetComponent<T>() == null)
            {
                failures.Add("P_Player_Local is missing " + typeof(T).Name + ".");
            }
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
