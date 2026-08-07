using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 직교 카메라와 2D 게임 좌표는 유지하면서 벽 정면을 아래로 세워 보이게 한다.
    /// 씬을 다시 생성하지 않아도 기존 연구소에 같은 표현 규칙을 적용할 수 있다.
    /// </summary>
    public static class MixedPerspectiveSceneStyler
    {
        /// <summary>
        /// 화면 위쪽 바닥 경계에서 아래로 내려오는 벽 정면 높이다.
        /// 문틀보다 충분히 높여 방의 상단 벽이 낮은 띠처럼 보이지 않게 한다.
        /// </summary>
        public const float WallFaceHeight = 2.45f;

        private const string DecorationRootName =
            "[Presentation] Room Wall Decorations";
        private const string RoomFloorResourcePrefix =
            "Environment/Floors/T_Floor_";
        private const float WallTopTolerance = 0.18f;
        private const float MinimumWallSegmentWidth = 0.7f;
        private const int WallpaperSortingOffset = 0;
        private const int FixtureSortingOffset = 5;
        private const int RoomLabelSortingOrder = 12;

        private static Sprite _unitSprite;

        private enum WallPattern : byte
        {
            Panel = 0,
            Stripe = 1,
            Hazard = 2
        }

        private enum WallFixtureKind : byte
        {
            Cctv = 0,
            DigitalClock = 1,
            SystemGauge = 2
        }

        private readonly struct RoomWallTheme
        {
            public RoomWallTheme(
                Color wallpaper,
                Color accent,
                WallPattern pattern,
                WallFixtureKind secondaryFixture)
            {
                Wallpaper = wallpaper;
                Accent = accent;
                Pattern = pattern;
                SecondaryFixture = secondaryFixture;
            }

            public Color Wallpaper { get; }
            public Color Accent { get; }
            public WallPattern Pattern { get; }
            public WallFixtureKind SecondaryFixture { get; }
        }

        private readonly struct WallSegment
        {
            public WallSegment(
                SpriteRenderer source,
                float minX,
                float maxX,
                float topY)
            {
                Source = source;
                MinX = minX;
                MaxX = maxX;
                TopY = topY;
            }

            public SpriteRenderer Source { get; }
            public float MinX { get; }
            public float MaxX { get; }
            public float TopY { get; }
            public float Width => MaxX - MinX;
            public float CenterX => (MinX + MaxX) * 0.5f;
        }

        public static void ApplyTo(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var wallFaces = new List<SpriteRenderer>();
            var roomFloors = new List<SpriteRenderer>();
            var propSlots = new List<EnvironmentPropSlot>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0;
                 rootIndex < roots.Length;
                 rootIndex++)
            {
                var root = roots[rootIndex];
                var renderers = root
                    .GetComponentsInChildren<SpriteRenderer>(true);
                for (var rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (renderer.gameObject.name.StartsWith(
                            "WallFace_",
                            StringComparison.Ordinal))
                    {
                        ApplyWallFace(renderer);
                        wallFaces.Add(renderer);
                    }
                    else if (renderer.gameObject.name.StartsWith(
                                 "Room_",
                                 StringComparison.Ordinal))
                    {
                        roomFloors.Add(renderer);
                    }
                    else if (renderer.gameObject.name.StartsWith(
                                 "Label_",
                                 StringComparison.Ordinal))
                    {
                        RaiseRoomLabel(renderer);
                    }
                }

                var rootPropSlots = root
                    .GetComponentsInChildren<EnvironmentPropSlot>(true);
                for (var slotIndex = 0;
                     slotIndex < rootPropSlots.Length;
                     slotIndex++)
                {
                    var slot = rootPropSlots[slotIndex];
                    if (slot == null)
                    {
                        continue;
                    }

                    slot.ApplyMixedPerspectivePresentation();
                    propSlots.Add(slot);
                }
            }

            HideRepeatedGenericWallFixtures(propSlots);
            ApplyRoomFloorTextures(roomFloors);
            CreateRoomWallDecorations(
                scene,
                roots,
                roomFloors,
                wallFaces);
        }

        private static void ApplyRoomFloorTextures(
            IReadOnlyList<SpriteRenderer> roomFloors)
        {
            for (var index = 0; index < roomFloors.Count; index++)
            {
                var floor = roomFloors[index];
                if (floor == null ||
                    !floor.gameObject.name.StartsWith(
                        "Room_",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var roomId = floor.gameObject.name.Substring(
                    "Room_".Length);
                var roomSprite = Resources.Load<Sprite>(
                    RoomFloorResourcePrefix + roomId);
                if (roomSprite == null)
                {
                    Debug.LogWarning(
                        $"[MonkeyLab] Dedicated floor texture is missing for {roomId}.",
                        floor);
                    continue;
                }

                var currentSize = floor.size;
                floor.sprite = roomSprite;
                floor.drawMode = SpriteDrawMode.Tiled;
                floor.size = currentSize;
                floor.color = Color.white;
            }
        }

        private static void RaiseRoomLabel(SpriteRenderer labelPanel)
        {
            labelPanel.sortingOrder = Mathf.Max(
                labelPanel.sortingOrder,
                RoomLabelSortingOrder);
            var labelRenderers = labelPanel
                .GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < labelRenderers.Length; index++)
            {
                if (labelRenderers[index] != null)
                {
                    labelRenderers[index].sortingOrder = Mathf.Max(
                        labelRenderers[index].sortingOrder,
                        RoomLabelSortingOrder + 1);
                }
            }
        }

        public static void ApplyWallFace(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var currentSize = renderer.bounds.size;
            if (currentSize.x <= Mathf.Epsilon ||
                currentSize.y <= Mathf.Epsilon)
            {
                return;
            }

            var topY = renderer.bounds.max.y;
            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                var rendererSize = renderer.size;
                renderer.size = new Vector2(
                    rendererSize.x,
                    rendererSize.y * WallFaceHeight / currentSize.y);
            }
            else
            {
                var localScale = renderer.transform.localScale;
                renderer.transform.localScale = new Vector3(
                    localScale.x,
                    localScale.y * WallFaceHeight / currentSize.y,
                    localScale.z);
            }

            var current = renderer.transform.position;
            renderer.transform.position = new Vector3(
                current.x,
                topY - WallFaceHeight * 0.5f,
                current.z);
        }

        private static void HideRepeatedGenericWallFixtures(
            IReadOnlyList<EnvironmentPropSlot> propSlots)
        {
            for (var index = 0; index < propSlots.Count; index++)
            {
                var slot = propSlots[index];
                if (slot.MountKind != EnvironmentPropMountKind.WallMounted)
                {
                    continue;
                }

                if (string.Equals(
                        slot.AssetKey,
                        "SM_WallMonitor",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        slot.AssetKey,
                        "SM_FireExtinguisher",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        slot.AssetKey,
                        "SM_EmergencyPhone",
                        StringComparison.Ordinal))
                {
                    slot.SetPlaceholderVisible(false);
                }
            }
        }

        private static void CreateRoomWallDecorations(
            Scene scene,
            IReadOnlyList<GameObject> roots,
            IReadOnlyList<SpriteRenderer> roomFloors,
            IReadOnlyList<SpriteRenderer> wallFaces)
        {
            if (roomFloors.Count == 0 || wallFaces.Count == 0 ||
                HasDecorationRoot(roots))
            {
                return;
            }

            var root = new GameObject(DecorationRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            for (var roomIndex = 0;
                 roomIndex < roomFloors.Count;
                 roomIndex++)
            {
                var roomFloor = roomFloors[roomIndex];
                if (roomFloor == null)
                {
                    continue;
                }

                var roomId = roomFloor.gameObject.name.Substring(
                    "Room_".Length);
                var roomBounds = roomFloor.bounds;
                var segments = CollectNorthWallSegments(
                    roomBounds,
                    wallFaces);
                if (segments.Count == 0)
                {
                    continue;
                }

                var roomRoot = new GameObject($"WallDecor_{roomId}");
                roomRoot.transform.SetParent(root.transform, false);
                var theme = GetRoomWallTheme(roomId);
                for (var segmentIndex = 0;
                     segmentIndex < segments.Count;
                     segmentIndex++)
                {
                    CreateWallpaperSegment(
                        roomRoot.transform,
                        roomId,
                        segments[segmentIndex],
                        theme,
                        segmentIndex);
                }

                var fixtureSegment = FindWidestSegment(segments);
                CreateWallFixtures(
                    roomRoot.transform,
                    roomId,
                    fixtureSegment,
                    theme);
            }
        }

        private static List<WallSegment> CollectNorthWallSegments(
            Bounds roomBounds,
            IReadOnlyList<SpriteRenderer> wallFaces)
        {
            var segments = new List<WallSegment>();
            for (var index = 0; index < wallFaces.Count; index++)
            {
                var wallFace = wallFaces[index];
                if (wallFace == null ||
                    Mathf.Abs(
                        wallFace.bounds.max.y - roomBounds.max.y) >
                    WallTopTolerance)
                {
                    continue;
                }

                var minX = Mathf.Max(
                    roomBounds.min.x,
                    wallFace.bounds.min.x);
                var maxX = Mathf.Min(
                    roomBounds.max.x,
                    wallFace.bounds.max.x);
                if (maxX - minX < MinimumWallSegmentWidth)
                {
                    continue;
                }

                segments.Add(new WallSegment(
                    wallFace,
                    minX,
                    maxX,
                    roomBounds.max.y));
            }

            return segments;
        }

        private static WallSegment FindWidestSegment(
            IReadOnlyList<WallSegment> segments)
        {
            var widest = segments[0];
            for (var index = 1; index < segments.Count; index++)
            {
                if (segments[index].Width > widest.Width)
                {
                    widest = segments[index];
                }
            }

            return widest;
        }

        private static void CreateWallpaperSegment(
            Transform parent,
            string roomId,
            WallSegment segment,
            RoomWallTheme theme,
            int segmentIndex)
        {
            var wallOrder = segment.Source.sortingOrder;
            var wallpaper = theme.Wallpaper;
            wallpaper.a = 0.32f;
            CreateRectangle(
                $"Wallpaper_{roomId}_{segmentIndex:00}",
                parent,
                new Vector2(
                    segment.CenterX,
                    segment.TopY - WallFaceHeight * 0.5f),
                new Vector2(segment.Width, WallFaceHeight - 0.08f),
                wallpaper,
                segment.Source,
                wallOrder + WallpaperSortingOffset);

            var accent = theme.Accent;
            accent.a = 0.72f;
            CreateRectangle(
                $"AccentBand_{roomId}_{segmentIndex:00}",
                parent,
                new Vector2(segment.CenterX, segment.TopY - 0.38f),
                new Vector2(segment.Width, 0.1f),
                accent,
                segment.Source,
                wallOrder + WallpaperSortingOffset + 1);

            var lowerBand = new Color(
                theme.Accent.r * 0.48f,
                theme.Accent.g * 0.48f,
                theme.Accent.b * 0.48f,
                0.56f);
            CreateRectangle(
                $"LowerBand_{roomId}_{segmentIndex:00}",
                parent,
                new Vector2(
                    segment.CenterX,
                    segment.TopY - WallFaceHeight + 0.24f),
                new Vector2(segment.Width, 0.07f),
                lowerBand,
                segment.Source,
                wallOrder + WallpaperSortingOffset + 1);

            CreateWallpaperPattern(
                parent,
                roomId,
                segment,
                theme,
                segmentIndex,
                wallOrder + WallpaperSortingOffset + 1);
        }

        private static void CreateWallpaperPattern(
            Transform parent,
            string roomId,
            WallSegment segment,
            RoomWallTheme theme,
            int segmentIndex,
            int sortingOrder)
        {
            var patternColor = theme.Accent;
            patternColor.a = theme.Pattern == WallPattern.Hazard
                ? 0.46f
                : 0.2f;

            switch (theme.Pattern)
            {
                case WallPattern.Panel:
                    for (var x = segment.MinX + 1.8f;
                         x < segment.MaxX - 0.5f;
                         x += 2.2f)
                    {
                        CreateRectangle(
                            $"PanelSeam_{roomId}_{segmentIndex:00}",
                            parent,
                            new Vector2(
                                x,
                                segment.TopY - WallFaceHeight * 0.58f),
                            new Vector2(0.035f, WallFaceHeight - 0.68f),
                            patternColor,
                            segment.Source,
                            sortingOrder);
                    }
                    break;
                case WallPattern.Stripe:
                    CreateRectangle(
                        $"Stripe_{roomId}_{segmentIndex:00}",
                        parent,
                        new Vector2(
                            segment.CenterX,
                            segment.TopY - WallFaceHeight * 0.61f),
                        new Vector2(segment.Width, 0.045f),
                        patternColor,
                        segment.Source,
                        sortingOrder);
                    CreateRectangle(
                        $"StripeLower_{roomId}_{segmentIndex:00}",
                        parent,
                        new Vector2(
                            segment.CenterX,
                            segment.TopY - WallFaceHeight * 0.77f),
                        new Vector2(segment.Width, 0.035f),
                        patternColor,
                        segment.Source,
                        sortingOrder);
                    break;
                case WallPattern.Hazard:
                    for (var x = segment.MinX + 0.65f;
                         x < segment.MaxX - 0.35f;
                         x += 1.15f)
                    {
                        var tick = CreateRectangle(
                            $"HazardTick_{roomId}_{segmentIndex:00}",
                            parent,
                            new Vector2(x, segment.TopY - 0.67f),
                            new Vector2(0.38f, 0.08f),
                            patternColor,
                            segment.Source,
                            sortingOrder);
                        tick.transform.rotation = Quaternion.Euler(
                            0f,
                            0f,
                            -35f);
                    }
                    break;
            }
        }

        private static void CreateWallFixtures(
            Transform parent,
            string roomId,
            WallSegment segment,
            RoomWallTheme theme)
        {
            if (segment.Width < 1.8f)
            {
                return;
            }

            var fixtureY = segment.TopY - 1.33f;
            var margin = 0.82f;
            var primaryX = segment.Width >= 4.2f
                ? segment.MinX + margin
                : segment.CenterX;
            CreateSystemPanel(
                parent,
                roomId,
                new Vector2(primaryX, fixtureY),
                theme,
                segment.Source);

            if (segment.Width < 4.2f)
            {
                return;
            }

            var secondaryX = segment.MaxX - margin;
            switch (theme.SecondaryFixture)
            {
                case WallFixtureKind.Cctv:
                    CreateCctv(
                        parent,
                        roomId,
                        new Vector2(secondaryX, fixtureY + 0.04f),
                        theme,
                        segment.Source);
                    break;
                case WallFixtureKind.DigitalClock:
                    CreateDigitalClock(
                        parent,
                        roomId,
                        new Vector2(secondaryX, fixtureY),
                        theme,
                        segment.Source);
                    break;
                case WallFixtureKind.SystemGauge:
                    CreateSystemGauge(
                        parent,
                        roomId,
                        new Vector2(secondaryX, fixtureY),
                        theme,
                        segment.Source);
                    break;
            }
        }

        private static void CreateSystemPanel(
            Transform parent,
            string roomId,
            Vector2 position,
            RoomWallTheme theme,
            SpriteRenderer source)
        {
            var order = source.sortingOrder + FixtureSortingOffset;
            CreateRectangle(
                $"SystemPanelFrame_{roomId}",
                parent,
                position,
                new Vector2(1.26f, 0.68f),
                new Color(0.035f, 0.055f, 0.07f, 1f),
                source,
                order);
            CreateRectangle(
                $"SystemPanelScreen_{roomId}",
                parent,
                position + new Vector2(0f, 0.04f),
                new Vector2(1.02f, 0.39f),
                new Color(
                    theme.Accent.r * 0.34f,
                    theme.Accent.g * 0.34f,
                    theme.Accent.b * 0.34f,
                    1f),
                source,
                order + 1);

            var traceColor = theme.Accent;
            traceColor.a = 0.9f;
            CreateRectangle(
                $"SystemPanelTraceA_{roomId}",
                parent,
                position + new Vector2(-0.17f, 0.11f),
                new Vector2(0.53f, 0.035f),
                traceColor,
                source,
                order + 2);
            CreateRectangle(
                $"SystemPanelTraceB_{roomId}",
                parent,
                position + new Vector2(0.19f, -0.03f),
                new Vector2(0.42f, 0.035f),
                traceColor,
                source,
                order + 2);
            CreateRectangle(
                $"SystemPanelStatus_{roomId}",
                parent,
                position + new Vector2(0.46f, -0.25f),
                new Vector2(0.09f, 0.07f),
                new Color(0.33f, 1f, 0.56f, 1f),
                source,
                order + 2);
        }

        private static void CreateCctv(
            Transform parent,
            string roomId,
            Vector2 position,
            RoomWallTheme theme,
            SpriteRenderer source)
        {
            var order = source.sortingOrder + FixtureSortingOffset;
            var arm = CreateRectangle(
                $"CctvArm_{roomId}",
                parent,
                position + new Vector2(0.2f, -0.23f),
                new Vector2(0.09f, 0.43f),
                new Color(0.16f, 0.19f, 0.22f, 1f),
                source,
                order);
            arm.transform.rotation = Quaternion.Euler(0f, 0f, -28f);
            CreateRectangle(
                $"CctvBody_{roomId}",
                parent,
                position,
                new Vector2(0.76f, 0.3f),
                new Color(0.54f, 0.6f, 0.63f, 1f),
                source,
                order + 1);
            CreateRectangle(
                $"CctvLens_{roomId}",
                parent,
                position + new Vector2(-0.3f, 0f),
                new Vector2(0.16f, 0.19f),
                new Color(
                    theme.Accent.r * 0.55f,
                    theme.Accent.g * 0.55f,
                    theme.Accent.b * 0.55f,
                    1f),
                source,
                order + 2);
            CreateRectangle(
                $"CctvStatus_{roomId}",
                parent,
                position + new Vector2(0.25f, 0.03f),
                new Vector2(0.08f, 0.08f),
                new Color(1f, 0.18f, 0.14f, 1f),
                source,
                order + 2);
        }

        private static void CreateDigitalClock(
            Transform parent,
            string roomId,
            Vector2 position,
            RoomWallTheme theme,
            SpriteRenderer source)
        {
            var order = source.sortingOrder + FixtureSortingOffset;
            CreateRectangle(
                $"DigitalClockFrame_{roomId}",
                parent,
                position,
                new Vector2(1.05f, 0.48f),
                new Color(0.025f, 0.035f, 0.045f, 1f),
                source,
                order);
            CreateRectangle(
                $"DigitalClockScreen_{roomId}",
                parent,
                position,
                new Vector2(0.84f, 0.28f),
                new Color(0.025f, 0.1f, 0.12f, 1f),
                source,
                order + 1);

            var digitColor = theme.Accent;
            digitColor.a = 0.95f;
            var digitOffsets = new[] { -0.27f, -0.1f, 0.1f, 0.27f };
            for (var index = 0; index < digitOffsets.Length; index++)
            {
                CreateRectangle(
                    $"DigitalClockDigit_{roomId}_{index}",
                    parent,
                    position + new Vector2(digitOffsets[index], 0f),
                    new Vector2(0.07f, 0.18f),
                    digitColor,
                    source,
                    order + 2);
            }

            CreateRectangle(
                $"DigitalClockColonA_{roomId}",
                parent,
                position + new Vector2(0f, 0.06f),
                new Vector2(0.035f, 0.035f),
                digitColor,
                source,
                order + 2);
            CreateRectangle(
                $"DigitalClockColonB_{roomId}",
                parent,
                position + new Vector2(0f, -0.06f),
                new Vector2(0.035f, 0.035f),
                digitColor,
                source,
                order + 2);
        }

        private static void CreateSystemGauge(
            Transform parent,
            string roomId,
            Vector2 position,
            RoomWallTheme theme,
            SpriteRenderer source)
        {
            var order = source.sortingOrder + FixtureSortingOffset;
            CreateRectangle(
                $"SystemGaugeFrame_{roomId}",
                parent,
                position,
                new Vector2(0.68f, 0.68f),
                new Color(0.05f, 0.065f, 0.075f, 1f),
                source,
                order);
            CreateRectangle(
                $"SystemGaugeScreen_{roomId}",
                parent,
                position,
                new Vector2(0.45f, 0.44f),
                new Color(0.07f, 0.12f, 0.13f, 1f),
                source,
                order + 1);

            var needleColor = theme.Accent;
            needleColor.a = 0.95f;
            var needle = CreateRectangle(
                $"SystemGaugeNeedle_{roomId}",
                parent,
                position + new Vector2(0.02f, -0.02f),
                new Vector2(0.04f, 0.31f),
                needleColor,
                source,
                order + 2);
            needle.transform.rotation = Quaternion.Euler(0f, 0f, -34f);
            CreateRectangle(
                $"SystemGaugeStatus_{roomId}",
                parent,
                position + new Vector2(0.23f, -0.24f),
                new Vector2(0.08f, 0.08f),
                new Color(1f, 0.68f, 0.12f, 1f),
                source,
                order + 2);
        }

        private static SpriteRenderer CreateRectangle(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            SpriteRenderer source,
            int sortingOrder)
        {
            var rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.position = new Vector3(
                position.x,
                position.y,
                source.transform.position.z - 0.01f);
            rectangle.transform.localScale = new Vector3(
                size.x,
                size.y,
                1f);
            var renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = GetUnitSprite();
            renderer.color = color;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            if (source.sharedMaterial != null)
            {
                renderer.sharedMaterial = source.sharedMaterial;
            }

            return renderer;
        }

        private static Sprite GetUnitSprite()
        {
            if (_unitSprite != null)
            {
                return _unitSprite;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "T_RuntimeRoomWallPixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            });
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _unitSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            _unitSprite.name = "S_RuntimeRoomWallPixel";
            _unitSprite.hideFlags = HideFlags.HideAndDontSave;
            return _unitSprite;
        }

        private static bool HasDecorationRoot(
            IReadOnlyList<GameObject> roots)
        {
            for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                var transforms = roots[rootIndex]
                    .GetComponentsInChildren<Transform>(true);
                for (var index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index] != null &&
                        string.Equals(
                            transforms[index].gameObject.name,
                            DecorationRootName,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static RoomWallTheme GetRoomWallTheme(string roomId)
        {
            switch (roomId)
            {
                case "VaccineA":
                    return new RoomWallTheme(
                        new Color(0.22f, 0.46f, 0.49f),
                        new Color(0.32f, 0.95f, 0.88f),
                        WallPattern.Panel,
                        WallFixtureKind.DigitalClock);
                case "VaccineB":
                    return new RoomWallTheme(
                        new Color(0.26f, 0.48f, 0.44f),
                        new Color(0.44f, 1f, 0.73f),
                        WallPattern.Stripe,
                        WallFixtureKind.DigitalClock);
                case "LabA":
                    return new RoomWallTheme(
                        new Color(0.2f, 0.4f, 0.46f),
                        new Color(0.25f, 0.82f, 1f),
                        WallPattern.Panel,
                        WallFixtureKind.Cctv);
                case "LabB":
                    return new RoomWallTheme(
                        new Color(0.3f, 0.26f, 0.46f),
                        new Color(0.72f, 0.52f, 1f),
                        WallPattern.Stripe,
                        WallFixtureKind.Cctv);
                case "QuarantineA":
                    return new RoomWallTheme(
                        new Color(0.48f, 0.25f, 0.24f),
                        new Color(1f, 0.38f, 0.25f),
                        WallPattern.Hazard,
                        WallFixtureKind.Cctv);
                case "QuarantineB":
                    return new RoomWallTheme(
                        new Color(0.43f, 0.24f, 0.34f),
                        new Color(1f, 0.44f, 0.58f),
                        WallPattern.Hazard,
                        WallFixtureKind.Cctv);
                case "Storage":
                    return new RoomWallTheme(
                        new Color(0.24f, 0.34f, 0.43f),
                        new Color(0.5f, 0.82f, 1f),
                        WallPattern.Stripe,
                        WallFixtureKind.SystemGauge);
                case "Security":
                    return new RoomWallTheme(
                        new Color(0.14f, 0.34f, 0.46f),
                        new Color(0.12f, 0.88f, 1f),
                        WallPattern.Panel,
                        WallFixtureKind.Cctv);
                case "Power":
                    return new RoomWallTheme(
                        new Color(0.48f, 0.35f, 0.17f),
                        new Color(1f, 0.68f, 0.12f),
                        WallPattern.Hazard,
                        WallFixtureKind.SystemGauge);
                case "Ward":
                    return new RoomWallTheme(
                        new Color(0.22f, 0.43f, 0.36f),
                        new Color(0.45f, 1f, 0.72f),
                        WallPattern.Stripe,
                        WallFixtureKind.DigitalClock);
                default:
                    return new RoomWallTheme(
                        new Color(0.22f, 0.34f, 0.39f),
                        new Color(0.3f, 0.82f, 0.92f),
                        WallPattern.Panel,
                        WallFixtureKind.SystemGauge);
            }
        }
    }
}
