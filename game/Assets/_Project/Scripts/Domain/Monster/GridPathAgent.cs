using System.Collections.Generic;
using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// NavMeshAgent를 대체한다. 그리드 A* 경로를 따라 이동한다.
    /// docs/technical-design-document.md §10.1
    ///
    /// 경로 계산은 목표가 바뀔 때만 하고, 이동은 매 물리 프레임 진행한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GridPathAgent : MonoBehaviour
    {
        [Tooltip("이 거리 안에 들어오면 해당 웨이포인트를 통과한 것으로 본다")]
        [SerializeField] private float _waypointTolerance = 0.15f;

        [Tooltip("최종 목표 도착 판정 거리")]
        [SerializeField] private float _arriveTolerance = 0.3f;

        private readonly List<Vector2Int> _path = new();
        private Rigidbody2D _body;
        private PathGrid _grid;
        private GridPathfinder _pathfinder;

        private int _waypointIndex;
        private Vector2 _destination;

        /// <summary>이동 속도. MonsterBrain이 상태에 따라 바꾼다.</summary>
        public float Speed { get; set; } = 2.6f;

        /// <summary>현재 목표에 도착했는지.</summary>
        public bool HasArrived { get; private set; } = true;

        /// <summary>경로 탐색이 실패했는지. 끼임 복구 판단에 쓴다.</summary>
        public bool HasPathFailed { get; private set; }

        /// <summary>현재 이동 방향. 감지 판정의 Facing으로 쓴다.</summary>
        public Vector2 MoveDirection { get; private set; } = Vector2.down;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        /// <summary>씬의 그리드를 주입한다. 씬 구성 시 한 번 호출한다.</summary>
        public void Initialize(PathGrid grid)
        {
            _grid = grid ?? throw new System.ArgumentNullException(nameof(grid));
            _pathfinder = new GridPathfinder(grid);
        }

        /// <summary>
        /// 목표를 설정하고 경로를 계산한다.
        /// </summary>
        /// <returns>경로 거리. 도달 불가면 -1</returns>
        public float SetDestination(Vector2 worldPosition)
        {
            if (_pathfinder == null)
            {
                Debug.LogError($"[{nameof(GridPathAgent)}] Initialize가 호출되지 않았다", this);
                return -1f;
            }

            _destination = worldPosition;
            _waypointIndex = 0;
            HasArrived = false;

            Vector2Int start = ResolveStartCell();
            Vector2Int goal = _grid.WorldToCell(worldPosition);

            // 목표가 벽 안이면 가장 가까운 통행 가능 셀로 대체한다.
            if (!_grid.IsWalkable(goal.x, goal.y) &&
                _grid.TryFindNearestWalkable(worldPosition, 6, out Vector2Int nearGoal))
            {
                goal = nearGoal;
            }

            float distance = _pathfinder.FindPath(start, goal, _path);
            HasPathFailed = distance < 0f;

            if (HasPathFailed)
            {
                _path.Clear();
                HasArrived = true;
            }

            return distance;
        }

        /// <summary>목표까지의 경로 거리만 조회한다. 이동 목표를 바꾸지 않는다.</summary>
        public float QueryPathDistance(Vector2 worldPosition)
        {
            if (_pathfinder == null)
            {
                return -1f;
            }

            Vector2Int goal = _grid.WorldToCell(worldPosition);
            if (!_grid.IsWalkable(goal.x, goal.y))
            {
                return -1f;
            }

            return _pathfinder.GetPathDistance(ResolveStartCell(), goal);
        }

        public void Stop()
        {
            _path.Clear();
            HasArrived = true;
            _body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (HasArrived || _path.Count == 0)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 position = _body.position;

            // 남은 웨이포인트를 순서대로 통과한다.
            while (_waypointIndex < _path.Count)
            {
                Vector2Int cell = _path[_waypointIndex];
                Vector2 target = _grid.CellToWorld(cell.x, cell.y);

                bool isLast = _waypointIndex == _path.Count - 1;
                float tolerance = isLast ? _arriveTolerance : _waypointTolerance;

                if ((target - position).sqrMagnitude > tolerance * tolerance)
                {
                    MoveDirection = (target - position).normalized;
                    _body.linearVelocity = MoveDirection * Speed;
                    return;
                }

                _waypointIndex++;
            }

            // 모든 웨이포인트를 통과했다.
            HasArrived = true;
            _body.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// 시작 셀을 구한다. 벽에 끼어 있으면 가장 가까운 통행 가능 셀을 쓴다.
        /// SDD §10.4 끼임 복구.
        /// </summary>
        private Vector2Int ResolveStartCell()
        {
            Vector2 position = _body != null ? _body.position : (Vector2)transform.position;
            Vector2Int cell = _grid.WorldToCell(position);

            if (_grid.IsWalkable(cell.x, cell.y))
            {
                return cell;
            }

            return _grid.TryFindNearestWalkable(position, 6, out Vector2Int recovered)
                ? recovered
                : cell;
        }

        private void OnDrawGizmosSelected()
        {
            if (_grid == null || _path.Count == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
            for (int i = _waypointIndex; i < _path.Count; i++)
            {
                Vector2 point = _grid.CellToWorld(_path[i].x, _path[i].y);
                Gizmos.DrawWireCube(point, Vector3.one * _grid.CellSize * 0.6f);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_destination, 0.3f);
        }
    }
}
