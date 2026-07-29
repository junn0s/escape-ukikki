using System;
using MonkeyLab.Gameplay.Application;
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
        [SerializeField] private LocalRoundPhasePrototype _roundPhase;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private MonsterBiteController _biteController;
        [SerializeField] private Transform[] _patrolPoints;

        private NoiseEventData? _currentNoise;
        private int _patrolIndex = -1;
        private float _nextAiTickTime;
        private float _stateEndsAt;
        private float _noiseAccelerationEndsAt;
        private Vector3 _lastKnownTargetPosition;
        private bool _hasLastKnownTargetPosition;
        private bool _biteHasResolved;
        private bool _isInitialized;
        private bool _isSubscribed;

        public event Action<MonsterBrain, MonsterState> StateChanged;

        public MonsterState State { get; private set; } = MonsterState.Patrol;
        public MonsterDetectionType LastDetectionType { get; private set; }
        public long CurrentNoiseId => _currentNoise?.NoiseId ?? 0;
        public int PatrolPointCount => _patrolPoints?.Length ?? 0;
        public MonsterBalanceConfig Config => _config;
        public MonsterSenses Senses => _senses;
        public MonsterBiteController BiteController => _biteController;
        public LocalRoundPhasePrototype RoundPhase => _roundPhase;

        public void Configure(
            NavMeshAgent agent,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterSenses senses,
            MonsterBiteController biteController,
            Transform[] patrolPoints)
        {
            Unsubscribe();
            _agent = agent;
            _noiseService = noiseService;
            _config = config;
            _roundPhase = roundPhase;
            _senses = senses;
            _biteController = biteController;
            _patrolPoints = patrolPoints;
            Subscribe();
        }

        private void Awake()
        {
            if (_agent == null || _noiseService == null || _config == null ||
                _roundPhase == null || _senses == null || _biteController == null ||
                PatrolPointCount == 0)
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
            _biteController?.Cancel();
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
            if (!_roundPhase.IsMonsterAggressionEnabled &&
                (State == MonsterState.Chase || State == MonsterState.Bite))
            {
                MoveToNextPatrolPoint();
                return;
            }

            if (_roundPhase.IsMonsterAggressionEnabled &&
                State != MonsterState.Chase && State != MonsterState.Bite &&
                TryEnterChase())
            {
                return;
            }

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
                    TickInvestigateNoise();
                    break;
                case MonsterState.Chase:
                    TickChase();
                    break;
                case MonsterState.Bite:
                    TickBite();
                    break;
                case MonsterState.Search:
                    if (Time.time >= _stateEndsAt)
                    {
                        MoveToNextPatrolPoint();
                    }
                    break;
            }
        }

        private void TickInvestigateNoise()
        {
            if (Time.time >= _noiseAccelerationEndsAt &&
                _agent.speed > _config.PatrolSpeed)
            {
                _agent.speed = _config.PatrolSpeed;
            }

            if (HasReachedDestination())
            {
                EnterSearch(transform.position);
            }
            else if (HasPathFailed())
            {
                _currentNoise = null;
                MoveToNextPatrolPoint();
            }
        }

        private void TickChase()
        {
            if (!_senses.TryDetectTarget(out var detectionType))
            {
                EnterSearch(_hasLastKnownTargetPosition
                    ? _lastKnownTargetPosition
                    : transform.position);
                return;
            }

            LastDetectionType = detectionType;
            _lastKnownTargetPosition = _senses.Target.transform.position;
            _hasLastKnownTargetPosition = true;

            if (_senses.IsTargetInBiteRangeWithLineOfSight())
            {
                EnterBite();
                return;
            }

            _agent.isStopped = false;
            _agent.speed = _config.ChaseSpeed;
            _agent.SetDestination(_lastKnownTargetPosition);
        }

        private void TickBite()
        {
            FaceTarget();
            if (!_biteHasResolved)
            {
                var result = _biteController.Tick(Time.time);
                if (result == MonsterBiteResult.Pending)
                {
                    return;
                }

                _biteHasResolved = true;
                _stateEndsAt = Time.time + _config.BiteRecoverySeconds;
            }

            if (Time.time < _stateEndsAt)
            {
                return;
            }

            if (!TryEnterChase())
            {
                EnterSearch(_hasLastKnownTargetPosition
                    ? _lastKnownTargetPosition
                    : transform.position);
            }
        }

        private bool TryEnterChase()
        {
            if (!_roundPhase.IsMonsterAggressionEnabled ||
                !_senses.TryDetectTarget(out var detectionType))
            {
                return false;
            }

            _currentNoise = null;
            LastDetectionType = detectionType;
            _lastKnownTargetPosition = _senses.Target.transform.position;
            _hasLastKnownTargetPosition = true;
            _agent.isStopped = false;
            _agent.speed = _config.ChaseSpeed;
            _agent.SetDestination(_lastKnownTargetPosition);
            SetState(MonsterState.Chase);
            return true;
        }

        private void EnterBite()
        {
            var result = _biteController.TryBegin(Time.time);
            if (result != MonsterBiteResult.Pending)
            {
                return;
            }

            _agent.isStopped = true;
            _biteHasResolved = false;
            FaceTarget();
            SetState(MonsterState.Bite);
        }

        private void HandleNoiseEmitted(NoiseEventData noise)
        {
            if (!_isInitialized || !_roundPhase.IsMonsterAggressionEnabled ||
                State == MonsterState.Chase || State == MonsterState.Bite)
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
            if (_agent == null || _noiseService == null || _config == null ||
                _roundPhase == null || _senses == null || _biteController == null ||
                PatrolPointCount == 0)
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

            _biteController.Cancel();
            _currentNoise = null;
            LastDetectionType = MonsterDetectionType.None;
            _hasLastKnownTargetPosition = false;
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

        private void EnterSearch(Vector3 searchPosition)
        {
            _biteController.Cancel();
            _currentNoise = null;
            LastDetectionType = MonsterDetectionType.None;
            _agent.speed = _config.PatrolSpeed;
            if (NavMesh.SamplePosition(
                    searchPosition,
                    out var hit,
                    _agent.height,
                    _agent.areaMask))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
            else
            {
                _agent.isStopped = true;
            }

            _stateEndsAt = Time.time + _config.SearchSeconds;
            SetState(MonsterState.Search);
        }

        private void FaceTarget()
        {
            if (_senses?.Target == null)
            {
                return;
            }

            var direction = Vector3.ProjectOnPlane(
                _senses.Target.transform.position - transform.position,
                Vector3.up);
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
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
