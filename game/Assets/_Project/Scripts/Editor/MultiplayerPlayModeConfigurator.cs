using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
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
        private const string SixPlayerScenarioPath =
            ScenarioFolder + "/HostClient6.asset";
        private const string BootstrapScenePath =
            "Assets/_Project/Scenes/00_Bootstrap.unity";

        [MenuItem("Tools/Monkey Lab/Configure Host Client Play Mode")]
        public static void Configure()
        {
            EnsureScenarioFolder();
            MoveDefaultScenarioIntoProject();
            ConfigureScenario(ScenarioPath, 2);
        }

        /// <summary>
        /// 메인 에디터 1개와 가상 플레이어 5개를 같은 부트스트랩 씬으로
        /// 실행하는 6인 시나리오를 만든다. 실제 Play Mode는 사용자가 시작한다.
        /// </summary>
        [MenuItem("Tools/Monkey Lab/Configure Six Player Play Mode")]
        public static void ConfigureSixPlayer()
        {
            EnsureScenarioFolder();
            MoveDefaultScenarioIntoProject();
            if (AssetDatabase.LoadMainAssetAtPath(SixPlayerScenarioPath) == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ScenarioPath) == null ||
                    !AssetDatabase.CopyAsset(
                        ScenarioPath,
                        SixPlayerScenarioPath))
                {
                    throw new InvalidOperationException(
                        "HostClient6 scenario could not be created from HostClient2.");
                }

                AssetDatabase.ImportAsset(
                    SixPlayerScenarioPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureScenario(SixPlayerScenarioPath, 6);
        }

        [MenuItem("Tools/Monkey Lab/Use Standard Play Mode")]
        public static void UseStandardPlayMode()
        {
            ActivateScenario(null);
            AssignDefaultPlayModeConfiguration();
            Selection.activeObject = null;
            Debug.Log("[MonkeyLab] Standard Play Mode is active.");
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
            GetRequiredProperty(
                serializedScenario,
                "m_Description").stringValue =
                $"Monkey Lab {totalPlayerCount}-player session";
            SetRequiredBoolean(serializedScenario, "m_EnableEditors", true);
            ConfigureInstance(
                GetRequiredProperty(
                    serializedScenario,
                    "m_MainEditorInstance"),
                "Main Editor",
                0,
                bootstrapScene);

            var editorInstances = GetRequiredProperty(
                serializedScenario,
                "m_EditorInstances");
            editorInstances.arraySize = totalPlayerCount - 1;
            for (var index = 0; index < editorInstances.arraySize; index++)
            {
                var playerNumber = index + 2;
                ConfigureInstance(
                    editorInstances.GetArrayElementAtIndex(index),
                    $"Player {playerNumber}",
                    playerNumber - 1,
                    bootstrapScene);
            }

            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ActivateScenario(scenario);
            Selection.activeObject = scenario;
            Debug.Log(
                $"[MonkeyLab] {totalPlayerCount}-player Play Mode Scenario " +
                "is configured and selected.");
        }

        private static void ActivateScenario(UnityEngine.Object scenario)
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
            if (settings is not UnityEngine.Object settingsObject)
            {
                throw new InvalidOperationException(
                    "Play Mode user settings are not serializable.");
            }

            var serializedSettings = new SerializedObject(settingsObject);
            GetRequiredProperty(
                    serializedSettings,
                    "m_LastActiveConfiguration")
                .objectReferenceValue = scenario;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new[] { settingsObject },
                "UserSettings/PlayModeUserSettings.asset",
                true);

            // LastActiveConfiguration은 드롭다운의 저장값일 뿐, 이미 Default가 활성화된
            // 에디터 세션의 현재 실행 설정은 바꾸지 않는다. 현재 설정도 즉시 교체해야
            // 재시작 없이 다음 Play에서 추가 Editor 인스턴스가 뜬다.
            if (scenario != null)
            {
                AssignActivePlayModeConfiguration(scenario);
            }
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

        private static void ConfigureInstance(
            SerializedProperty instance,
            string name,
            int instanceIndex,
            SceneAsset initialScene)
        {
            var nodeId = $"{name}|{instanceIndex}_run/deploy";
            SetRequiredString(instance, "Name", name);
            SetRequiredString(
                instance,
                "<CorrespondingNodeId>k__BackingField",
                nodeId);
            var nodes = instance.FindPropertyRelative("m_Nodes") ??
                        throw new InvalidOperationException(
                            "Missing scenario property: m_Nodes");
            nodes.arraySize = 1;
            nodes.GetArrayElementAtIndex(0).stringValue = nodeId;

            SetRequiredInteger(instance, "m_Role", 1);
            SetRequiredString(instance, "m_PlayerTag", string.Empty);
            var scene = instance.FindPropertyRelative("m_InitialScene") ??
                        throw new InvalidOperationException(
                            "Missing scenario property: m_InitialScene");
            scene.objectReferenceValue = initialScene;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                   throw new InvalidOperationException(
                       "Missing scenario property: " + propertyName);
        }

        private static void SetRequiredBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetRequiredString(
            SerializedProperty parent,
            string propertyName,
            string value)
        {
            var property = parent.FindPropertyRelative(propertyName) ??
                           throw new InvalidOperationException(
                               "Missing scenario property: " + propertyName);
            property.stringValue = value;
        }

        private static void SetRequiredInteger(
            SerializedProperty parent,
            string propertyName,
            int value)
        {
            var property = parent.FindPropertyRelative(propertyName) ??
                           throw new InvalidOperationException(
                               "Missing scenario property: " + propertyName);
            property.intValue = value;
        }
    }
}
