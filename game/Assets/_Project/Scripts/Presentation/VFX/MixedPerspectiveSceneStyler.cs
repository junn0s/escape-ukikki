using System;
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
        /// 카메라를 기울이지 않고도 상단 벽을 정면에서 보는 것처럼 만든다.
        /// </summary>
        public const float WallFaceHeight = 1.8f;

        public static void ApplyTo(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

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
                    if (renderer != null &&
                        renderer.gameObject.name.StartsWith(
                            "WallFace_",
                            StringComparison.Ordinal))
                    {
                        ApplyWallFace(renderer);
                    }
                }

                var propSlots = root
                    .GetComponentsInChildren<EnvironmentPropSlot>(true);
                for (var slotIndex = 0;
                     slotIndex < propSlots.Length;
                     slotIndex++)
                {
                    propSlots[slotIndex]?.ApplyMixedPerspectivePresentation();
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
    }
}
