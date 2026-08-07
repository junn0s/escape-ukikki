using MonkeyLab.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class MixedPerspectivePresentationTests
    {
        [Test]
        public void WallFace_ExpandsDownwardWithoutMovingTopEdge()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);
            var wall = new GameObject("WallFace_Test");
            var renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            wall.transform.position = new Vector3(2f, 4f, 0f);
            wall.transform.localScale = new Vector3(5f, 0.95f, 1f);
            var topY = renderer.bounds.max.y;

            MixedPerspectiveSceneStyler.ApplyWallFace(renderer);

            Assert.That(
                renderer.bounds.size.y,
                Is.EqualTo(MixedPerspectiveSceneStyler.WallFaceHeight)
                    .Within(0.001f));
            Assert.That(renderer.bounds.max.y, Is.EqualTo(topY).Within(0.001f));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void FloorProp_UsesFootPositionForHeightShadowAndSorting()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);
            var root = new GameObject("PROP_Test");
            root.transform.position = new Vector3(3f, 2f, 0f);

            var visualObject = new GameObject("PlaceholderVisual");
            visualObject.transform.SetParent(root.transform);
            visualObject.transform.position = root.transform.position;
            visualObject.transform.localScale = new Vector3(2f, 2f, 1f);
            var visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;

            var shadowObject = new GameObject("PlaceholderShadow");
            shadowObject.transform.SetParent(root.transform);
            shadowObject.transform.position = root.transform.position;
            shadowObject.transform.localScale = new Vector3(2f, 2f, 1f);
            var shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = sprite;
            shadow.color = new Color(0f, 0f, 0f, 0.5f);

            var footprint = new Vector2(2f, 2f);
            var slot = root.AddComponent<EnvironmentPropSlot>();
            slot.ConfigureDetailed(
                "LabA",
                "SM_Test",
                footprint,
                isObstacle: true,
                EnvironmentPropMountKind.FloorStanding,
                sortingOrder: 8,
                root.transform,
                visual,
                new[] { shadow, visual });

            var groundY = 1f;
            var expectedHeight =
                EnvironmentPropSlot.GetMixedPerspectiveVisualHeight(footprint);
            Assert.That(
                visual.bounds.size.y,
                Is.EqualTo(expectedHeight).Within(0.001f));
            Assert.That(
                visual.bounds.min.y,
                Is.EqualTo(groundY).Within(0.001f));
            Assert.That(
                visual.sortingOrder,
                Is.EqualTo(YSortedRenderer.GetSortingOrder(groundY)));
            Assert.That(
                shadow.bounds.size.x,
                Is.EqualTo(
                    footprint.x +
                    EnvironmentPropSlot.ShadowWidthPadding).Within(0.001f));
            Assert.That(
                shadow.bounds.size.y,
                Is.EqualTo(
                    footprint.y *
                    EnvironmentPropSlot.ShadowDepthScale).Within(0.001f));
            Assert.That(
                shadow.transform.position.x,
                Is.EqualTo(
                    root.transform.position.x +
                    EnvironmentPropSlot.ShadowHorizontalOffset).Within(0.001f));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void HorizontalDoor_IsFrontFacingRectangleAnchoredToWallTop()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);
            var root = new GameObject("Door_Test");
            root.transform.position = new Vector3(2f, 5f, 0f);
            var panelA = CreateRenderer("Panel_A", root.transform, sprite);
            var panelB = CreateRenderer("Panel_B", root.transform, sprite);
            var frameA = CreateRenderer("Frame_A", root.transform, sprite);
            var frameB = CreateRenderer("Frame_B", root.transform, sprite);
            var statusA = CreateRenderer("Status_A", root.transform, sprite);
            var statusB = CreateRenderer("Status_B", root.transform, sprite);
            var slot = root.AddComponent<EnvironmentPropSlot>();

            slot.ConfigureDetailed(
                "LabA",
                "P_AutomaticDoor",
                new Vector2(4.5f, 0.55f),
                isObstacle: true,
                EnvironmentPropMountKind.DoorAssembly,
                sortingOrder: 35,
                root.transform,
                panelA,
                new[] { panelA, panelB, frameA, frameB, statusA, statusB });

            Assert.That(
                panelA.bounds.size.y,
                Is.EqualTo(EnvironmentPropSlot.DoorFrontFaceHeight)
                    .Within(0.001f));
            Assert.That(
                panelA.bounds.max.y,
                Is.EqualTo(
                    root.transform.position.y +
                    EnvironmentPropSlot.DoorFrontTopOffset).Within(0.001f));
            Assert.That(
                frameA.bounds.size.y,
                Is.EqualTo(EnvironmentPropSlot.DoorFrontFrameHeight)
                    .Within(0.001f));
            Assert.That(
                frameA.transform.position.y,
                Is.EqualTo(panelA.transform.position.y).Within(0.001f));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void YSort_PreservesChildLayerOffsets()
        {
            var root = new GameObject("YSort_Test");
            root.transform.position = new Vector3(0f, 3f, 0f);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root.transform);
            body.sortingOrder = 41;
            var eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
            eye.transform.SetParent(root.transform);
            eye.sortingOrder = 43;

            var ySort = root.AddComponent<YSortedRenderer>();
            ySort.Configure(new[] { body, eye }, groundOffsetY: -0.5f);

            var expectedOrder = YSortedRenderer.GetSortingOrder(2.5f);
            Assert.That(body.sortingOrder, Is.EqualTo(expectedOrder));
            Assert.That(eye.sortingOrder, Is.EqualTo(expectedOrder + 2));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void FlashlightCone_StopsBeforeStaticWallCollider()
        {
            var player = new GameObject("FlashlightPlayer_Test");
            var controller = player.AddComponent<FlashlightController>();
            var wall = new GameObject("FlashlightWall_Test");
            wall.transform.position = new Vector3(0f, 2f, 0f);
            var wallCollider = wall.AddComponent<BoxCollider2D>();
            wallCollider.size = new Vector2(4f, 0.2f);
            Physics2D.SyncTransforms();

            var resolveMethod = typeof(FlashlightController).GetMethod(
                "ResolveVisibleDistance",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);
            var visibleDistance = (float)resolveMethod.Invoke(
                controller,
                new object[] { Vector2.zero, Vector2.up });

            // 벽의 아래 표면 1.9m보다 살짝 앞에서 원뿔이 끝나야 한다.
            Assert.That(visibleDistance, Is.GreaterThan(1.84f));
            Assert.That(visibleDistance, Is.LessThan(1.90f));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(player);
        }

        private static SpriteRenderer CreateRenderer(
            string name,
            Transform parent,
            Sprite sprite)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.position = parent.position;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }
    }
}
