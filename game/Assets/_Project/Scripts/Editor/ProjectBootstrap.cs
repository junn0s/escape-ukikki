using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonkeyLab.Core;
using MonkeyLab.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    public static class ProjectBootstrap
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string SceneRoot = ProjectRoot + "/Scenes";
        private const string SettingsRoot = ProjectRoot + "/Settings";
        private const string MaterialRoot = ProjectRoot + "/Art/Materials";
        private const string PipelinePath = SettingsRoot + "/URP_Pipeline.asset";
        private const string RendererPath = SettingsRoot + "/URP_Renderer.asset";

        internal static readonly Vector3[] LaboratoryPlayerSpawnPositions =
        {
            new(-14f, 0f, 5.5f),
            new(0f, 0f, 6f),
            new(7f, 0f, 0f),
            new(-14f, 0f, -5.5f),
            new(0f, 0f, -6f),
            new(-7f, 0f, 0f)
        };

        private static readonly string[] ProjectFolders =
        {
            ProjectRoot + "/Art/Characters",
            ProjectRoot + "/Art/Monsters",
            ProjectRoot + "/Art/Environment",
            ProjectRoot + "/Art/Props",
            ProjectRoot + "/Art/Materials",
            ProjectRoot + "/Art/Textures",
            ProjectRoot + "/Art/Animations",
            ProjectRoot + "/Art/VFX",
            ProjectRoot + "/Audio/Music",
            ProjectRoot + "/Audio/Ambience",
            ProjectRoot + "/Audio/SFX",
            ProjectRoot + "/Audio/Mixers",
            ProjectRoot + "/Data/Balance",
            ProjectRoot + "/Data/Missions",
            ProjectRoot + "/Data/Maps",
            ProjectRoot + "/Data/Catalogs",
            ProjectRoot + "/Prefabs/Core",
            ProjectRoot + "/Prefabs/Players",
            ProjectRoot + "/Prefabs/Monsters",
            ProjectRoot + "/Prefabs/Environment",
            ProjectRoot + "/Prefabs/Missions",
            ProjectRoot + "/Prefabs/Interactables",
            ProjectRoot + "/Prefabs/Network",
            ProjectRoot + "/Prefabs/UI",
            SceneRoot,
            ProjectRoot + "/Scripts/Gameplay/Domain",
            ProjectRoot + "/Scripts/Core/Utilities",
            ProjectRoot + "/Scripts/Gameplay/Application",
            ProjectRoot + "/Scripts/Gameplay/Player",
            ProjectRoot + "/Scripts/Gameplay/Monsters",
            ProjectRoot + "/Scripts/Gameplay/Missions",
            ProjectRoot + "/Scripts/Gameplay/Infection",
            ProjectRoot + "/Scripts/Gameplay/Villain",
            ProjectRoot + "/Scripts/Gameplay/Meeting",
            ProjectRoot + "/Scripts/Presentation/UI",
            ProjectRoot + "/Scripts/Presentation/Camera",
            ProjectRoot + "/Scripts/Presentation/Audio",
            ProjectRoot + "/Scripts/Presentation/VFX",
            SettingsRoot,
            ProjectRoot + "/Shaders",
            ProjectRoot + "/UI/Fonts",
            ProjectRoot + "/UI/Icons",
            ProjectRoot + "/UI/Sprites",
            ProjectRoot + "/UI/Themes",
            ProjectRoot + "/Tests/PlayMode",
            "Assets/ThirdParty",
            "Assets/Plugins"
        };

        [MenuItem("Tools/Monkey Lab/Create Project Foundation")]
        public static void Run()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            EnsureRenderPipeline();
            CreateScenes();
            ConfigureBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MonkeyLab] Project foundation is ready.");
        }

        public static void Validate()
        {
            var failures = new List<string>();
            if (!Application.unityVersion.StartsWith("6000.3.", StringComparison.Ordinal))
            {
                failures.Add("Unity must be version 6000.3.x.");
            }

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                failures.Add("The default render pipeline is not configured.");
            }

            var expectedScenes = new[]
            {
                "00_Bootstrap.unity",
                "01_MainMenu.unity",
                "02_Lobby.unity",
                "10_Laboratory.unity"
            };
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileName(scene.path))
                .ToArray();

            if (!expectedScenes.SequenceEqual(enabledScenes))
            {
                failures.Add("Enabled build scenes do not match the project scene order.");
            }

            foreach (var sceneName in expectedScenes)
            {
                if (!File.Exists(Path.Combine(SceneRoot, sceneName)))
                {
                    failures.Add("Missing scene: " + sceneName);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }

            Debug.Log("[MonkeyLab] Foundation validation passed.");
        }

        private static void EnsureProjectFolders()
        {
            foreach (var folder in ProjectFolders)
            {
                Directory.CreateDirectory(folder);
                var keepFile = Path.Combine(folder, ".gitkeep");
                if (!File.Exists(keepFile))
                {
                    File.WriteAllText(keepFile, string.Empty);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureProjectSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.externalVersionControl = "Visible Meta Files";

            PlayerSettings.companyName = "Ukikki Team";
            PlayerSettings.productName = "Escape Ukikki";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            QualitySettings.vSyncCount = 1;
        }

        private static void EnsureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create();
                pipeline.name = "URP_Pipeline";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);

                var renderer = pipeline.LoadBuiltinRendererData();
                renderer.name = "URP_Renderer";
                var moveError = AssetDatabase.MoveAsset("Assets/UniversalRenderer.asset", RendererPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException(moveError);
                }

                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(pipeline);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void CreateScenes()
        {
            var floorMaterial = EnsureMaterial("M_BlockoutFloor", new Color(0.08f, 0.12f, 0.14f), 0.15f, 0.25f);
            var roomMaterial = EnsureMaterial("M_BlockoutRoom", new Color(0.16f, 0.27f, 0.30f), 0.1f, 0.3f);
            var accentMaterial = EnsureMaterial("M_BlockoutAccent", new Color(0.95f, 0.55f, 0.08f), 0.05f, 0.35f);
            var characterMaterial = EnsureMaterial("M_BlockoutCharacter", new Color(0.20f, 0.70f, 0.90f), 0.0f, 0.4f);
            var dangerMaterial = EnsureMaterial("M_BlockoutDanger", new Color(0.70f, 0.08f, 0.08f), 0.0f, 0.25f);

            CreateBootstrapScene();
            CreateMainMenuScene(floorMaterial, accentMaterial);
            CreateLobbyScene(floorMaterial, characterMaterial);
            CreateLaboratoryScene(floorMaterial, roomMaterial, characterMaterial, dangerMaterial);
            CreateArtSandboxScene(floorMaterial, accentMaterial, characterMaterial, dangerMaterial);
            CreateGameplaySandboxScene(floorMaterial, characterMaterial, dangerMaterial);
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
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader was not found.");
            }

            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateBootstrapScene()
        {
            var path = SceneRoot + "/00_Bootstrap.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("[Core] AppBootstrap");
            bootstrap.AddComponent<BootstrapEntryPoint>();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateMainMenuScene(Material floorMaterial, Material accentMaterial)
        {
            var path = SceneRoot + "/01_MainMenu.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = CreatePresentationScene(
                "MAIN MENU",
                "Session creation and join-code UI will be implemented here.",
                new Vector3(0f, 11f, -14f),
                new Vector3(35f, 0f, 0f));

            CreatePrimitive("MenuStage", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(12f, 1f, 8f), floorMaterial);
            CreatePrimitive("MonkeyMascotPlaceholder", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f), accentMaterial);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateLobbyScene(Material floorMaterial, Material characterMaterial)
        {
            var path = SceneRoot + "/02_Lobby.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = CreatePresentationScene(
                "LOBBY",
                "Six player slots, ready state, color and nickname selection placeholder.",
                new Vector3(0f, 13f, -16f),
                new Vector3(38f, 0f, 0f));

            CreatePrimitive("LobbyFloor", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(18f, 1f, 12f), floorMaterial);
            for (var index = 0; index < 6; index++)
            {
                var angle = index * Mathf.PI * 2f / 6f;
                var position = new Vector3(Mathf.Cos(angle) * 4f, 1f, Mathf.Sin(angle) * 3f);
                CreatePrimitive($"PlayerSlot_{index + 1:00}", PrimitiveType.Capsule, position, Vector3.one, characterMaterial);
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateLaboratoryScene(
            Material floorMaterial,
            Material roomMaterial,
            Material characterMaterial,
            Material dangerMaterial)
        {
            var path = SceneRoot + "/10_Laboratory.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = CreatePresentationScene(
                "LABORATORY BLOCKOUT",
                "Ten-room north/south loop graybox based on docs/map-level-design.md.",
                new Vector3(0f, 42f, -35f),
                new Vector3(50f, 0f, 0f));

            var rooms = new Dictionary<string, RoomBlock>
            {
                ["VaccineA"] = new RoomBlock(new Vector3(-14f, 0f, 11f), 8f, 10f),
                ["LabA"] = new RoomBlock(new Vector3(0f, 0f, 12f), 10f, 12f),
                ["QuarantineA"] = new RoomBlock(new Vector3(14f, 0f, 11f), 8f, 10f),
                ["Storage"] = new RoomBlock(new Vector3(-14f, 0f, 0f), 8f, 10f),
                ["Security"] = new RoomBlock(new Vector3(0f, 0f, 0f), 10f, 12f),
                ["Power"] = new RoomBlock(new Vector3(14f, 0f, 0f), 8f, 10f),
                ["Ward"] = new RoomBlock(new Vector3(-14f, 0f, -11f), 8f, 10f),
                ["LabB"] = new RoomBlock(new Vector3(0f, 0f, -12f), 10f, 12f),
                ["QuarantineB"] = new RoomBlock(new Vector3(14f, 0f, -11f), 8f, 10f),
                ["VaccineB"] = new RoomBlock(new Vector3(24f, 0f, -13f), 8f, 10f)
            };

            var links = new[]
            {
                ("VaccineA", "LabA"), ("LabA", "QuarantineA"), ("VaccineA", "Storage"),
                ("LabA", "Security"), ("QuarantineA", "Power"), ("Storage", "Security"),
                ("Security", "Power"), ("Storage", "Ward"), ("Ward", "LabB"),
                ("Security", "LabB"), ("Power", "QuarantineB"), ("LabB", "QuarantineB"),
                ("LabB", "VaccineB"), ("QuarantineB", "VaccineB")
            };

            var mapRoot = new GameObject("[Map] LaboratoryBlockout").transform;
            foreach (var link in links)
            {
                CreateCorridor(link.Item1 + "_to_" + link.Item2, rooms[link.Item1].Position, rooms[link.Item2].Position, floorMaterial, mapRoot);
            }

            foreach (var room in rooms)
            {
                var roomObject = CreatePrimitive(
                    "Room_" + room.Key,
                    PrimitiveType.Cube,
                    room.Value.Position,
                    new Vector3(room.Value.Width, 0.3f, room.Value.Depth),
                    roomMaterial);
                roomObject.transform.SetParent(mapRoot);
            }

            for (var index = 0; index < LaboratoryPlayerSpawnPositions.Length; index++)
            {
                var spawnPosition = LaboratoryPlayerSpawnPositions[index];
                var position = new Vector3(spawnPosition.x, 1.1f, spawnPosition.z);
                var player = CreatePrimitive($"PlayerSpawn_{index + 1:00}", PrimitiveType.Capsule, position, Vector3.one, characterMaterial);
                player.transform.SetParent(mapRoot);
            }

            var monsterSpawns = new[]
            {
                new Vector3(-20f, 0.8f, 4f), new Vector3(20f, 0.8f, 4f),
                new Vector3(-20f, 0.8f, -5f), new Vector3(20f, 0.8f, -5f)
            };
            for (var index = 0; index < monsterSpawns.Length; index++)
            {
                var monster = CreatePrimitive($"MonsterSpawn_{index + 1:00}", PrimitiveType.Sphere, monsterSpawns[index], Vector3.one * 1.3f, dangerMaterial);
                monster.transform.SetParent(mapRoot);
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateArtSandboxScene(Material floor, Material accent, Material character, Material danger)
        {
            var path = SceneRoot + "/90_ArtSandbox.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = CreatePresentationScene(
                "ART SANDBOX",
                "Lighting, materials, characters, monsters and VFX are reviewed here.",
                new Vector3(0f, 12f, -16f),
                new Vector3(38f, 0f, 0f));

            CreatePrimitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 12f), floor);
            CreatePrimitive("Material_Cube", PrimitiveType.Cube, new Vector3(-4f, 1f, 0f), Vector3.one * 2f, accent);
            CreatePrimitive("Player_Capsule", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), Vector3.one, character);
            CreatePrimitive("Monster_Sphere", PrimitiveType.Sphere, new Vector3(4f, 1f, 0f), Vector3.one * 2f, danger);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateGameplaySandboxScene(Material floor, Material character, Material danger)
        {
            var path = SceneRoot + "/91_GameplaySandbox.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = CreatePresentationScene(
                "GAMEPLAY SANDBOX",
                "Movement, interaction, missions, infection and monster AI are tested here.",
                new Vector3(0f, 18f, -20f),
                new Vector3(42f, 0f, 0f));

            CreatePrimitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(24f, 1f, 18f), floor);
            CreatePrimitive("PlayerPrototype", PrimitiveType.Capsule, new Vector3(-3f, 1f, 0f), Vector3.one, character);
            CreatePrimitive("MonsterPrototype", PrimitiveType.Capsule, new Vector3(3f, 1f, 0f), Vector3.one * 1.3f, danger);
            new GameObject("[Systems] GameplayPrototypes");
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Scene CreatePresentationScene(string title, string description, Vector3 cameraPosition, Vector3 cameraEuler)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.035f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(0.78f, 0.88f, 1f);

            var viewObject = new GameObject("[UI] SceneInfo");
            var view = viewObject.AddComponent<ScenePlaceholderView>();
            view.Initialize(title, description);
            EditorUtility.SetDirty(view);
            return scene;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance;
        }

        private static void CreateCorridor(string name, Vector3 start, Vector3 end, Material material, Transform parent)
        {
            var direction = end - start;
            var corridor = CreatePrimitive(
                "Corridor_" + name,
                PrimitiveType.Cube,
                (start + end) * 0.5f + Vector3.down * 0.08f,
                new Vector3(2.7f, 0.15f, direction.magnitude),
                material);
            corridor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            corridor.transform.SetParent(parent);
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneRoot + "/00_Bootstrap.unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/01_MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/02_Lobby.unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/10_Laboratory.unity", true)
            };
        }

        private readonly struct RoomBlock
        {
            public RoomBlock(Vector3 position, float width, float depth)
            {
                Position = position;
                Width = width;
                Depth = depth;
            }

            public Vector3 Position { get; }
            public float Width { get; }
            public float Depth { get; }
        }
    }
}
