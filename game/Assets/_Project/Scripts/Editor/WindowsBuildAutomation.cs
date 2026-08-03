using System;
using System.IO;
using System.Linq;
using MonkeyLab.Network;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// 6인 배포용 Windows x64 빌드 진입점이다. 메뉴와 batchmode가
    /// 같은 경로를 쓰며, 현재 활성 빌드 씬만 포함한다.
    /// </summary>
    public static class WindowsBuildAutomation
    {
        private const string ProductExecutableName = "EscapeUkikki.exe";
        private const string RelativeOutputFolder = "Builds/Windows";

        [MenuItem("Tools/Monkey Lab/Build/Windows 64-bit")]
        public static void BuildWindows64()
        {
            BuildWindows64Internal();
        }

        /// <summary>
        /// CI: -batchmode -quit -executeMethod
        /// MonkeyLab.EditorTools.WindowsBuildAutomation.BuildWindows64Batch
        /// </summary>
        public static void BuildWindows64Batch()
        {
            BuildWindows64Internal();
        }

        private static void BuildWindows64Internal()
        {
            if (NetworkPlayerSpawnLayout.SlotCount != 6)
            {
                throw new InvalidOperationException(
                    "Windows release requires exactly six player slots.");
            }

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0 ||
                scenes.Any(scenePath => !File.Exists(scenePath)))
            {
                throw new InvalidOperationException(
                    "Enabled build scenes are missing or empty.");
            }

            var projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            var outputFolder = Path.Combine(
                projectRoot,
                RelativeOutputFolder);
            Directory.CreateDirectory(outputFolder);
            var executablePath = Path.Combine(
                outputFolder,
                ProductExecutableName);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Windows build failed: " + report.summary.result);
            }

            Debug.Log(
                $"[MonkeyLab] Windows x64 build completed: {executablePath} " +
                $"({report.summary.totalSize} bytes).");
        }
    }
}
