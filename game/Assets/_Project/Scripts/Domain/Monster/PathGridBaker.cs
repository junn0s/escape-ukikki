using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// 씬의 충돌 정보를 읽어 길찾기 격자를 굽고 에이전트에 주입한다.
    /// docs/technical-design-document.md §10.1
    ///
    /// 굽기는 씬 시작 시 한 번만 한다. 문이 열리는 등 통행 가능 여부가 바뀌면
    /// 해당 셀만 갱신한다 (map-level-design.md §11).
    /// </summary>
    public sealed class PathGridBaker : MonoBehaviour
    {
        [SerializeField] private float _cellSize = 0.5f;
        [SerializeField] private Vector2 _worldOrigin = new(-20f, -8f);
        [SerializeField] private int _width = 80;
        [SerializeField] private int _height = 32;

        [Tooltip("통행을 막는 레이어 (벽)")]
        [SerializeField] private LayerMask _obstacleMask;

        [Tooltip("굽기 결과를 받을 에이전트")]
        [SerializeField] private GridPathAgent[] _agents;

        [Tooltip("셀 판정에 쓰는 검사 상자 크기 비율. 1보다 작으면 벽에 더 가까이 붙을 수 있다")]
        [Range(0.5f, 1.2f)]
        [SerializeField] private float _probeScale = 0.9f;

        [Tooltip("기즈모로 통행 불가 셀을 표시한다")]
        [SerializeField] private bool _drawGizmos;

        public PathGrid Grid { get; private set; }

        private void Awake()
        {
            Grid = Bake();

            if (_agents == null)
            {
                return;
            }

            foreach (GridPathAgent agent in _agents)
            {
                if (agent != null)
                {
                    agent.Initialize(Grid);
                }
            }
        }

        /// <summary>격자를 굽는다. 충돌체가 있는 셀은 통행 불가로 표시한다.</summary>
        public PathGrid Bake()
        {
            var grid = new PathGrid(_worldOrigin, _cellSize, _width, _height);
            var probe = Vector2.one * (_cellSize * _probeScale);

            int blocked = 0;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector2 center = grid.CellToWorld(x, y);
                    bool hasObstacle = Physics2D.OverlapBox(center, probe, 0f, _obstacleMask) != null;

                    grid.SetWalkable(x, y, !hasObstacle);

                    if (hasObstacle)
                    {
                        blocked++;
                    }
                }
            }

            int total = _width * _height;
            Debug.Log($"[PathGrid] 굽기 완료 {_width}x{_height} 셀 " +
                      $"(통행 가능 {total - blocked}, 막힘 {blocked})");

            if (blocked == total)
            {
                Debug.LogError(
                    "[PathGrid] 모든 셀이 막혔다. 레이어 마스크나 격자 범위를 확인하라.", this);
            }

            return grid;
        }

        /// <summary>문이 열리거나 닫힐 때 해당 영역만 다시 판정한다.</summary>
        public void RefreshArea(Bounds worldBounds)
        {
            if (Grid == null)
            {
                return;
            }

            Vector2Int min = Grid.WorldToCell(worldBounds.min);
            Vector2Int max = Grid.WorldToCell(worldBounds.max);
            var probe = Vector2.one * (_cellSize * _probeScale);

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    Vector2 center = Grid.CellToWorld(x, y);
                    bool hasObstacle = Physics2D.OverlapBox(center, probe, 0f, _obstacleMask) != null;
                    Grid.SetWalkable(x, y, !hasObstacle);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos)
            {
                // 범위만 표시한다.
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                var size = new Vector3(_width * _cellSize, _height * _cellSize, 0f);
                var center = (Vector3)_worldOrigin + size * 0.5f - new Vector3(_cellSize, _cellSize) * 0.5f;
                Gizmos.DrawWireCube(center, size);
                return;
            }

            if (Grid == null)
            {
                return;
            }

            Gizmos.color = new Color(0.84f, 0.23f, 0.26f, 0.35f);
            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    if (!Grid.IsWalkable(x, y))
                    {
                        Gizmos.DrawCube(Grid.CellToWorld(x, y), Vector3.one * _cellSize * 0.85f);
                    }
                }
            }
        }
    }
}
