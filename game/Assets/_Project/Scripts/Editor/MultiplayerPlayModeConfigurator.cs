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
        private const string BootstrapScenePath =
            "Assets/_Project/Scenes/00_Bootstrap.unity";

        [MenuItem("Tools/Monkey Lab/Configure Host Client Play Mode")]
        public static void Configure()
        {
            EnsureScenarioFolder();
            MoveDefaultScenarioIntoProject();

            var scenario = AssetDatabase.LoadMainAssetAtPath(ScenarioPath);
            if (scenario == null)
            {
                throw new InvalidOperationException(
                    "Create the HostClient2 Play Mode Scenario first.");
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
                "m_Description").stringValue = string.Empty;
            SetRequiredBoolean(serializedScenario, "m_EnableEditors", true);
            ConfigureInstance(
                GetRequiredProperty(
                    serializedScenario,
                    "m_MainEditorInstance"),
                "Editor",
                bootstrapScene);

            var editorInstances = GetRequiredProperty(
                serializedScenario,
                "m_EditorInstances");
            editorInstances.arraySize = 1;
            ConfigureInstance(
                editorInstances.GetArrayElementAtIndex(0),
                "Player 2",
                bootstrapScene);

            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ActivateScenario(scenario);
            Selection.activeObject = scenario;
            Debug.Log(
                "[MonkeyLab] HostClient2 Play Mode Scenario is configured " +
                "with Editor and Player 2.");
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
            SceneAsset initialScene)
        {
            SetRequiredString(instance, "Name", name);
            SetRequiredString(
                instance,
                "<CorrespondingNodeId>k__BackingField",
                string.Empty);
            var nodes = instance.FindPropertyRelative("m_Nodes");
            if (nodes != null)
            {
                nodes.arraySize = 0;
            }

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
