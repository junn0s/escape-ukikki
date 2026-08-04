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

        [MenuItem("Tools/Monkey Lab/Build/Create Or Update UI Theme")]
        public static void CreateOrUpdate()
        {
            var theme = EnsureTheme();
            var boldFont = FindFont("Bold");
            var regularFont = FindFont("Regular");
            AssignFonts(theme, boldFont, regularFont);
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
            TMP_FontAsset regularFont)
        {
            var serializedTheme = new SerializedObject(theme);
            SetFont(serializedTheme, "_boldFont", boldFont);
            SetFont(serializedTheme, "_regularFont", regularFont);
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
