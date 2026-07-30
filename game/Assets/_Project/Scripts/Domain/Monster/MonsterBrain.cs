using System.Collections.Generic;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Noise;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// 괴물 한 마리의 상태 머신.
    ///
    /// AI 판단은 매 프레임이 아니라 Balance의 AiTickRateHz(기본 8Hz)로 돈다
    /// (technical-design-document.md §10). 이동 자체는 GridPathAgent가 물리 프레임마다 진행한다.
    ///
    /// docs/system-design-document.md §10.1 상태 표를 따른다.
    /// </summary>
    [RequireComponent(typeof(GridPathAgent))]
    public sealed class MonsterBrain : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private NoiseService _noiseService;

        [Tooltip("순찰 지점. 비어 있으면 제자리에서 대기한다")]
        [SerializeField] private Transform[] _patrolPoints;

        [Tooltip("추격 대상 후보. M1은 로컬 플레이어 하나만 넣는다")]
        [SerializeField] private Transform _player;

        private GridPathAgent _agent;
        private MonsterState _state = MonsterState.Patrol;

        private float _nextTickTime;
        private float _stateTimer;
        private int _patrolIndex;

        // 소리 조사용
        private NoiseEvent _targetNoise;
        private float _sprintRemaining;
        private int _lastHandledNoiseId;

        // 후보 평가용 재사용 버퍼 (GC 할당 방지)
        private readonly List<NoiseEvent> _candidates = new();
        private readonly List<float> _distances = new();

        public MonsterState State => _state;

        /// <summary>상태가 바뀔 때 발생. 표현·디버그 계층이 구독한다.</summary>
        public event System.Action<MonsterState> StateChanged;

        private void Awake()
        {
            _agent = GetComponent<GridPathAgent>();

            if (_balance == null || _senses == null || _noiseService == null)
            {
                Debug.LogError($"[{nameof(MonsterBrain)}] 필수 참조 미할당", this);
                enabled = false;
                return;
            }

            _agent.Speed = _balance.MonsterPatrolSpeed;
        }

        private void Start()
        {
            EnterPatrol();
        }

        private void Update()
        {
            // 감지 판정이 쓰는 시야 방향을 이동 방향으로 갱신한다.
            if (_agent.MoveDirection.sqrMagnitude > 0.01f)
            {
                _senses.Facing = _agent.MoveDirection;
            }

            if (Time.time < _nextTickTime)
            {
                return;
            }

            float tickInterval = 1f / Mathf.Max(1f, _balance.AiTickRateHz);
            _nextTickTime = Time.time + tickInterval;

            Tick(tickInterval);
        }

        private void Tick(float deltaTime)
        {
            _stateTimer += deltaTime;

            // 추격 중이 아니면 항상 감지를 먼저 확인한다.
            // 추격 중에는 일반 소음을 무시한다 (SDD §9.3).
            if (_state != MonsterState.Chase && _senses.CanDetect(_player))
            {
                EnterChase();
                return;
            }

            switch (_state)
            {
                case MonsterState.Patrol:
                    TickPatrol();
                    break;
                case MonsterState.RoomIdle:
                    TickRoomIdle();
                    break;
                case MonsterState.InvestigateNoise:
                    TickInvestigate(deltaTime);
                    break;
                case MonsterState.Chase:
                    TickChase();
                    break;
                case MonsterState.Search:
                    TickSearch();
                    break;
                case MonsterState.RecoverPath:
                    TickRecoverPath();
                    break;
            }
        }

        private void TickPatrol()
        {
            if (TryReactToNoise())
            {
                return;
            }

            if (_agent.HasPathFailed)
            {
                EnterRecoverPath();
                return;
            }

            if (_patrolPoints == null || _patrolPoints.Length == 0)
            {
                return;
            }

            if (_agent.HasArrived)
            {
                EnterRoomIdle();
            }
        }

        private void TickRoomIdle()
        {
            if (TryReactToNoise())
            {
                return;
            }

            // 방에 들어가면 약 6초간 주변을 살핀다 (GDD §12.1).
            if (_stateTimer >= _balance.RoomDwellSeconds)
            {
                AdvancePatrolPoint();
                EnterPatrol();
            }
        }

        private void TickInvestigate(float deltaTime)
        {
            // 더 우선순위 높은 소음이 생기면 목표를 바꿀 수 있다 (SDD §9.3).
            TryReactToNoise();

            // 가속은 최대 6초까지만 유지한다 (GDD §11.2).
            _sprintRemaining -= deltaTime;
            if (_sprintRemaining <= 0f)
            {
                _agent.Speed = _balance.MonsterPatrolSpeed;
            }

            if (_agent.HasPathFailed)
            {
                EnterRecoverPath();
                return;
            }

            if (_agent.HasArrived)
            {
                EnterSearch();
            }
        }

        private void TickChase()
        {
            if (_player == null || !_senses.CanDetect(_player))
            {
                // 표적을 잃으면 마지막 위치를 수색한다.
                EnterSearch();
                return;
            }

            _agent.Speed = _balance.MonsterChaseSpeed;
            _agent.SetDestination(_player.position);
        }

        private void TickSearch()
        {
            if (TryReactToNoise())
            {
                return;
            }

            // 짧게 수색한 뒤 순찰로 복귀한다 (GDD §11.2).
            if (_stateTimer >= _balance.SearchSeconds)
            {
                EnterPatrol();
            }
        }

        private void TickRecoverPath()
        {
            // 경로 복구는 순찰 지점 재설정으로 처리한다 (SDD §10.4).
            AdvancePatrolPoint();
            EnterPatrol();
        }

        /// <summary>
        /// 유효한 소음이 있으면 조사 상태로 전환한다.
        /// </summary>
        private bool TryReactToNoise()
        {
            IReadOnlyList<NoiseEvent> active = _noiseService.ActiveNoises;
            if (active.Count == 0)
            {
                return false;
            }

            _candidates.Clear();
            _distances.Clear();

            for (int i = 0; i < active.Count; i++)
            {
                NoiseEvent noise = active[i];

                // 이미 처리한 소음은 다시 반응하지 않는다.
                if (noise.NoiseId == _lastHandledNoiseId)
                {
                    continue;
                }

                _candidates.Add(noise);

                // 직선거리가 아니라 경로 거리를 쓴다 (GDD §11.2).
                _distances.Add(_agent.QueryPathDistance(noise.WorldPosition));
            }

            if (!NoisePrioritySelector.TrySelect(_candidates, _distances, out NoiseEvent selected))
            {
                return false;
            }

            // 이미 같은 소음을 조사 중이면 다시 전환하지 않는다.
            if (_state == MonsterState.InvestigateNoise && selected.NoiseId == _targetNoise.NoiseId)
            {
                return false;
            }

            EnterInvestigate(selected);
            return true;
        }

        private void AdvancePatrolPoint()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0)
            {
                return;
            }

            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
        }

        private void EnterPatrol()
        {
            SetState(MonsterState.Patrol);
            _agent.Speed = _balance.MonsterPatrolSpeed;

            if (_patrolPoints != null && _patrolPoints.Length > 0 && _patrolPoints[_patrolIndex] != null)
            {
                _agent.SetDestination(_patrolPoints[_patrolIndex].position);
            }
        }

        private void EnterRoomIdle()
        {
            SetState(MonsterState.RoomIdle);
            _agent.Stop();
        }

        private void EnterInvestigate(NoiseEvent noise)
        {
            SetState(MonsterState.InvestigateNoise);

            _targetNoise = noise;
            _lastHandledNoiseId = noise.NoiseId;

            // 소리에 반응한 괴물은 최대 6초간 1.5배 속도로 이동한다.
            _agent.Speed = _balance.MonsterInvestigateSpeed;
            _sprintRemaining = _balance.NoiseSprintMaxSeconds;
            _agent.SetDestination(noise.WorldPosition);

            Debug.Log($"[Monster] 소음 조사 시작 id={noise.NoiseId} {noise.Intensity}");
        }

        private void EnterChase()
        {
            SetState(MonsterState.Chase);
            _agent.Speed = _balance.MonsterChaseSpeed;
            Debug.Log("[Monster] 추격 시작");
        }

        private void EnterSearch()
        {
            SetState(MonsterState.Search);
            _agent.Stop();
            _agent.Speed = _balance.MonsterPatrolSpeed;
        }

        private void EnterRecoverPath()
        {
            SetState(MonsterState.RecoverPath);
            _agent.Stop();
            Debug.LogWarning("[Monster] 경로 실패 — 복구 시도");
        }

        private void SetState(MonsterState next)
        {
            if (_state == next)
            {
                return;
            }

            _state = next;
            _stateTimer = 0f;
            StateChanged?.Invoke(next);
        }
    }
}
