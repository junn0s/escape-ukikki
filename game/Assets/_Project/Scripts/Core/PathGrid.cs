using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 길찾기용 통행 가능 격자. 씬의 충돌 정보를 한 번 굽고 재사용한다.
    /// docs/technical-design-document.md §10.1
    ///
    /// 순수 데이터라 Unity 씬 없이 테스트할 수 있다.
    /// </summary>
    public sealed class PathGrid
    {
        private readonly bool[] _walkable;

        public PathGrid(Vector2 origin, float cellSize, int width, int height)
        {
            if (cellSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(cellSize), cellSize, "셀 크기는 0보다 커야 한다.");
            }

            if (width <= 0 || height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(width), $"{width}x{height}", "격자 크기는 1 이상이어야 한다.");
            }

            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Height = height;

            _walkable = new bool[width * height];
        }

        /// <summary>격자 (0,0) 셀의 중심에 해당하는 월드 좌표.</summary>
        public Vector2 Origin { get; }

        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }

        public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public bool IsWalkable(int x, int y) => IsInside(x, y) && _walkable[y * Width + x];

        public void SetWalkable(int x, int y, bool value)
        {
            if (!IsInside(x, y))
            {
                return;
            }

            _walkable[y * Width + x] = value;
        }

        /// <summary>모든 셀을 한 번에 설정한다. 굽기 직후 호출한다.</summary>
        public void Fill(bool value)
        {
            for (int i = 0; i < _walkable.Length; i++)
            {
                _walkable[i] = value;
            }
        }

        public Vector2 CellToWorld(int x, int y) =>
            Origin + new Vector2(x * CellSize, y * CellSize);

        public Vector2Int WorldToCell(Vector2 world)
        {
            Vector2 local = (world - Origin) / CellSize;
            return new Vector2Int(
                Mathf.RoundToInt(local.x),
                Mathf.RoundToInt(local.y));
        }

        /// <summary>
        /// 주어진 위치에서 가장 가까운 통행 가능 셀을 찾는다.
        /// 괴물이 벽 안으로 밀려났을 때 복구용으로 쓴다 (SDD §10.4).
        /// </summary>
        public bool TryFindNearestWalkable(Vector2 world, int maxRingSearch, out Vector2Int cell)
        {
            Vector2Int start = WorldToCell(world);

            if (IsWalkable(start.x, start.y))
            {
                cell = start;
                return true;
            }

            // 중심에서 바깥으로 링을 넓혀가며 탐색한다.
            for (int ring = 1; ring <= maxRingSearch; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        // 링의 테두리만 검사한다.
                        if (Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring)
                        {
                            continue;
                        }

                        int nx = start.x + dx;
                        int ny = start.y + dy;

                        if (IsWalkable(nx, ny))
                        {
                            cell = new Vector2Int(nx, ny);
                            return true;
                        }
                    }
                }
            }

            cell = start;
            return false;
        }
    }
}
