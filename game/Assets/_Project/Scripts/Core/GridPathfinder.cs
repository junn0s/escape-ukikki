using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 8방향 그리드 A*. NavMesh를 대체한다.
    /// docs/technical-design-document.md §10.1
    ///
    /// 중요: 경로 거리를 반드시 제공해야 한다. GDD §11.2의 "직선거리가 아니라 이동 가능한
    /// 경로 거리"와 SDD §9.2 소음 우선순위 1순위가 이 값에 의존한다.
    ///
    /// 인스턴스를 재사용해 버퍼 할당을 피한다. 스레드 안전하지 않으므로 괴물마다 하나씩
    /// 두거나 서버에서 순차 호출한다.
    /// </summary>
    public sealed class GridPathfinder
    {
        private const float OrthogonalCost = 1f;
        private const float DiagonalCost = 1.41421356f;

        private static readonly Vector2Int[] Directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        private readonly PathGrid _grid;
        private readonly float[] _gScore;
        private readonly int[] _cameFrom;
        private readonly bool[] _closed;
        private readonly List<int> _openSet = new();

        public GridPathfinder(PathGrid grid)
        {
            _grid = grid ?? throw new System.ArgumentNullException(nameof(grid));

            int cellCount = grid.Width * grid.Height;
            _gScore = new float[cellCount];
            _cameFrom = new int[cellCount];
            _closed = new bool[cellCount];
        }

        /// <summary>
        /// 경로를 찾는다.
        /// </summary>
        /// <param name="path">시작을 제외한 경로 셀. null이면 채우지 않는다.</param>
        /// <returns>경로 거리(월드 단위). 경로가 없으면 -1</returns>
        public float FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            path?.Clear();

            if (!_grid.IsWalkable(start.x, start.y) || !_grid.IsWalkable(goal.x, goal.y))
            {
                return -1f;
            }

            if (start == goal)
            {
                return 0f;
            }

            int startIndex = ToIndex(start);
            int goalIndex = ToIndex(goal);

            Reset();

            _gScore[startIndex] = 0f;
            _openSet.Add(startIndex);

            while (_openSet.Count > 0)
            {
                int current = PopLowestFScore(goal);

                if (current == goalIndex)
                {
                    float distance = _gScore[current] * _grid.CellSize;
                    if (path != null)
                    {
                        ReconstructPath(startIndex, goalIndex, path);
                    }

                    return distance;
                }

                _closed[current] = true;
                ExpandNeighbors(current, goal);
            }

            return -1f;
        }

        /// <summary>
        /// 경로 거리만 필요할 때 쓴다. 소음 우선순위 평가용.
        /// </summary>
        public float GetPathDistance(Vector2Int start, Vector2Int goal) =>
            FindPath(start, goal, null);

        private void ExpandNeighbors(int current, Vector2Int goal)
        {
            int cx = current % _grid.Width;
            int cy = current / _grid.Width;

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int dir = Directions[i];
                int nx = cx + dir.x;
                int ny = cy + dir.y;

                if (!_grid.IsWalkable(nx, ny))
                {
                    continue;
                }

                bool isDiagonal = dir.x != 0 && dir.y != 0;

                // 대각선은 양쪽 직교 셀이 모두 통행 가능할 때만 허용한다.
                // 이 검사를 빼면 괴물이 벽 모서리를 뚫고 지나가는 것처럼 보인다
                // (map-level-design.md §11).
                if (isDiagonal &&
                    (!_grid.IsWalkable(cx + dir.x, cy) || !_grid.IsWalkable(cx, cy + dir.y)))
                {
                    continue;
                }

                int neighbor = ny * _grid.Width + nx;
                if (_closed[neighbor])
                {
                    continue;
                }

                float tentative = _gScore[current] + (isDiagonal ? DiagonalCost : OrthogonalCost);

                if (tentative >= _gScore[neighbor])
                {
                    continue;
                }

                _gScore[neighbor] = tentative;
                _cameFrom[neighbor] = current;

                if (!_openSet.Contains(neighbor))
                {
                    _openSet.Add(neighbor);
                }
            }
        }

        /// <summary>
        /// 옥타일 거리. 8방향 이동에서 실제 최단 거리와 일치하므로
        /// A*가 최적 경로를 보장한다.
        /// </summary>
        private static float OctileHeuristic(int x, int y, Vector2Int goal)
        {
            int dx = Mathf.Abs(x - goal.x);
            int dy = Mathf.Abs(y - goal.y);
            int min = Mathf.Min(dx, dy);
            int max = Mathf.Max(dx, dy);

            return (max - min) * OrthogonalCost + min * DiagonalCost;
        }

        /// <summary>
        /// 선형 탐색으로 최소 F를 꺼낸다. 우선순위 큐보다 느리지만,
        /// 맵 규모(수천 셀)에서는 충분하고 구현이 단순하다.
        /// 성능 문제가 확인되면 바이너리 힙으로 교체한다.
        /// </summary>
        private int PopLowestFScore(Vector2Int goal)
        {
            int bestSlot = 0;
            float bestF = float.MaxValue;

            for (int i = 0; i < _openSet.Count; i++)
            {
                int index = _openSet[i];
                int x = index % _grid.Width;
                int y = index / _grid.Width;

                float f = _gScore[index] + OctileHeuristic(x, y, goal);

                if (f < bestF)
                {
                    bestF = f;
                    bestSlot = i;
                }
            }

            int best = _openSet[bestSlot];

            // 순서를 유지할 필요가 없으므로 마지막 원소로 덮어써 O(1)로 제거한다.
            _openSet[bestSlot] = _openSet[^1];
            _openSet.RemoveAt(_openSet.Count - 1);

            return best;
        }

        private void ReconstructPath(int startIndex, int goalIndex, List<Vector2Int> path)
        {
            int current = goalIndex;

            while (current != startIndex)
            {
                path.Add(new Vector2Int(current % _grid.Width, current / _grid.Width));
                current = _cameFrom[current];
            }

            path.Reverse();
        }

        private int ToIndex(Vector2Int cell) => cell.y * _grid.Width + cell.x;

        private void Reset()
        {
            _openSet.Clear();

            for (int i = 0; i < _gScore.Length; i++)
            {
                _gScore[i] = float.MaxValue;
                _closed[i] = false;
                _cameFrom[i] = -1;
            }
        }
    }
}
