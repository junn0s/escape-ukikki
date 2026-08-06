using System;
using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// 미션 9종이 쓰는 UI 부품 그림을 절차적으로 만든다.
    /// 지금까지 미니게임은 <c>GUI.Box</c>에 색만 곱해 그려서, 퓨즈든 레버든
    /// 케이블이든 전부 같은 회색 사각형으로 보였다(ui-ux-design.md §9).
    ///
    /// 여기서 만든 스프라이트는 IMGUI와 uGUI 어느 쪽에서도 그대로 쓴다.
    /// 그림을 손으로 그려 교체할 때도 같은 이름으로 덮으면 된다.
    /// </summary>
    public static class MissionUiSpriteBuilder
    {
        private const string SpriteFolder = "Assets/_Project/UI/Sprites";

        /// <summary>부품 한 칸의 기준 픽셀. 직교 카메라와 무관한 화면 좌표계다.</summary>
        private const int PartSize = 128;

        private const float PixelsPerUnit = 128f;

        /// <summary>9-slice 테두리. 패널·버튼이 늘어나도 두께가 일정하다.</summary>
        private const int PanelBorder = 24;

        private static readonly Color32 Clear = new(0, 0, 0, 0);
        private static readonly Color32 Ink = new(18, 25, 34, 255);
        private static readonly Color32 Metal = new(126, 141, 156, 255);
        private static readonly Color32 MetalLight = new(178, 192, 205, 255);
        private static readonly Color32 MetalDark = new(74, 86, 99, 255);
        private static readonly Color32 Glass = new(210, 228, 236, 255);

        [MenuItem("Tools/Monkey Lab/Build/Create Mission UI Sprites")]
        public static void CreateAll()
        {
            EnsureFolder();
            var created = 0;
            created += Build("UI_Panel", CreatePanelPixel, PanelBorder);
            created += Build("UI_Button", CreateButtonPixel, PanelBorder);
            created += Build("UI_Slot", CreateSlotPixel, PanelBorder);
            created += Build("UI_Fuse", CreateFusePixel, 0);
            created += Build("UI_Lever", CreateLeverPixel, 0);
            created += Build("UI_Dial", CreateDialPixel, 0);
            created += Build("UI_Gauge", CreateGaugePixel, 0);
            created += Build("UI_CableEnd", CreateCableEndPixel, 0);
            created += Build("UI_Dish", CreateDishPixel, 0);
            created += Build("UI_Led", CreateLedPixel, 0);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[MonkeyLab] Mission UI sprites ready ({created} created) in " +
                SpriteFolder + ".");
        }

        /// <summary>
        /// 픽셀 함수를 고친 뒤 결과가 그대로면 이걸 실행한다. 새로 만들면 GUID가
        /// 바뀌므로 이 스프라이트를 참조하던 프리팹은 다시 연결해야 한다.
        /// </summary>
        [MenuItem("Tools/Monkey Lab/Build/Regenerate Mission UI Sprites")]
        public static void RegenerateAll()
        {
            EnsureFolder();
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:Sprite", new[] { SpriteFolder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateAll();
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(SpriteFolder))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/UI"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "UI");
            }

            AssetDatabase.CreateFolder("Assets/_Project/UI", "Sprites");
        }

        private static int Build(
            string spriteName,
            Func<int, int, Color32> pixelFactory,
            int border)
        {
            var path = SpriteFolder + "/" + spriteName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
            {
                return 0;
            }

            var texture = new Texture2D(
                PartSize,
                PartSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "T_" + spriteName[3..],
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[PartSize * PartSize];
            for (var y = 0; y < PartSize; y++)
            {
                for (var x = 0; x < PartSize; x++)
                {
                    pixels[y * PartSize + x] = pixelFactory(x, y);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, PartSize, PartSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = spriteName;
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            return 1;
        }

        // ---- 공통 프레임 (ui-ux-design.md §9.1) ----

        /// <summary>패널. 모서리 반경 20px, 어두운 배경, 밝은 테두리(§15.3).</summary>
        private static Color32 CreatePanelPixel(int x, int y)
        {
            if (!IsRoundedRect(x, y, 20))
            {
                return Clear;
            }

            var edge = EdgeDistance(x, y);
            if (edge <= 2)
            {
                return new Color32(58, 74, 99, 255);
            }

            return new Color32(20, 26, 38, 240);
        }

        /// <summary>버튼. 아래쪽 어두운 립으로 두께감을 준다(§15.3).</summary>
        private static Color32 CreateButtonPixel(int x, int y)
        {
            if (!IsRoundedRect(x, y, 16))
            {
                return Clear;
            }

            var edge = EdgeDistance(x, y);
            if (edge <= 2)
            {
                return Ink;
            }

            if (y < 14)
            {
                return new Color32(120, 148, 164, 255);
            }

            return y > PartSize - 22
                ? new Color32(255, 255, 255, 255)
                : new Color32(226, 236, 242, 255);
        }

        /// <summary>빈 소켓. 부품을 끼우기 전 자리를 보여준다.</summary>
        private static Color32 CreateSlotPixel(int x, int y)
        {
            if (!IsRoundedRect(x, y, 14))
            {
                return Clear;
            }

            var edge = EdgeDistance(x, y);
            if (edge <= 3)
            {
                return MetalDark;
            }

            // 안쪽은 비어 보이도록 어둡게 파낸다.
            return new Color32(28, 34, 44, 210);
        }

        // ---- 미션별 부품 ----

        /// <summary>퓨즈. 양끝 금속 캡과 가운데 유리관(§9.2).</summary>
        private static Color32 CreateFusePixel(int x, int y)
        {
            const float centerY = 63.5f;
            if (Mathf.Abs(y - centerY) > 26f)
            {
                return Clear;
            }

            var isCap = x < 26 || x > PartSize - 27;
            if (isCap)
            {
                if (Mathf.Abs(y - centerY) > 22f)
                {
                    return Clear;
                }

                return Mathf.Abs(y - centerY) > 18f ? MetalDark : Metal;
            }

            if (Mathf.Abs(y - centerY) > 24f)
            {
                return Ink;
            }

            // 유리관 안의 필라멘트
            if (Mathf.Abs(y - centerY) <= 2f)
            {
                return new Color32(232, 184, 75, 255);
            }

            return Glass;
        }

        /// <summary>차단기 레버. 세로 트랙 위의 손잡이(§9.3).</summary>
        private static Color32 CreateLeverPixel(int x, int y)
        {
            const float centerX = 63.5f;

            // 트랙
            if (Mathf.Abs(x - centerX) <= 12f && y is > 8 and < PartSize - 9)
            {
                if (Mathf.Abs(x - centerX) > 9f)
                {
                    return MetalDark;
                }

                return new Color32(34, 42, 54, 255);
            }

            // 손잡이
            if (Mathf.Abs(x - centerX) <= 34f && y is > 70 and < 106)
            {
                var edge = Mathf.Min(
                    Mathf.Min(34f - Mathf.Abs(x - centerX), y - 70f),
                    106f - y);
                return edge <= 3f ? Ink : MetalLight;
            }

            return Clear;
        }

        /// <summary>회전 다이얼. 현재 각도를 알리는 노치가 있다(§9.7, §9.8).</summary>
        private static Color32 CreateDialPixel(int x, int y)
        {
            const float center = 63.5f;
            var distance = Mathf.Sqrt(
                (x - center) * (x - center) + (y - center) * (y - center));
            if (distance > 58f)
            {
                return Clear;
            }

            if (distance > 52f)
            {
                return Ink;
            }

            // 위쪽 노치
            if (Mathf.Abs(x - center) <= 5f && y > center + 20f)
            {
                return new Color32(232, 117, 50, 255);
            }

            if (distance > 44f)
            {
                return MetalDark;
            }

            return distance > 18f ? Metal : MetalLight;
        }

        /// <summary>압력계. 눈금 링만 그리고 바늘은 코드가 회전시킨다(§9.7).</summary>
        private static Color32 CreateGaugePixel(int x, int y)
        {
            const float center = 63.5f;
            var distance = Mathf.Sqrt(
                (x - center) * (x - center) + (y - center) * (y - center));
            if (distance > 60f || distance < 34f)
            {
                return Clear;
            }

            if (distance > 56f || distance < 38f)
            {
                return Ink;
            }

            // 위험 구간을 붉게 표시해 색만으로 읽지 않아도 되게 한다.
            var angle = Mathf.Atan2(y - center, x - center) * Mathf.Rad2Deg;
            if (angle is > 20f and < 70f)
            {
                return new Color32(214, 59, 66, 255);
            }

            return MetalLight;
        }

        /// <summary>케이블 커넥터 끝단(§9.4).</summary>
        private static Color32 CreateCableEndPixel(int x, int y)
        {
            const float centerY = 63.5f;
            if (Mathf.Abs(y - centerY) > 30f)
            {
                return Clear;
            }

            // 플러그 몸통
            if (x < 78)
            {
                var edge = Mathf.Min(
                    30f - Mathf.Abs(y - centerY),
                    Mathf.Min(x - 6f, 78f - x));
                if (x < 6)
                {
                    return Clear;
                }

                return edge <= 3f ? Ink : MetalLight;
            }

            // 접점 핀 두 개
            if (Mathf.Abs(y - centerY) > 18f)
            {
                return Clear;
            }

            return Mathf.Abs(y - centerY) <= 6f
                ? Clear
                : new Color32(232, 184, 75, 255);
        }

        /// <summary>시료 접시(§9.5).</summary>
        private static Color32 CreateDishPixel(int x, int y)
        {
            const float center = 63.5f;
            var distance = Mathf.Sqrt(
                (x - center) * (x - center) + (y - center) * (y - center));
            if (distance > 58f)
            {
                return Clear;
            }

            if (distance > 50f)
            {
                return Ink;
            }

            if (distance > 44f)
            {
                return Glass;
            }

            // 안쪽 내용물은 틴트를 받도록 밝게 둔다.
            return new Color32(238, 246, 250, 200);
        }

        /// <summary>상태등. 색은 코드가 곱한다.</summary>
        private static Color32 CreateLedPixel(int x, int y)
        {
            const float center = 63.5f;
            var distance = Mathf.Sqrt(
                (x - center) * (x - center) + (y - center) * (y - center));
            if (distance > 46f)
            {
                return Clear;
            }

            if (distance > 38f)
            {
                return Ink;
            }

            // 위쪽에 하이라이트를 넣어 볼록해 보이게 한다.
            var highlight = Mathf.Clamp01(
                1f - Mathf.Sqrt(
                    (x - center) * (x - center) +
                    (y - center - 14f) * (y - center - 14f)) / 34f);
            var value = (byte)Mathf.RoundToInt(
                Mathf.Lerp(190f, 255f, highlight));
            return new Color32(value, value, value, 255);
        }

        // ---- 공통 도형 ----

        private static int EdgeDistance(int x, int y)
        {
            return Mathf.Min(
                Mathf.Min(x, PartSize - 1 - x),
                Mathf.Min(y, PartSize - 1 - y));
        }

        private static bool IsRoundedRect(int x, int y, int radius)
        {
            var clampedX = Mathf.Clamp(x, radius, PartSize - radius - 1);
            var clampedY = Mathf.Clamp(y, radius, PartSize - radius - 1);
            var dx = x - clampedX;
            var dy = y - clampedY;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
