using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    public static class MultiplayerPlayModeConfigurator
    {
        private const string DefaultScenarioPath =
            "Assets/Settings/PlayMode/HostClient2.asset";
        private const string ScenarioFolder =
            "Assets/_Project/Settings/PlayMode";
        private const string ScenarioPath =
            ScenarioFolder + "/HostClient2.asset";
        private const string FourPlayerScenarioPath =
            ScenarioFolder + "/HostClient4.asset";
        private const string BootstrapScenePath =
            "Assets/_Project/Scenes/00_Bootstrap.unity";
        private const string VirtualProjectFolderPath = "Library/VP";
        private const string VirtualProjectSystemDataPath =
            VirtualProjectFolderPath + "/SystemData.json";
        private static double _hostClientStartTime;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= TryEnterConfiguredPlayMode;
            EditorApplication.delayCall += () =>
            {
                RepairVirtualProjectState();
                if (string.Equals(
                        AssetDatabase.GetAssetPath(Selection.activeObject),
                        ScenarioPath,
                        StringComparison.Ordinal))
                {
                    Selection.activeObject = null;
                }
            };
        }

        [MenuItem("Tools/Monkey Lab/Configure Host Client Play Mode")]
        public static void Configure()
        {
            RepairVirtualProjectState();
            EnsureScenarioFolder();
            MoveDefaultScenarioIntoProject();
            ConfigureScenario(ScenarioPath, 2);
        }

        [MenuItem("Tools/Monkey Lab/Start Host Client Play Mode")]
        public static void StartHostClientPlayMode()
        {
            if (IsPlayModeConfigurationRunning() ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                HasActiveVirtualPlayer())
            {
                Debug.LogWarning(
                    "[MonkeyLab] HostClient Play Mode is already running. " +
                    "Stop it before starting another session.");
                return;
            }

            Configure();
            var scenario = AssetDatabase.LoadMainAssetAtPath(ScenarioPath) ??
                           throw new InvalidOperationException(
                               "HostClient2 scenario was not found.");
            SetLastActiveScenario(scenario);
            AssignActivePlayModeConfiguration(scenario);
            ScheduleConfiguredPlayModeStart();
        }

        private static void ScheduleConfiguredPlayModeStart()
        {
            _hostClientStartTime = EditorApplication.timeSinceStartup + 0.2d;
            EditorApplication.update -= TryEnterConfiguredPlayMode;
            EditorApplication.update += TryEnterConfiguredPlayMode;
        }

        private static void TryEnterConfiguredPlayMode()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < _hostClientStartTime)
            {
                return;
            }

            EditorApplication.update -= TryEnterConfiguredPlayMode;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EnterConfiguredPlayMode();
            }
        }

        private static void EnterConfiguredPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            RepairVirtualProjectState();
            Debug.Log("[MonkeyLab] Starting HostClient2 Play Mode.");
            var didStart = EditorApplication.ExecuteMenuItem(
                "Edit/Play Mode/Play");
            if (!didStart)
            {
                throw new InvalidOperationException(
                    "Unity Play Mode command could not be executed.");
            }
        }

        /// <summary>
        /// Unity MPPM 2.0이 지원하는 최대 구성인 메인 에디터 1개와
        /// 추가 에디터 3개를 같은 부트스트랩 씬으로 실행한다.
        /// </summary>
        [MenuItem("Tools/Monkey Lab/Configure Four Editor Play Mode")]
        public static void ConfigureFourEditorPlayers()
        {
            RepairVirtualProjectState();
            EnsureScenarioFolder();
            MoveDefaultScenarioIntoProject();
            if (AssetDatabase.LoadMainAssetAtPath(FourPlayerScenarioPath) == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ScenarioPath) == null ||
                    !AssetDatabase.CopyAsset(
                        ScenarioPath,
                        FourPlayerScenarioPath))
                {
                    throw new InvalidOperationException(
                        "HostClient4 scenario could not be created from HostClient2.");
                }

                AssetDatabase.ImportAsset(
                    FourPlayerScenarioPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureScenario(FourPlayerScenarioPath, 4);
        }

        [MenuItem("Tools/Monkey Lab/Use Standard Play Mode")]
        public static void UseStandardPlayMode()
        {
            if (!RestoreStandardPlayMode())
            {
                Debug.LogWarning(
                    "[MonkeyLab] Stop HostClient Play Mode before switching " +
                    "to Standard Play Mode.");
                return;
            }

            Selection.activeObject = null;
            Debug.Log("[MonkeyLab] Standard Play Mode is active.");
        }

        [MenuItem("Tools/Monkey Lab/Stop Host Client Play Mode")]
        public static void StopHostClientPlayMode()
        {
            EditorApplication.update -= TryEnterConfiguredPlayMode;
            if (!IsPlayModeConfigurationRunning())
            {
                Debug.Log("[MonkeyLab] HostClient Play Mode is already stopped.");
                return;
            }

            StopActivePlayModeConfiguration();
        }

        private static bool RestoreStandardPlayMode()
        {
            if (IsPlayModeConfigurationRunning() ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                HasActiveVirtualPlayer())
            {
                return false;
            }

            try
            {
                AssignDefaultPlayModeConfiguration();
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                return false;
            }

            SetLastActiveScenario(null);
            return true;
        }

        private static bool IsPlayModeConfigurationRunning()
        {
            var managerType = GetPlayModeManagerType();
            var manager = GetPlayModeManagerInstance(managerType);
            var state = managerType.GetProperty(
                    "CurrentState",
                    BindingFlags.Public | BindingFlags.Instance)?
                .GetValue(manager);
            return state != null && Convert.ToInt32(state) == 1;
        }

        private static void StopActivePlayModeConfiguration()
        {
            var managerType = GetPlayModeManagerType();
            var manager = GetPlayModeManagerInstance(managerType);
            var stopMethod = managerType.GetMethod(
                "Stop",
                BindingFlags.Public | BindingFlags.Instance) ??
                throw new InvalidOperationException(
                    "Play Mode manager stop method was not found.");
            stopMethod.Invoke(manager, null);
        }

        private static Type GetPlayModeManagerType()
        {
            var playModeAssembly = Assembly.Load("UnityEditor.PlayModeModule");
            return playModeAssembly.GetType(
                       "Unity.PlayMode.Editor.PlayModeManager") ??
                   FindTypeByName(playModeAssembly, "PlayModeManager") ??
                   throw new InvalidOperationException(
                       "Play Mode manager type was not found.");
        }

        private static object GetPlayModeManagerInstance(Type managerType)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy;
            return managerType.GetProperty("instance", flags)?
                       .GetValue(null) ??
                   throw new InvalidOperationException(
                       "Play Mode manager instance was not found.");
        }

        private static void AssignDefaultPlayModeConfiguration()
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy;

            var playModeAssembly = Assembly.Load("UnityEditor.PlayModeModule");
            var managerType =
                playModeAssembly.GetType(
                    "Unity.PlayMode.Editor.PlayModeManager") ??
                FindTypeByName(playModeAssembly, "PlayModeManager") ??
                throw new InvalidOperationException(
                    "Play Mode manager type was not found.");
            var defaultConfigurationType =
                playModeAssembly.GetType(
                    "Unity.PlayMode.Editor.DefaultPlayModeConfiguration") ??
                FindTypeByName(
                    playModeAssembly,
                    "DefaultPlayModeConfiguration") ??
                throw new InvalidOperationException(
                    "Default Play Mode configuration type was not found.");
            var defaultConfigurations =
                Resources.FindObjectsOfTypeAll(defaultConfigurationType);
            var defaultConfiguration =
                defaultConfigurations.Length > 0
                    ? defaultConfigurations[0]
                    : ScriptableObject.CreateInstance(
                        defaultConfigurationType);
            defaultConfiguration.hideFlags = HideFlags.HideAndDontSave;

            var activeConfigurationProperty = managerType.GetProperty(
                "ActivePlayModeConfig",
                flags) ??
                throw new InvalidOperationException(
                    "Active Play Mode configuration property was not found.");
            var setter = activeConfigurationProperty.GetSetMethod(true) ??
                         throw new InvalidOperationException(
                             "Active Play Mode configuration is read-only.");
            object manager = null;
            if (!setter.IsStatic)
            {
                manager = managerType.GetProperty("instance", flags)?
                    .GetValue(null) ??
                    throw new InvalidOperationException(
                        "Play Mode manager instance was not found.");
            }

            setter.Invoke(manager, new object[] { defaultConfiguration });
        }

        private static void ConfigureScenario(
            string scenarioPath,
            int totalPlayerCount)
        {
            var scenario = AssetDatabase.LoadMainAssetAtPath(scenarioPath);
            if (scenario == null)
            {
                throw new InvalidOperationException(
                    "Create the Multiplayer Play Mode Scenario first: " +
                    scenarioPath);
            }

            var bootstrapScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapScene == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap scene was not found.");
            }

            var serializedScenario = new SerializedObject(scenario);
            var hasChanges = SetBooleanIfDifferent(
                serializedScenario,
                "m_EnableEditors",
                true);
            hasChanges |= ConfigureInstance(
                GetRequiredProperty(
                    serializedScenario,
                    "m_MainEditorInstance"),
                "Main Editor",
                0,
                bootstrapScene);

            var editorInstances = GetRequiredProperty(
                serializedScenario,
                "m_EditorInstances");
            var requiredEditorCount = totalPlayerCount - 1;
            if (editorInstances.arraySize != requiredEditorCount)
            {
                editorInstances.arraySize = requiredEditorCount;
                hasChanges = true;
            }

            for (var index = 0; index < editorInstances.arraySize; index++)
            {
                var playerNumber = index + 2;
                hasChanges |= ConfigureInstance(
                    editorInstances.GetArrayElementAtIndex(index),
                    $"Player {playerNumber}",
                    playerNumber - 1,
                    bootstrapScene);
            }

            if (hasChanges)
            {
                // 활성 ScenarioConfig를 저장하면 Unity 6.3 PreviewImporter가
                // 초기화되지 않은 PlayerOne을 읽는다. 저장 전 표준 설정으로
                // 전환하고 영구 선택값은 비워 둔다.
                SetLastActiveScenario(null);
                AssignDefaultPlayModeConfiguration();
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(scenario);
                AssetDatabase.SaveAssets();
            }

            SetLastActiveScenario(null);
            // ScenarioConfig를 선택하면 Unity 6.3의 PreviewImporter 워커가
            // PlayerOne 없이 GetAllInstances를 호출해 AssertionException을 낸다.
            // 활성화에는 Project 창 선택이 필요하지 않으므로 선택하지 않는다.
            Selection.activeObject = null;
            Debug.Log(
                $"[MonkeyLab] {totalPlayerCount}-player Play Mode Scenario " +
                "is configured.");
        }

        private static void SetLastActiveScenario(UnityEngine.Object scenario)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy;

            Type settingsType = null;
            Exception assemblyLoadException = null;
            try
            {
                var playModeAssembly = Assembly.Load(
                    "UnityEditor.PlayModeModule");
                settingsType =
                    playModeAssembly.GetType(
                        "Unity.PlayMode.Editor.PlayModeUserSettings") ??
                    playModeAssembly.GetType(
                        "UnityEditor.PlayMode.Editor.PlayModeUserSettings") ??
                    FindTypeByName(
                        playModeAssembly,
                        "PlayModeUserSettings");
            }
            catch (Exception exception)
            {
                assemblyLoadException = exception;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (settingsType != null)
                {
                    break;
                }

                settingsType =
                    assembly.GetType(
                        "Unity.PlayMode.Editor.PlayModeUserSettings") ??
                    assembly.GetType(
                        "UnityEditor.PlayMode.Editor.PlayModeUserSettings") ??
                    FindTypeByName(
                        assembly,
                        "PlayModeUserSettings");
                if (settingsType != null)
                {
                    break;
                }
            }

            if (settingsType == null)
            {
                throw new InvalidOperationException(
                    "Play Mode user settings type was not found.",
                    assemblyLoadException);
            }

            var instanceProperty = settingsType.GetProperty("instance", flags);
            var settings = instanceProperty?.GetValue(null);
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Play Mode user settings instance was not found.");
            }

            var lastActiveProperty = settingsType.GetProperty(
                "LastActiveConfiguration",
                flags);
            if (lastActiveProperty == null)
            {
                throw new InvalidOperationException(
                    "Play Mode active configuration property was not found.");
            }

            lastActiveProperty.SetValue(settings, scenario);

            MethodInfo saveMethod = null;
            for (var type = settingsType;
                 type != null && saveMethod == null;
                 type = type.BaseType)
            {
                saveMethod = type.GetMethod(
                    "Save",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly,
                    null,
                    new[] { typeof(bool) },
                    null);
            }

            if (saveMethod == null)
            {
                throw new InvalidOperationException(
                    "Play Mode user settings save method was not found.");
            }

            saveMethod.Invoke(settings, new object[] { true });

        }

        private static void AssignActivePlayModeConfiguration(
            UnityEngine.Object configuration)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy;

            var playModeAssembly = Assembly.Load("UnityEditor.PlayModeModule");
            var managerType =
                playModeAssembly.GetType(
                    "Unity.PlayMode.Editor.PlayModeManager") ??
                FindTypeByName(playModeAssembly, "PlayModeManager") ??
                throw new InvalidOperationException(
                    "Play Mode manager type was not found.");
            var activeConfigurationProperty = managerType.GetProperty(
                "ActivePlayModeConfig",
                flags) ??
                throw new InvalidOperationException(
                    "Active Play Mode configuration property was not found.");
            if (!activeConfigurationProperty.PropertyType.IsInstanceOfType(
                    configuration))
            {
                throw new InvalidOperationException(
                    $"{configuration.GetType().FullName} cannot be used as " +
                    $"{activeConfigurationProperty.PropertyType.FullName}.");
            }

            var setter = activeConfigurationProperty.GetSetMethod(true) ??
                         throw new InvalidOperationException(
                             "Active Play Mode configuration is read-only.");
            object manager = null;
            if (!setter.IsStatic)
            {
                manager = managerType.GetProperty("instance", flags)?
                    .GetValue(null) ??
                    throw new InvalidOperationException(
                        "Play Mode manager instance was not found.");
            }

            setter.Invoke(manager, new object[] { configuration });
        }

        private static Type FindTypeByName(
            Assembly assembly,
            string typeName)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (string.Equals(
                            type.Name,
                            typeName,
                            StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                return null;
            }

            return null;
        }

        private static void EnsureScenarioFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenarioFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Settings",
                    "PlayMode");
            }
        }

        private static void MoveDefaultScenarioIntoProject()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ScenarioPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(DefaultScenarioPath) == null)
            {
                return;
            }

            var moveError = AssetDatabase.MoveAsset(
                DefaultScenarioPath,
                ScenarioPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new InvalidOperationException(moveError);
            }
        }

        [MenuItem("Tools/Monkey Lab/Repair Host Client Play Mode Cache")]
        public static void RepairHostClientPlayModeCache()
        {
            if (RepairVirtualProjectState())
            {
                Debug.Log(
                    "[MonkeyLab] Invalid Multiplayer Play Mode virtual " +
                    "project state was repaired.");
            }
            else
            {
                Debug.Log(
                    "[MonkeyLab] Multiplayer Play Mode virtual project " +
                    "state is already valid.");
            }
        }

        private static bool RepairVirtualProjectState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                IsScenarioClone() ||
                !File.Exists(VirtualProjectSystemDataPath))
            {
                return false;
            }

            try
            {
                var root = JObject.Parse(
                    File.ReadAllText(VirtualProjectSystemDataPath));
                if (root["Data"] is not JObject players)
                {
                    return false;
                }

                var hasChanges = false;
                foreach (var playerProperty in players.Properties())
                {
                    if (playerProperty.Value is not JObject player ||
                        player.Value<int?>("Type") == 0)
                    {
                        continue;
                    }

                    var typeDependentInfo =
                        player["TypeDependentPlayerInfo"] as JObject;
                    var identifier =
                        typeDependentInfo?["VirtualProjectIdentifier"]
                            as JObject;
                    if (identifier == null)
                    {
                        continue;
                    }

                    var id = identifier.Value<string>("m_Id");
                    var prefix = identifier.Value<string>("m_Prefix");
                    var directoryName = string.Concat(prefix, id);
                    var hasUsableIdentifier =
                        !string.IsNullOrWhiteSpace(id) &&
                        !string.IsNullOrWhiteSpace(prefix) &&
                        Directory.Exists(
                            Path.Combine(
                                VirtualProjectFolderPath,
                                directoryName));
                    if (hasUsableIdentifier)
                    {
                        continue;
                    }

                    typeDependentInfo["VirtualProjectIdentifier"] =
                        JValue.CreateNull();
                    player["Active"] = false;
                    hasChanges = true;
                }

                if (!hasChanges)
                {
                    return false;
                }

                File.WriteAllText(
                    VirtualProjectSystemDataPath,
                    root.ToString(Formatting.Indented));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MonkeyLab] Multiplayer Play Mode cache repair " +
                    $"was skipped: {exception.Message}");
                return false;
            }
        }

        private static bool HasActiveVirtualPlayer()
        {
            if (!File.Exists(VirtualProjectSystemDataPath))
            {
                return false;
            }

            try
            {
                var root = JObject.Parse(
                    File.ReadAllText(VirtualProjectSystemDataPath));
                if (root["Data"] is not JObject players)
                {
                    return false;
                }

                foreach (var playerProperty in players.Properties())
                {
                    if (playerProperty.Value is JObject player &&
                        player.Value<int?>("Type") != 0 &&
                        player.Value<bool?>("Active") == true)
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            return false;
        }

        private static bool IsScenarioClone()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(
                        argument,
                        "-scenarioClone",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConfigureInstance(
            SerializedProperty instance,
            string name,
            int instanceIndex,
            SceneAsset initialScene)
        {
            var runNodeId = $"{name}|{instanceIndex}_run";
            var deployNodeId = $"{name}|{instanceIndex}_deploy";
            var hasChanges = SetStringIfDifferent(instance, "Name", name);
            hasChanges |= SetStringIfDifferent(
                instance,
                "<CorrespondingNodeId>k__BackingField",
                runNodeId);
            var nodes = instance.FindPropertyRelative("m_Nodes") ??
                        throw new InvalidOperationException(
                            "Missing scenario property: m_Nodes");
            if (nodes.arraySize != 2)
            {
                nodes.arraySize = 2;
                hasChanges = true;
            }

            hasChanges |= SetStringIfDifferent(
                nodes.GetArrayElementAtIndex(0),
                runNodeId);
            hasChanges |= SetStringIfDifferent(
                nodes.GetArrayElementAtIndex(1),
                deployNodeId);

            hasChanges |= SetIntegerIfDifferent(instance, "m_Role", 1);
            hasChanges |= SetStringIfDifferent(
                instance,
                "m_PlayerTag",
                string.Empty);
            var scene = instance.FindPropertyRelative("m_InitialScene") ??
                        throw new InvalidOperationException(
                            "Missing scenario property: m_InitialScene");
            if (scene.objectReferenceValue != initialScene)
            {
                scene.objectReferenceValue = initialScene;
                hasChanges = true;
            }

            return hasChanges;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                   throw new InvalidOperationException(
                       "Missing scenario property: " + propertyName);
        }

        private static bool SetBooleanIfDifferent(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            var property = GetRequiredProperty(
                serializedObject,
                propertyName);
            if (property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetStringIfDifferent(
            SerializedProperty parent,
            string propertyName,
            string value)
        {
            var property = parent.FindPropertyRelative(propertyName) ??
                           throw new InvalidOperationException(
                               "Missing scenario property: " + propertyName);
            return SetStringIfDifferent(property, value);
        }

        private static bool SetStringIfDifferent(
            SerializedProperty property,
            string value)
        {
            if (string.Equals(
                    property.stringValue,
                    value,
                    StringComparison.Ordinal))
            {
                return false;
            }

            property.stringValue = value;
            return true;
        }

        private static bool SetIntegerIfDifferent(
            SerializedProperty parent,
            string propertyName,
            int value)
        {
            var property = parent.FindPropertyRelative(propertyName) ??
                           throw new InvalidOperationException(
                               "Missing scenario property: " + propertyName);
            if (property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }
    }
}
