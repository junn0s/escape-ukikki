using System;
using System.Collections.Generic;
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
