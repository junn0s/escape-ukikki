using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    public static class FirstPlayableBuilder
    {
        private const string LaboratoryScenePath = "Assets/_Project/Scenes/10_Laboratory.unity";
        private const string InputActionsPath = "Assets/_Project/Settings/PlayerControls.inputactions";
        private const string MovementConfigPath = "Assets/_Project/Data/Balance/SO_PlayerMovement_Default.asset";
        private const string MaterialRoot = "Assets/_Project/Art/Materials";
        private const float FloorTop = 0.15f;

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
            var player = CreatePlayer(prototypeRoot.transform, spawnPosition);
            CreateRoomWalls(prototypeRoot.transform);
            CreateFuseStation(prototypeRoot.transform);
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
            if (station == null || station.GetComponent<FuseStationPrototype>() == null)
            {
                failures.Add("Fuse mission station is missing.");
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
                inputActions.FindAction("Gameplay/Flashlight") == null)
            {
                failures.Add("Required player input actions are missing.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }

            Debug.Log("[MonkeyLab] First playable validation passed.");
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

        private static void CreateFuseStation(Transform parent)
        {
            var securityRoom = GameObject.Find("Room_Security");
            var stationMaterial = EnsureMaterial("M_FuseStation", new Color(0.95f, 0.4f, 0.05f), 0.2f, 0.4f);
            var station = GameObject.CreatePrimitive(PrimitiveType.Cube);
            station.name = "MissionStation_Fuse";
            station.transform.SetParent(parent);
            station.transform.position = securityRoom.transform.position + new Vector3(2.5f, FloorTop + 0.6f, 2.5f);
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
            station.AddComponent<FuseStationPrototype>().Configure(renderer, indicator);
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
