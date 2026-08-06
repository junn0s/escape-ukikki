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
