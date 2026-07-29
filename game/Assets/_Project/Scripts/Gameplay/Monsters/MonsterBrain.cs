using System;
using MonkeyLab.Gameplay.Noise;
using UnityEngine;
using UnityEngine.AI;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterBrain : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private Transform[] _patrolPoints;

        private NoiseEventData? _currentNoise;
        private int _patrolIndex = -1;
        private float _nextAiTickTime;
        private float _stateEndsAt;
        private float _noiseAccelerationEndsAt;
        private bool _isInitialized;
        private bool _isSubscribed;

        public event Action<MonsterBrain, MonsterState> StateChanged;

        public MonsterState State { get; private set; } = MonsterState.Patrol;
        public long CurrentNoiseId => _currentNoise?.NoiseId ?? 0;
        public int PatrolPointCount => _patrolPoints?.Length ?? 0;
        public MonsterBalanceConfig Config => _config;

        public void Configure(
            NavMeshAgent agent,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            Transform[] patrolPoints)
        {
            Unsubscribe();
            _agent = agent;
            _noiseService = noiseService;
            _config = config;
            _patrolPoints = patrolPoints;
            Subscribe();
        }

        private void Awake()
        {
            if (_agent == null || _noiseService == null || _config == null || PatrolPointCount == 0)
            {
                Debug.LogError("[Monster] MonsterBrain is missing required references.", this);
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Unsubscribe();
            Subscribe();
            if (!TryInitializeOnNavMesh())
            {
                enabled = false;
                return;
            }

            MoveToNextPatrolPoint();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
        }

        private void Update()
        {
            if (!_isInitialized || Time.time < _nextAiTickTime)
            {
                return;
            }

            _nextAiTickTime = Time.time + _config.AiTickIntervalSeconds;
            TickState();
        }

        private void TickState()
        {
            switch (State)
            {
                case MonsterState.Patrol:
                    if (HasReachedDestination())
                    {
                        EnterRoomIdle();
                    }
                    else if (HasPathFailed())
                    {
                        MoveToNextPatrolPoint();
                    }
                    break;
                case MonsterState.RoomIdle:
                    if (Time.time >= _stateEndsAt)
                    {
                        MoveToNextPatrolPoint();
                    }
                    break;
                case MonsterState.InvestigateNoise:
                    if (Time.time >= _noiseAccelerationEndsAt &&
                        _agent.speed > _config.PatrolSpeed)
                    {
                        _agent.speed = _config.PatrolSpeed;
                    }

                    if (HasReachedDestination())
                    {
                        EnterSearch();
                    }
                    else if (HasPathFailed())
                    {
                        _currentNoise = null;
                        MoveToNextPatrolPoint();
                    }
                    break;
                case MonsterState.Search:
                    if (Time.time >= _stateEndsAt)
                    {
                        MoveToNextPatrolPoint();
                    }
                    break;
            }
        }

        private void HandleNoiseEmitted(NoiseEventData noise)
        {
            if (!_isInitialized || State is MonsterState.Chase or MonsterState.Bite)
            {
                return;
            }

            if (!TryCreateCandidate(noise, out var candidate, out var path))
            {
                Debug.Log(
                    $"[Monster] id={name} ignored noise={noise.NoiseId} because no complete NavMesh path exists.",
                    this);
                return;
            }

            if (!candidate.IsWithinRadius)
            {
                Debug.Log(
                    $"[Monster] id={name} ignored noise={noise.NoiseId} " +
                    $"distance={candidate.PathDistance:0.#}m radius={noise.PathRadius:0.#}m.",
                    this);
                return;
            }

            if (State == MonsterState.InvestigateNoise && _currentNoise.HasValue &&
                TryCreateCandidate(_currentNoise.Value, out var currentCandidate, out _) &&
                !NoisePriority.HasHigherPriority(candidate, currentCandidate))
            {
                return;
            }

            _currentNoise = noise;
            _agent.isStopped = false;
            _agent.speed = _config.NoiseInvestigateSpeed;
            _agent.SetPath(path);
            _noiseAccelerationEndsAt = Time.time + _config.NoiseAccelerationSeconds;
            SetState(MonsterState.InvestigateNoise);
            Debug.Log(
                $"[Monster] id={name} investigating noise={noise.NoiseId} " +
                $"distance={candidate.PathDistance:0.#}m.",
                this);
        }

        private bool TryCreateCandidate(
            NoiseEventData noise,
            out NoiseCandidate candidate,
            out NavMeshPath path)
        {
            candidate = default;
            path = new NavMeshPath();
            if (_agent == null || !_agent.isOnNavMesh ||
                !NavMesh.SamplePosition(
                    noise.WorldPosition,
                    out var targetHit,
                    _agent.height,
                    _agent.areaMask) ||
                !NavMesh.CalculatePath(transform.position, targetHit.position, _agent.areaMask, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            var pathDistance = CalculatePathDistance(path);
            candidate = new NoiseCandidate(noise, pathDistance);
            return true;
        }

        private bool TryInitializeOnNavMesh()
        {
            if (_agent == null || _noiseService == null || _config == null || PatrolPointCount == 0)
            {
                return false;
            }

            if (!_agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(
                        transform.position,
                        out var hit,
                        _agent.height,
                        _agent.areaMask) ||
                    !_agent.Warp(hit.position))
                {
                    Debug.LogError($"[Monster] id={name} could not be placed on the NavMesh.", this);
                    return false;
                }
            }

            _agent.speed = _config.PatrolSpeed;
            _isInitialized = true;
            return true;
        }

        private void MoveToNextPatrolPoint()
        {
            if (!_isInitialized || PatrolPointCount == 0)
            {
                return;
            }

            _currentNoise = null;
            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
            _agent.isStopped = false;
            _agent.speed = _config.PatrolSpeed;
            _agent.SetDestination(_patrolPoints[_patrolIndex].position);
            SetState(MonsterState.Patrol);
        }

        private void EnterRoomIdle()
        {
            _agent.isStopped = true;
            _stateEndsAt = Time.time + _config.RoomIdleSeconds + UnityEngine.Random.Range(
                -_config.RoomIdleVariationSeconds,
                _config.RoomIdleVariationSeconds);
            SetState(MonsterState.RoomIdle);
        }

        private void EnterSearch()
        {
            _currentNoise = null;
            _agent.isStopped = true;
            _stateEndsAt = Time.time + _config.SearchSeconds;
            SetState(MonsterState.Search);
        }

        private bool HasReachedDestination()
        {
            return !_agent.pathPending &&
                   _agent.pathStatus == NavMeshPathStatus.PathComplete &&
                   _agent.remainingDistance <= _agent.stoppingDistance;
        }

        private bool HasPathFailed()
        {
            return !_agent.pathPending && _agent.pathStatus != NavMeshPathStatus.PathComplete;
        }

        private void SetState(MonsterState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            State = nextState;
            StateChanged?.Invoke(this, nextState);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _noiseService == null)
            {
                return;
            }

            _noiseService.NoiseEmitted += HandleNoiseEmitted;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _noiseService == null)
            {
                return;
            }

            _noiseService.NoiseEmitted -= HandleNoiseEmitted;
            _isSubscribed = false;
        }

        private static float CalculatePathDistance(NavMeshPath path)
        {
            var distance = 0f;
            for (var index = 1; index < path.corners.Length; index++)
            {
                distance += Vector3.Distance(path.corners[index - 1], path.corners[index]);
            }

            return distance;
        }
    }
}
