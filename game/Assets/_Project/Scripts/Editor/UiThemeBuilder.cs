using System;
using System.Linq;
using MonkeyLab.Presentation.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// uGUI·TMP 화면이 공유하는 <see cref="UiThemeConfig"/> 원본을 만들고
    /// <c>Art/Fonts</c>의 TMP 폰트 에셋을 연결한다.
    /// docs/ui-ux-design.md §15.4 기준이다.
    /// </summary>
    public static class UiThemeBuilder
    {
        private const string ThemeFolder = "Assets/_Project/Data/UI";
        private const string ThemePath =
            ThemeFolder + "/SO_UiTheme_Default.asset";
        private const string FontFolder = "Assets/_Project/Art/Fonts";
        private const string ResourcesFolder = "Assets/_Project/Resources";
        private const string ImguiFontSetPath =
            ResourcesFolder + "/SO_ImguiFontSet.asset";

        [MenuItem("Tools/Monkey Lab/Build/Create Or Update UI Theme")]
        public static void CreateOrUpdate()
        {
            // 폰트 파일만 있으면 TMP 에셋 생성까지 여기서 끝낸다. 손으로
            // Font Asset Creator를 돌리는 단계를 남겨두면 사람마다 설정이 달라진다.
            KoreanFontAssetBuilder.BuildMissingFontAssets();

            // 화면을 uGUI로 옮기기 전에도 한글 서체가 보이도록 IMGUI 쪽을 먼저 연결한다.
            EnsureImguiFontSet();

            var theme = EnsureTheme();
            var boldFont = FindFont("Bold");
            var regularFont = FindFont("Regular");
            var displayFont = FindFont("Display");
            AssignFonts(theme, boldFont, regularFont, displayFont);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Selection.activeObject = theme;

            if (theme.HasFonts)
            {
                Debug.Log(
                    "[MonkeyLab] UI theme is ready: " + ThemePath +
                    $" (bold: {boldFont.name}, regular: {regularFont.name}).");
                return;
            }

            Debug.LogWarning(
                "[MonkeyLab] UI theme was created but no TMP font asset was " +
                $"found in {FontFolder}. Add a redistributable Korean font, " +
                "generate Bold and Regular SDF assets with Window > " +
                "TextMeshPro > Font Asset Creator, then run this menu again. " +
                "Screens built without fonts cannot render Korean text.");
        }

        /// <summary>
        /// 화면 빌더가 폰트 없이 프리팹을 만들지 않도록 먼저 확인한다.
        /// </summary>
        public static UiThemeConfig LoadReadyThemeOrThrow()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeConfig>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    "Run Tools > Monkey Lab > Build > Create Or Update UI " +
                    "Theme first: " + ThemePath);
            }

            if (!theme.HasFonts)
            {
                throw new InvalidOperationException(
                    "The UI theme has no TMP font assets. Add them to " +
                    FontFolder + " before building UI prefabs.");
            }

            return theme;
        }

        /// <summary>
        /// IMGUI 화면이 쓸 폰트 지정을 만든다. 런타임에 <c>Resources.Load</c>로
        /// 찾으므로 이 에셋만 Resources 폴더에 둔다. 서체 파일은 옮기지 않는다.
        /// </summary>
        private static void EnsureImguiFontSet()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            }

            var fontSet =
                AssetDatabase.LoadAssetAtPath<ImguiFontSet>(ImguiFontSetPath);
            if (fontSet == null)
            {
                fontSet = ScriptableObject.CreateInstance<ImguiFontSet>();
                fontSet.name = "SO_ImguiFontSet";
                AssetDatabase.CreateAsset(fontSet, ImguiFontSetPath);
            }

            var serialized = new SerializedObject(fontSet);
            SetSourceFont(serialized, "_boldFont", "SCDream6");
            SetSourceFont(serialized, "_regularFont", "SCDream4");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontSet);
        }

        private static void SetSourceFont(
            SerializedObject serialized,
            string propertyName,
            string fontFileName)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(
                FontFolder + "/" + fontFileName + ".otf");
            var property = serialized.FindProperty(propertyName);
            if (property != null && font != null)
            {
                property.objectReferenceValue = font;
            }
        }

        private static UiThemeConfig EnsureTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeConfig>(ThemePath);
            if (theme != null)
            {
                return theme;
            }

            if (!AssetDatabase.IsValidFolder(ThemeFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "UI");
            }

            theme = ScriptableObject.CreateInstance<UiThemeConfig>();
            theme.name = "SO_UiTheme_Default";
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }

        /// <summary>
        /// 이름에 대상 굵기가 들어간 TMP 폰트 에셋을 찾는다. 굵기 이름이 없는
        /// 폰트가 하나만 있으면 그것을 공용으로 쓴다.
        /// </summary>
        private static TMP_FontAsset FindFont(string weightKeyword)
        {
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                return null;
            }

            var fonts = AssetDatabase
                .FindAssets("t:TMP_FontAsset", new[] { FontFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .Where(font => font != null)
                .ToArray();
            if (fonts.Length == 0)
            {
                return null;
            }

            var matched = fonts.FirstOrDefault(
                font => font.name.IndexOf(
                    weightKeyword,
                    StringComparison.OrdinalIgnoreCase) >= 0);
            return matched != null || fonts.Length > 1 ? matched : fonts[0];
        }

        private static void AssignFonts(
            UiThemeConfig theme,
            TMP_FontAsset boldFont,
            TMP_FontAsset regularFont,
            TMP_FontAsset displayFont)
        {
            var serializedTheme = new SerializedObject(theme);
            SetFont(serializedTheme, "_boldFont", boldFont);
            SetFont(serializedTheme, "_regularFont", regularFont);
            SetFont(serializedTheme, "_displayFont", displayFont);
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFont(
            SerializedObject serializedTheme,
            string propertyName,
            TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            var property = serializedTheme.FindProperty(propertyName) ??
                           throw new InvalidOperationException(
                               "Missing UI theme property: " + propertyName);
            property.objectReferenceValue = font;
        }
    }
}
