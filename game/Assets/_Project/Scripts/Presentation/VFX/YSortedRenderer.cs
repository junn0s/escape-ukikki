using System;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 화면에서 아래에 있는 것이 앞에 그려지도록 정렬 순서를 Y 좌표로 정한다.
    ///
    /// 프롭을 눕히지 않고 세워 그리면 서로 겹치기 시작하는데, 정렬이 고정값이면
    /// 캐릭터가 아래쪽 프롭 뒤로 들어가는 등 앞뒤가 뒤집힌다. 발이 닿는 지점의
    /// Y를 기준으로 삼아야 실제로 그 앞에 선 것처럼 보인다.
    /// </summary>
    public sealed class YSortedRenderer : MonoBehaviour
    {
        /// <summary>
        /// Y 정렬 대역의 기준값. 바닥(0)·벽 정면(2)보다 확실히 위에 오도록
        /// 맵 Y 범위(-40~36) 전체가 양수로 들어가는 값을 쓴다.
        /// </summary>
        public const int SortingBase = 300;

        /// <summary>1m 차이를 몇 단계로 볼지. 클수록 얕은 깊이 차도 구분된다.</summary>
        public const float SortingScalePerMeter = 4f;

        [SerializeField] private SpriteRenderer[] _renderers =
            Array.Empty<SpriteRenderer>();

        [SerializeField] private int[] _sortingOffsets =
            Array.Empty<int>();

        /// <summary>발이 닿는 지점이 루트 원점과 다를 때 보정한다.</summary>
        [SerializeField] private float _groundOffsetY;

        private int _appliedOrder = int.MinValue;

        public static int GetSortingOrder(float groundY)
        {
            return SortingBase -
                   Mathf.RoundToInt(groundY * SortingScalePerMeter);
        }

        public void Configure(
            SpriteRenderer[] renderers,
            float groundOffsetY = 0f)
        {
            _renderers = renderers ?? Array.Empty<SpriteRenderer>();
            CaptureSortingOffsets();
            _groundOffsetY = groundOffsetY;
            _appliedOrder = int.MinValue;
            Apply();
        }

        private void OnEnable()
        {
            if (_sortingOffsets == null ||
                _sortingOffsets.Length != _renderers.Length)
            {
                CaptureSortingOffsets();
            }

            _appliedOrder = int.MinValue;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            var order = GetSortingOrder(
                transform.position.y + _groundOffsetY);
            if (order == _appliedOrder)
            {
                return;
            }

            _appliedOrder = order;
            for (var index = 0; index < _renderers.Length; index++)
            {
                var renderer = _renderers[index];
                if (renderer != null)
                {
                    renderer.sortingOrder = order +
                        _sortingOffsets[index];
                }
            }
        }

        private void CaptureSortingOffsets()
        {
            _sortingOffsets = new int[_renderers.Length];
            var minimumOrder = int.MaxValue;
            for (var index = 0;
                 index < _renderers.Length;
                 index++)
            {
                var renderer = _renderers[index];
                if (renderer != null)
                {
                    minimumOrder = Mathf.Min(
                        minimumOrder,
                        renderer.sortingOrder);
                }
            }

            if (minimumOrder == int.MaxValue)
            {
                minimumOrder = 0;
            }

            for (var index = 0;
                 index < _renderers.Length;
                 index++)
            {
                var renderer = _renderers[index];
                _sortingOffsets[index] = renderer != null
                    ? renderer.sortingOrder - minimumOrder
                    : 0;
            }
        }
    }
}
