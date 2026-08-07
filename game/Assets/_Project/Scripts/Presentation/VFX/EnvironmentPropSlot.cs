using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Interaction;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public enum EnvironmentPropMountKind : byte
    {
        FloorStanding = 0,
        WallMounted = 1,
        FloorDecal = 2,
        Overhead = 3,
        DoorAssembly = 4
    }

    /// <summary>
    /// 회색상자 프롭을 최종 스프라이트나 프리팹으로 교체할 때 사용하는 배치 메타데이터다.
    /// 교체 에셋은 이 오브젝트의 ReplacementAnchor에 붙이고, 배치된 footprint와
    /// blocking collider를 그대로 기준으로 삼는다.
    /// </summary>
    public sealed class EnvironmentPropSlot : MonoBehaviour
    {
        public const float DetailedPropMinimumExtent = 0.5f;
        public const float VisualDepthScale = 0.55f;
        public const float VisualBaseHeight = 0.85f;
        public const float ShadowHorizontalOffset = 0.16f;
        public const float ShadowGroundOffset = 0.04f;
        public const float ShadowWidthPadding = 0.24f;
        public const float ShadowDepthScale = 0.36f;
        public const float ShadowMinimumDepth = 0.28f;
        /// <summary>좌우 벽 문에서 보이는 측면 두께다.</summary>
        public const float DoorPanelDepth = 0.86f;

        /// <summary>
        /// 위·아래로 통과하는 문의 정면 높이다. 바닥 위 선이 아니라 상단 벽
        /// 입면 안의 직사각형 문으로 읽히게 한다.
        /// </summary>
        public const float DoorFrontFaceHeight = 1.65f;
        public const float DoorFrontFrameHeight = 1.95f;
        public const float DoorFrontTopOffset = 0.16f;
        public const float DoorFrameThickness = 0.42f;
        public const float DoorFrameSpan = 1.58f;

        [SerializeField] private string _roomId;
        [SerializeField] private string _assetKey;
        [SerializeField] private Vector2 _footprint;
        [SerializeField] private bool _isObstacle;
        [SerializeField] private EnvironmentPropMountKind _mountKind;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private Transform _replacementAnchor;
        [SerializeField] private SpriteRenderer _placeholderRenderer;
        [SerializeField] private SpriteRenderer[] _placeholderRenderers =
            Array.Empty<SpriteRenderer>();

        public string RoomId => _roomId;
        public string AssetKey => _assetKey;
        public Vector2 Footprint => _footprint;
        public bool IsObstacle => _isObstacle;
        public EnvironmentPropMountKind MountKind => _mountKind;
        public int SortingOrder => _sortingOrder;
        public Transform ReplacementAnchor => _replacementAnchor != null
            ? _replacementAnchor
            : transform;
        public SpriteRenderer PlaceholderRenderer =>
            _placeholderRenderer;
        public IReadOnlyList<SpriteRenderer> PlaceholderRenderers =>
            _placeholderRenderers;

        public static float GetMixedPerspectiveVisualHeight(
            Vector2 footprint)
        {
            return footprint.y * VisualDepthScale + VisualBaseHeight;
        }

        public void Configure(
            string roomId,
            string assetKey,
            Vector2 footprint,
            bool isObstacle,
            SpriteRenderer placeholderRenderer)
        {
            ConfigureDetailed(
                roomId,
                assetKey,
                footprint,
                isObstacle,
                isObstacle
                    ? EnvironmentPropMountKind.FloorStanding
                    : EnvironmentPropMountKind.FloorDecal,
                placeholderRenderer != null
                    ? placeholderRenderer.sortingOrder
                    : 0,
                transform,
                placeholderRenderer,
                placeholderRenderer != null
                    ? new[] { placeholderRenderer }
                    : Array.Empty<SpriteRenderer>());
        }

        public void ConfigureDetailed(
            string roomId,
            string assetKey,
            Vector2 footprint,
            bool isObstacle,
            EnvironmentPropMountKind mountKind,
            int sortingOrder,
            Transform replacementAnchor,
            SpriteRenderer placeholderRenderer,
            SpriteRenderer[] placeholderRenderers)
        {
            _roomId = roomId;
            _assetKey = assetKey;
            _footprint = footprint;
            _isObstacle = isObstacle;
            _mountKind = mountKind;
            _sortingOrder = sortingOrder;
            _replacementAnchor = replacementAnchor != null
                ? replacementAnchor
                : transform;
            _placeholderRenderer = placeholderRenderer;
            _placeholderRenderers = placeholderRenderers ??
                Array.Empty<SpriteRenderer>();
            ApplyMixedPerspectivePresentation();
        }

        private void OnEnable()
        {
            ApplyMixedPerspectivePresentation();
        }

        /// <summary>
        /// 배치 footprint는 충돌·상호작용용 바닥 면적으로 유지하고, 화면에 보이는
        /// 몸체만 발 기준으로 위로 세운다. 현재 씬과 빌더 재생성 결과가 같은 규칙을
        /// 쓰도록 런타임에도 멱등적으로 적용한다.
        /// </summary>
        public void ApplyMixedPerspectivePresentation()
        {
            if (_mountKind == EnvironmentPropMountKind.DoorAssembly)
            {
                ApplyDoorEmphasis();
                return;
            }

            if (_mountKind != EnvironmentPropMountKind.FloorStanding ||
                _placeholderRenderer == null ||
                Mathf.Min(_footprint.x, _footprint.y) <
                DetailedPropMinimumExtent)
            {
                return;
            }

            var groundY = transform.position.y - _footprint.y * 0.5f;
            var visualHeight = GetMixedPerspectiveVisualHeight(_footprint);
            var visualPosition = new Vector2(
                transform.position.x,
                groundY + visualHeight * 0.5f);
            SetRendererWorldSize(
                _placeholderRenderer,
                new Vector2(_footprint.x, visualHeight));
            SetRendererWorldPosition(_placeholderRenderer, visualPosition);

            var visualOrder = YSortedRenderer.GetSortingOrder(groundY);
            _placeholderRenderer.sortingOrder = visualOrder;
            _sortingOrder = visualOrder;

            for (var index = 0;
                 index < _placeholderRenderers.Length;
                 index++)
            {
                var renderer = _placeholderRenderers[index];
                if (renderer == null || renderer == _placeholderRenderer)
                {
                    continue;
                }

                switch (renderer.gameObject.name)
                {
                    case "PlaceholderShadow":
                        SetRendererWorldSize(
                            renderer,
                            new Vector2(
                                _footprint.x + ShadowWidthPadding,
                                Mathf.Max(
                                    ShadowMinimumDepth,
                                    _footprint.y * ShadowDepthScale)));
                        SetRendererWorldPosition(
                            renderer,
                            new Vector2(
                                transform.position.x +
                                ShadowHorizontalOffset,
                                groundY + ShadowGroundOffset));
                        renderer.sortingOrder = visualOrder - 1;
                        var shadowColor = renderer.color;
                        shadowColor.a = 0.26f;
                        renderer.color = shadowColor;
                        break;
                    case "PlaceholderCategoryIcon":
                        SetRendererWorldPosition(renderer, visualPosition);
                        renderer.sortingOrder = visualOrder + 1;
                        break;
                    case "PlaceholderStatusIndicator":
                        SetRendererWorldPosition(
                            renderer,
                            visualPosition + new Vector2(
                                _footprint.x * 0.32f,
                                visualHeight * 0.28f));
                        renderer.sortingOrder = visualOrder + 2;
                        break;
                }
            }
        }

        private void ApplyDoorEmphasis()
        {
            if (_placeholderRenderers == null ||
                _placeholderRenderers.Length == 0)
            {
                return;
            }

            var isHorizontalWall = _footprint.x >= _footprint.y;
            var faceCenterY = transform.position.y +
                              DoorFrontTopOffset -
                              DoorFrontFaceHeight * 0.5f;
            for (var index = 0;
                 index < _placeholderRenderers.Length;
                 index++)
            {
                var renderer = _placeholderRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                var currentSize = renderer.bounds.size;
                if (renderer.gameObject.name.StartsWith(
                        "Panel_",
                        StringComparison.Ordinal))
                {
                    var targetSize = isHorizontalWall
                        ? new Vector2(
                            currentSize.x,
                            DoorFrontFaceHeight)
                        : new Vector2(DoorPanelDepth, currentSize.y);
                    SetRendererWorldSize(renderer, targetSize);
                    if (isHorizontalWall)
                    {
                        SetRendererWorldPosition(
                            renderer,
                            new Vector2(
                                renderer.transform.position.x,
                                faceCenterY));
                    }
                }
                else if (renderer.gameObject.name.StartsWith(
                             "Frame_",
                             StringComparison.Ordinal))
                {
                    var targetSize = isHorizontalWall
                        ? new Vector2(
                            DoorFrameThickness,
                            DoorFrontFrameHeight)
                        : new Vector2(
                            DoorFrameSpan,
                            DoorFrameThickness);
                    SetRendererWorldSize(renderer, targetSize);
                    if (isHorizontalWall)
                    {
                        SetRendererWorldPosition(
                            renderer,
                            new Vector2(
                                renderer.transform.position.x,
                                faceCenterY));
                    }
                }
                else if (isHorizontalWall &&
                         renderer.gameObject.name.StartsWith(
                             "Status_",
                             StringComparison.Ordinal))
                {
                    SetRendererWorldPosition(
                        renderer,
                        new Vector2(
                            renderer.transform.position.x,
                            faceCenterY));
                }
            }

            if (isHorizontalWall &&
                TryGetComponent<AutomaticDoorMotor>(out var motor))
            {
                motor.RefreshClosedPositions();
            }
        }

        private static void SetRendererWorldPosition(
            SpriteRenderer renderer,
            Vector2 worldPosition)
        {
            var current = renderer.transform.position;
            renderer.transform.position = new Vector3(
                worldPosition.x,
                worldPosition.y,
                current.z);
        }

        private static void SetRendererWorldSize(
            SpriteRenderer renderer,
            Vector2 worldSize)
        {
            var currentSize = renderer.bounds.size;
            if (currentSize.x <= Mathf.Epsilon ||
                currentSize.y <= Mathf.Epsilon)
            {
                return;
            }

            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                var rendererSize = renderer.size;
                renderer.size = new Vector2(
                    rendererSize.x * worldSize.x / currentSize.x,
                    rendererSize.y * worldSize.y / currentSize.y);
                return;
            }

            var localScale = renderer.transform.localScale;
            renderer.transform.localScale = new Vector3(
                localScale.x * worldSize.x / currentSize.x,
                localScale.y * worldSize.y / currentSize.y,
                localScale.z);
        }

        public void SetPlaceholderVisible(bool isVisible)
        {
            foreach (var renderer in _placeholderRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = isVisible;
                }
            }
        }
    }
}
