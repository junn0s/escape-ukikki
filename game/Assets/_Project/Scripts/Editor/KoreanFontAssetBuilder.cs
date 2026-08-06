using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// <c>Art/Fonts</c>의 한글 폰트 파일에서 TMP SDF 에셋을 만든다.
    /// Window > TextMeshPro > Font Asset Creator를 손으로 돌리지 않아도
    /// 저장소를 받은 사람이 같은 결과를 얻게 하는 것이 목적이다.
    ///
    /// 한글은 완성형만 11,172자라 모든 글자를 아틀라스에 미리 굽지 않는다.
    /// <see cref="AtlasPopulationMode.Dynamic"/>으로 두어 실제로 쓰인 글자만
    /// 실행 중에 아틀라스로 올린다.
    /// </summary>
    public static class KoreanFontAssetBuilder
    {
        private const string FontFolder = "Assets/_Project/Art/Fonts";

        /// <summary>SDF 품질과 아틀라스 크기. 한글 획이 뭉개지지 않는 최소선이다.</summary>
        private const int SamplingPointSize = 90;

        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        private static readonly FontSource[] Sources =
        {
            new("SCDream6.otf", "SCDream_Bold SDF"),
            new("SCDream4.otf", "SCDream_Regular SDF"),
            new("BMEULJIROTTF.ttf", "Euljiro_Display SDF")
        };

        /// <summary>
        /// 배치 모드 전용 진입점이다. <c>AssetDatabase.ImportPackage</c>가 비동기라
        /// <c>-quit</c>과 함께 부르면 임포트가 끝나기 전에 에디터가 내려간다.
        /// 완료 콜백에서 직접 종료하도록 두고, 호출할 때 <c>-quit</c>을 빼야 한다.
        /// </summary>
        public static void ImportTmpEssentialsForBatch()
        {
            if (TMP_Settings.instance != null)
            {
                Debug.Log("[MonkeyLab] TMP Essential Resources are already present.");
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.importPackageCompleted += _ =>
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[MonkeyLab] TMP Essential Resources imported.");
                EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError(
                    "[MonkeyLab] TMP Essential Resources import failed: " + error);
                EditorApplication.Exit(1);
            };
            AssetDatabase.importPackageCancelled += _ =>
            {
                Debug.LogError(
                    "[MonkeyLab] TMP Essential Resources import was cancelled.");
                EditorApplication.Exit(1);
            };

            TMP_PackageResourceImporter.ImportResources(
                importEssentials: true,
                importExamples: false,
                interactive: false);
        }

        [MenuItem("Tools/Monkey Lab/Build/Create Korean TMP Font Assets")]
        public static void CreateAll()
        {
            var created = BuildMissingFontAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[MonkeyLab] Korean TMP font assets ready ({created} created). " +
                "Run Create Or Update UI Theme next to link them.");
        }

        /// <summary>
        /// 이미 있는 에셋은 다시 만들지 않는다. 폰트 에셋을 새로 만들면 GUID가 바뀌어
        /// 이 폰트를 참조하던 화면 프리팹의 연결이 끊긴다.
        /// </summary>
        internal static int BuildMissingFontAssets()
        {
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                throw new InvalidOperationException(
                    "Font folder is missing: " + FontFolder);
            }

            EnsureTmpEssentialResources();

            var created = 0;
            var missingSources = new List<string>();
            foreach (var source in Sources)
            {
                var sourcePath = FontFolder + "/" + source.FileName;
                var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (font == null)
                {
                    missingSources.Add(source.FileName);
                    continue;
                }

                var assetPath = FontFolder + "/" + source.AssetName + ".asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                {
                    continue;
                }

                CreateFontAsset(font, source.AssetName, assetPath);
                created++;
            }

            if (missingSources.Count > 0)
            {
                Debug.LogWarning(
                    "[MonkeyLab] These font files were not found in " +
                    FontFolder + ": " + string.Join(", ", missingSources) +
                    ". See LICENSE.md in that folder for where to get them.");
            }

            return created;
        }

        /// <summary>
        /// TMP Essential Resources가 없으면 <c>TMP_Settings</c>가 만들어지지 않고,
        /// 폰트 에셋 생성이 그 안에서 NullReferenceException으로 죽는다.
        /// 저장소를 처음 받은 사람도 메뉴 하나로 끝나도록 여기서 먼저 임포트한다.
        /// </summary>
        private static void EnsureTmpEssentialResources()
        {
            if (TMP_Settings.instance != null)
            {
                return;
            }

            Debug.Log(
                "[MonkeyLab] TMP Essential Resources are missing. Importing them first.");
            TMP_PackageResourceImporter.ImportResources(
                importEssentials: true,
                importExamples: false,
                interactive: false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (TMP_Settings.instance == null)
            {
                // 패키지 임포트는 비동기라 같은 호출 안에서는 끝나지 않는다.
                throw new InvalidOperationException(
                    "TMP Essential Resources import has been started but is not " +
                    "finished yet. Run this menu once more.");
            }
        }

        private static void CreateFontAsset(
            Font font,
            string assetName,
            string assetPath)
        {
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    "TMP font asset creation failed for " + assetName);
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // 아틀라스 텍스처와 재질을 하위 에셋으로 넣지 않으면 다음 임포트에서
            // 참조가 끊겨 글자가 사라진다.
            if (fontAsset.atlasTextures != null)
            {
                for (var index = 0; index < fontAsset.atlasTextures.Length; index++)
                {
                    var atlas = fontAsset.atlasTextures[index];
                    if (atlas == null)
                    {
                        continue;
                    }

                    atlas.name = assetName + " Atlas " + index;
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
        }

        private readonly struct FontSource
        {
            public FontSource(string fileName, string assetName)
            {
                FileName = fileName;
                AssetName = assetName;
            }

            public string FileName { get; }
            public string AssetName { get; }
        }
    }
}
