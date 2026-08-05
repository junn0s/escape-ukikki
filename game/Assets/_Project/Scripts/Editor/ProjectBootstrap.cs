using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Network;
using MonkeyLab.Presentation;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.Player;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private const string Renderer2DPath =
            SettingsRoot + "/URP_Renderer2D.asset";
        private const string NetworkPlayerPrefabPath =
            ProjectRoot + "/Prefabs/Players/P_Player_Network.prefab";

        /// <summary>
        /// 연구소 10개 방을 모두 감싸는 경계다. 유령이 맵 밖으로 나가지 못하게 한다.
        /// FirstPlayableBuilder의 방 좌표와 크기에서 여유를 두고 계산한 값이다.
        /// 로컬 씬과 네트워크 프리팹이 같은 값을 써야 하므로 한 곳에만 둔다.
        /// </summary>
        internal static readonly Rect LaboratoryMapBounds =
            new(-52f, -40f, 119f, 76f);
        private const string InputActionsPath =
            SettingsRoot + "/PlayerControls.inputactions";
        private const string MovementConfigPath =
            ProjectRoot + "/Data/Balance/SO_PlayerMovement_Default.asset";
        private const string MonsterBalanceConfigPath =
            ProjectRoot + "/Data/Balance/SO_MonsterBalance_Default.asset";
        private const string AntidoteBalanceConfigPath =
            ProjectRoot + "/Data/Balance/SO_AntidoteBalance_Default.asset";
        private const string InteractionBalanceConfigPath =
            ProjectRoot + "/Data/Balance/SO_InteractionBalance_Default.asset";

        internal static readonly Vector3[] LaboratoryPlayerSpawnPositions =
        {
            new(-25f, -7f, 0f),
            new(-10f, 24f, 0f),
            new(13f, -7f, 0f),
            new(-7f, -29f, 0f),
            new(13f, -29f, 0f),
            new(-7f, -7f, 0f)
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
            ProjectRoot + "/Scripts/Gameplay/Interaction",
            ProjectRoot + "/Scripts/Gameplay/Villain",
            ProjectRoot + "/Scripts/Gameplay/Meeting",
            ProjectRoot + "/Scripts/Presentation/UI",
            ProjectRoot + "/Scripts/Presentation/Camera",
            ProjectRoot + "/Scripts/Presentation/Player",
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

        [InitializeOnLoadMethod]
        private static void ScheduleRenderer2DRepair()
        {
            EditorApplication.delayCall -= RepairRenderer2DConfiguration;
            EditorApplication.delayCall += RepairRenderer2DConfiguration;
        }

        private static void RepairRenderer2DConfiguration()
        {
            var pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    PipelinePath);
            if (pipeline == null || UsesRenderer2D(pipeline))
            {
                return;
            }

            EnsureRenderer2DData(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[MonkeyLab] URP default renderer repaired to Renderer2D. " +
                "Restart Play Mode to apply 2D lights.");
        }

        [MenuItem("Tools/Monkey Lab/Create Project Foundation")]
        public static void Run()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            EnsureRenderPipeline();
            CreateScenes();
            FirstPlayableBuilder.Build();
            ConfigureMainMenuNetwork();
            ConfigureLaboratoryNetworkFlow();
            ConfigureBootstrapServices();
            ConfigureBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MonkeyLab] Project foundation is ready.");
        }

        [MenuItem("Tools/Monkey Lab/Build Network Player Flow")]
        public static void BuildNetworkPlayerFlow()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before building the network player flow.");
            }

            EnsureProjectFolders();
            ConfigureMainMenuNetwork();
            ConfigureLaboratoryNetworkFlow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MonkeyLab] Network player flow is ready.");
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
            VersionControlSettings.mode = "Visible Meta Files";

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
            var renderer = EnsureRenderer2DAsset();
            var pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "URP_Pipeline";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            else
            {
                AssignRenderer2D(pipeline, renderer);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static Renderer2DData EnsureRenderer2DData(
            UniversalRenderPipelineAsset pipeline)
        {
            var renderer = EnsureRenderer2DAsset();
            AssignRenderer2D(pipeline, renderer);
            return renderer;
        }

        private static Renderer2DData EnsureRenderer2DAsset()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(
                Renderer2DPath);
            if (renderer != null)
            {
                return renderer;
            }

            renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            renderer.name = "URP_Renderer2D";
            AssetDatabase.CreateAsset(renderer, Renderer2DPath);
            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        private static bool UsesRenderer2D(
            UniversalRenderPipelineAsset pipeline)
        {
            var serializedPipeline = new SerializedObject(pipeline);
            var rendererList = serializedPipeline.FindProperty(
                "m_RendererDataList");
            return rendererList != null && rendererList.arraySize > 0 &&
                   rendererList.GetArrayElementAtIndex(0)
                       .objectReferenceValue is Renderer2DData;
        }

        private static void AssignRenderer2D(
            UniversalRenderPipelineAsset pipeline,
            Renderer2DData renderer)
        {
            var serializedPipeline = new SerializedObject(pipeline);
            var rendererList = serializedPipeline.FindProperty(
                "m_RendererDataList");
            if (rendererList == null)
            {
                throw new InvalidOperationException(
                    "URP renderer data list is unavailable.");
            }

            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue =
                renderer;
            var defaultRendererIndex = serializedPipeline.FindProperty(
                "m_DefaultRendererIndex");
            if (defaultRendererIndex != null)
            {
                defaultRendererIndex.intValue = 0;
            }

            var legacyRenderer = serializedPipeline.FindProperty(
                "m_RendererData");
            if (legacyRenderer != null)
            {
                legacyRenderer.objectReferenceValue = renderer;
            }

            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
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
            CreateLaboratoryScene(floorMaterial, roomMaterial, characterMaterial);
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

        private static void ConfigureBootstrapServices()
        {
            var path = SceneRoot + "/00_Bootstrap.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var cameraObject = GameObject.Find("Main Camera") ??
                               new GameObject("Main Camera");
            var bootstrapCamera = cameraObject.GetComponent<Camera>();
            if (bootstrapCamera == null)
            {
                bootstrapCamera = cameraObject.AddComponent<Camera>();
            }

            cameraObject.tag = "MainCamera";
            bootstrapCamera.clearFlags = CameraClearFlags.SolidColor;
            bootstrapCamera.backgroundColor = Color.black;

            var bootstrap = GameObject.Find("[Core] AppBootstrap") ??
                            new GameObject("[Core] AppBootstrap");
            var entryPoint = bootstrap.GetComponent<BootstrapEntryPoint>() ??
                             bootstrap.AddComponent<BootstrapEntryPoint>();
            var servicesInitializer = bootstrap.GetComponent<UnityServicesInitializer>() ??
                                      bootstrap.AddComponent<UnityServicesInitializer>();

            var statusObject = GameObject.Find("[UI] BootstrapStatus") ??
                               new GameObject("[UI] BootstrapStatus");
            statusObject.transform.SetParent(bootstrap.transform);
            var statusView = statusObject.GetComponent<BootstrapStatusView>() ??
                             statusObject.AddComponent<BootstrapStatusView>();

            entryPoint.ConfigureStartupTasks(servicesInitializer);
            statusView.Configure(entryPoint);
            EditorUtility.SetDirty(bootstrapCamera);
            EditorUtility.SetDirty(entryPoint);
            EditorUtility.SetDirty(statusView);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ConfigureMainMenuNetwork()
        {
            var playerPrefab = EnsureNetworkPlayerPrefab();
            var path = SceneRoot + "/01_MainMenu.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sessionObject = GameObject.Find("[Network] GameSession") ??
                                new GameObject("[Network] GameSession");

            var transport = sessionObject.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = sessionObject.AddComponent<UnityTransport>();
            }

            var networkManager = sessionObject.GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                networkManager = sessionObject.AddComponent<NetworkManager>();
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ConnectionApproval = true;

            var controller = sessionObject.GetComponent<GameSessionController>();
            if (controller == null)
            {
                controller = sessionObject.AddComponent<GameSessionController>();
            }

            controller.Configure(networkManager, transport);

            var lobbyObject = GameObject.Find("[Network] LobbyRoster") ??
                              new GameObject("[Network] LobbyRoster");
            if (lobbyObject.GetComponent<NetworkObject>() == null)
            {
                lobbyObject.AddComponent<NetworkObject>();
            }

            var lobbyRoster = lobbyObject.GetComponent<LobbyRosterNetwork>();
            if (lobbyRoster == null)
            {
                lobbyRoster = lobbyObject.AddComponent<LobbyRosterNetwork>();
            }

            var viewObject = GameObject.Find("[UI] MainMenuSession") ??
                             new GameObject("[UI] MainMenuSession");
            var sessionView = viewObject.GetComponent<MainMenuSessionView>();
            if (sessionView == null)
            {
                sessionView = viewObject.AddComponent<MainMenuSessionView>();
            }

            // 배경 이야기는 메인 메뉴 첫 진입에 1회 재생한다(ui-ux-design.md §2.1).
            var introStory = viewObject.GetComponent<IntroStoryView>();
            if (introStory == null)
            {
                introStory = viewObject.AddComponent<IntroStoryView>();
            }

            sessionView.Configure(controller, lobbyRoster, introStory);
            EditorUtility.SetDirty(introStory);
            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(lobbyObject);
            EditorUtility.SetDirty(lobbyRoster);
            EditorUtility.SetDirty(sessionView);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ConfigureLaboratoryNetworkFlow()
        {
            var path = SceneRoot + "/10_Laboratory.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var localPrototypeRoot = GameObject.Find("[Prototype] FirstPlayable");
            if (localPrototypeRoot == null)
            {
                throw new InvalidOperationException(
                    "Build the first playable before configuring network gameplay.");
            }

            var adapterObject = GameObject.Find("[Network] GameplayScene") ??
                                new GameObject("[Network] GameplayScene");
            var adapter =
                adapterObject.GetComponent<NetworkGameplaySceneAdapter>() ??
                adapterObject.AddComponent<NetworkGameplaySceneAdapter>();
            var localPlayer = GameObject.Find("P_Player_Local");
            var monsterTierRuntime =
                GameObject.Find("[Gameplay] MonsterTierRuntime")?
                    .GetComponent<MonsterTierRuntime>();
            var infectionHud = GameObject.Find("[UI] InfectionHud")?
                .GetComponent<InfectionHudView>();
            var monsterBiteAlert =
                GameObject.Find("[UI] MonsterBiteAlert")?
                    .GetComponent<MonsterBiteAlertView>();
            var interactionPrompt =
                GameObject.Find("[UI] InteractionPrompt")?
                    .GetComponent<InteractionPromptView>();
            var gameplayFeel = GameObject.Find("[UI] GameplayFeel")?
                .GetComponent<GameplayFeelView>();
            if (localPlayer == null || monsterTierRuntime == null ||
                infectionHud == null || monsterBiteAlert == null ||
                interactionPrompt == null || gameplayFeel == null)
            {
                throw new InvalidOperationException(
                    "The local network gameplay references are incomplete.");
            }

            var missionJournal = GameObject.Find("[UI] MissionJournal")?
                .GetComponent<MissionJournalView>();
            adapter.Configure(
                localPrototypeRoot,
                localPlayer,
                monsterTierRuntime,
                infectionHud,
                monsterBiteAlert,
                interactionPrompt,
                missionJournal,
                gameplayFeel);

            EditorUtility.SetDirty(adapter);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static GameObject EnsureNetworkPlayerPrefab()
        {
            FirstPlayableBuilder.EnsureTopDownArtAssets();
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            var movementConfig = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                MovementConfigPath);
            var monsterConfig =
                AssetDatabase.LoadAssetAtPath<MonsterBalanceConfig>(
                    MonsterBalanceConfigPath);
            var antidoteConfig =
                AssetDatabase.LoadAssetAtPath<AntidoteBalanceConfig>(
                    AntidoteBalanceConfigPath);
            var interactionConfig =
                AssetDatabase.LoadAssetAtPath<InteractionBalanceConfig>(
                    InteractionBalanceConfigPath);
            if (interactionConfig == null)
            {
                interactionConfig =
                    ScriptableObject.CreateInstance<
                        InteractionBalanceConfig>();
                interactionConfig.name =
                    "SO_InteractionBalance_Default";
                AssetDatabase.CreateAsset(
                    interactionConfig,
                    InteractionBalanceConfigPath);
            }
            if (inputActions == null || movementConfig == null ||
                monsterConfig == null ||
                antidoteConfig == null || interactionConfig == null)
            {
                throw new InvalidOperationException(
                    "Network player source assets are missing.");
            }

            var root = new GameObject("P_Player_Network");
            try
            {
                var networkObject = root.AddComponent<NetworkObject>();
                networkObject.ActiveSceneSynchronization = true;

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode =
                    NetworkTransform.AuthorityModes.Owner;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleY = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncPositionZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                networkTransform.UseUnreliableDeltas = true;

                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.linearDamping = 8f;
                body.angularDamping = 8f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode =
                    CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var collider = root.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(1.05f, 1.45f);

                var input = root.AddComponent<PlayerInputReader>();
                input.Configure(inputActions);
                var motor = root.AddComponent<PlayerMotor>();
                motor.Configure(input, body, movementConfig);
                var aim = root.AddComponent<PlayerAimController>();
                aim.Configure(input, null, movementConfig);
                var interactor = root.AddComponent<PlayerInteractor>();
                interactor.Configure(
                    input,
                    interactionConfig.GeneralInteractionRangeMeters);
                var monsterTarget = root.AddComponent<MonsterTarget>();
                monsterTarget.Configure(
                    isDetectable: true,
                    canBeInfected: true);
                var infectionService = root.AddComponent<InfectionService>();
                infectionService.Configure(monsterTarget, null);
                var antidoteService = root.AddComponent<AntidoteService>();
                antidoteService.Configure(
                    antidoteConfig,
                    infectionService,
                    input,
                    motor);

                var avatar = root.AddComponent<NetworkPlayerAvatar>();
                avatar.Configure(
                    networkTransform,
                    monsterTarget,
                    monsterConfig);
                var infectionAuthority =
                    root.AddComponent<NetworkInfectionAuthority>();
                infectionAuthority.Configure(infectionService);

                // 해독제 소지와 레시피 발견은 서버 권위이며 소유자에게만 복제한다.
                var antidoteInventory =
                    root.AddComponent<NetworkAntidoteInventoryAuthority>();
                antidoteInventory.Configure(
                    antidoteService,
                    infectionAuthority,
                    antidoteConfig);

                // 유령은 벽을 통과하지만 맵 밖으로 나갈 수 없다(GDD §17).
                var ghostMovement =
                    root.AddComponent<GhostMovementController>();
                ghostMovement.Configure(
                    infectionService,
                    body,
                    collider,
                    movementConfig,
                    LaboratoryMapBounds);
                motor.SetGhostMovement(ghostMovement);
                var missionJournal =
                    root.AddComponent<NetworkPlayerMissionJournal>();

                var visualRoot = FirstPlayableBuilder.CreatePlayerVisuals(
                    root.transform,
                    Color.white,
                    input,
                    out var flashlightController);

                var bodyRenderers = new[]
                {
                    visualRoot.transform.Find("Body")
                        .GetComponent<Renderer>()
                };

                // 살아 있는 플레이어는 유령을 볼 수 없다(GDD §17).
                root.AddComponent<GhostVisibilityPresenter>()
                    .Configure(
                        infectionAuthority,
                        bodyRenderers,
                        visualRoot.GetComponentsInChildren<Light2D>(
                            includeInactive: true));

                var presentation =
                    root.AddComponent<NetworkPlayerPresentation>();
                var flashlightRenderer = visualRoot.transform
                    .Find("AimPivot/FlashlightCone")?
                    .GetComponent<Renderer>();
                presentation.Configure(
                    avatar,
                    visualRoot,
                    bodyRenderers,
                    new Behaviour[]
                    {
                        input,
                        motor,
                        aim,
                        interactor,
                        antidoteService,
                        flashlightController
                    },
                    body,
                    interactor,
                    missionJournal,
                    aim,
                    monsterTarget,
                    infectionService,
                    antidoteService,
                    visualRoot.GetComponentsInChildren<Light2D>(
                        includeInactive: true),
                    flashlightRenderer != null
                        ? new[] { flashlightRenderer }
                        : Array.Empty<Renderer>(),
                    flashlightController);

                input.enabled = false;
                motor.enabled = false;
                aim.enabled = false;
                interactor.enabled = false;
                antidoteService.enabled = false;
                flashlightController.enabled = false;

                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    NetworkPlayerPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Failed to save the network player prefab.");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
            Material characterMaterial)
        {
            var path = SceneRoot + "/10_Laboratory.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.016f, 0.026f);
            cameraObject.AddComponent<AudioListener>();
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

    }
}
