using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Noise;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MonsterBrain : MonoBehaviour
    {
        private const float StoppingDistance = 0.12f;

        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private TopDownNavigationGraph _navigationGraph;
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private LocalRoundPhasePrototype _roundPhase;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private MonsterBiteController _biteController;
        [SerializeField] private Transform[] _patrolPoints;

        private readonly List<Vector2> _path = new(24);
        private NoiseEventData? _currentNoise;
        private int _pathIndex;
        private int _patrolIndex = -1;
        private float _currentSpeed;
        private float _nextAiTickTime;
        private float _stateEndsAt;
        private float _noiseAccelerationEndsAt;
        private Vector3 _lastKnownTargetPosition;
        private bool _hasLastKnownTargetPosition;
        private bool _hasPath;
        private bool _pathFailed;
        private bool _biteHasResolved;
        private bool _isNoiseAmbushChase;
        private bool _isInitialized;
        private bool _isSubscribed;
        private Vector3 _noiseAmbushOrigin;
        private MonsterBiteResult _resolvedBiteResult;

        public event Action<MonsterBrain, MonsterState> StateChanged;

        public MonsterState State { get; private set; } = MonsterState.Patrol;
        public MonsterDetectionType LastDetectionType { get; private set; }
        public long CurrentNoiseId => _currentNoise?.NoiseId ?? 0;
        public int PatrolPointCount => _patrolPoints?.Length ?? 0;
        public MonsterBalanceConfig Config => _config;
        public MonsterSenses Senses => _senses;
        public MonsterBiteController BiteController => _biteController;
        public LocalRoundPhasePrototype RoundPhase => _roundPhase;
        public TopDownNavigationGraph NavigationGraph => _navigationGraph;

        public void ApplyReplicatedStateForPresentation(
            MonsterState state)
        {
            SetState(state);
        }

        public void Configure(
            Rigidbody2D body,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterSenses senses,
            MonsterBiteController biteController,
            Transform[] patrolPoints)
        {
            Unsubscribe();
            _body = body;
            _navigationGraph = navigationGraph;
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
            _body ??= GetComponent<Rigidbody2D>();
            if (!HasRequiredReferences())
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
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            _isInitialized = true;
            MoveToNextPatrolPoint();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _biteController?.Cancel();
            StopMovement();
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

        private void FixedUpdate()
        {
            if (!_isInitialized || !_hasPath || _body == null)
            {
                return;
            }

            while (_pathIndex < _path.Count &&
                   Vector2.Distance(_body.position, _path[_pathIndex]) <= StoppingDistance)
            {
                _pathIndex++;
            }

            if (_pathIndex >= _path.Count)
            {
                _hasPath = false;
                return;
            }

            var direction = _path[_pathIndex] - _body.position;
            var moveDistance = _currentSpeed * Time.fixedDeltaTime;
            var nextPosition = Vector2.Distance(_body.position, _path[_pathIndex]) <= moveDistance
                ? _path[_pathIndex]
                : _body.position + direction.normalized * moveDistance;
            _body.MovePosition(nextPosition);

            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                _senses.SetFacingDirection(direction.normalized);
            }
        }

        private void TickState()
        {
            if (!_roundPhase.IsMonsterAggressionEnabled &&
                State is MonsterState.Chase or MonsterState.Bite)
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
                    else if (_pathFailed)
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
                _currentSpeed > _config.PatrolSpeed)
            {
                _currentSpeed = _config.PatrolSpeed;
            }

            if (HasReachedDestination())
            {
                _noiseAmbushOrigin = _currentNoise?.WorldPosition ??
                                     transform.position;
                if (TryEnterChase(useNoiseAmbushRadius: true))
                {
                    return;
                }

                EnterSearch(transform.position);
            }
            else if (_pathFailed)
            {
                _currentNoise = null;
                MoveToNextPatrolPoint();
            }
        }

        private void TickChase()
        {
            MonsterDetectionType detectionType;
            var hasDetectedTarget = _isNoiseAmbushChase
                ? _senses.TryDetectTargetNearPosition(
                    _noiseAmbushOrigin,
                    _config.NoiseAmbushRadius,
                    out detectionType)
                : _senses.TryDetectTarget(out detectionType);
            if (!hasDetectedTarget)
            {
                EnterSearch(_hasLastKnownTargetPosition
                    ? _lastKnownTargetPosition
                    : transform.position);
                return;
            }

            LastDetectionType = detectionType;
            _lastKnownTargetPosition = _senses.Target.transform.position;
            _hasLastKnownTargetPosition = true;

            if (_senses.IsTargetInBiteRange())
            {
                EnterBite();
                return;
            }

            SetChaseDestination(
                _lastKnownTargetPosition,
                _isNoiseAmbushChase
                    ? _config.NoiseInvestigateSpeed
                    : _config.ChaseSpeed);
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
                _resolvedBiteResult = result;
                _stateEndsAt = Time.time + _config.BiteRecoverySeconds;
            }

            if (Time.time < _stateEndsAt)
            {
                return;
            }

            if (MonsterAggroRules.ShouldReleaseTargetAfterBite(_resolvedBiteResult))
            {
                MoveToNextPatrolPoint();
                return;
            }

            if (!TryEnterChase(_isNoiseAmbushChase))
            {
                EnterSearch(_hasLastKnownTargetPosition
                    ? _lastKnownTargetPosition
                    : transform.position);
            }
        }

        private bool TryEnterChase(bool useNoiseAmbushRadius = false)
        {
            if (!_roundPhase.IsMonsterAggressionEnabled)
            {
                return false;
            }

            MonsterDetectionType detectionType;
            var hasDetectedTarget = useNoiseAmbushRadius
                ? _senses.TryDetectTargetNearPosition(
                    _noiseAmbushOrigin,
                    _config.NoiseAmbushRadius,
                    out detectionType)
                : _senses.TryDetectTarget(out detectionType);
            if (!hasDetectedTarget)
            {
                return false;
            }

            _currentNoise = null;
            LastDetectionType = detectionType;
            _lastKnownTargetPosition = _senses.Target.transform.position;
            _hasLastKnownTargetPosition = true;
            _isNoiseAmbushChase = useNoiseAmbushRadius;
            SetChaseDestination(
                _lastKnownTargetPosition,
                useNoiseAmbushRadius
                    ? _config.NoiseInvestigateSpeed
                    : _config.ChaseSpeed);
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

            StopMovement();
            _biteHasResolved = false;
            _resolvedBiteResult = MonsterBiteResult.None;
            FaceTarget();
            SetState(MonsterState.Bite);
        }

        private void HandleNoiseEmitted(NoiseEventData noise)
        {
            if (!_isInitialized || !_roundPhase.IsMonsterAggressionEnabled ||
                State is MonsterState.Chase or MonsterState.Bite)
            {
                return;
            }

            if (!TryCreateCandidate(noise, out var candidate))
            {
                Debug.Log(
                    $"[Monster] id={name} ignored noise={noise.NoiseId} because no complete 2D path exists.",
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
                TryCreateCandidate(_currentNoise.Value, out var currentCandidate) &&
                !NoisePriority.HasHigherPriority(candidate, currentCandidate))
            {
                return;
            }

            _currentNoise = noise;
            SetDestination(noise.WorldPosition, _config.NoiseInvestigateSpeed);
            _noiseAccelerationEndsAt = Time.time + _config.NoiseAccelerationSeconds;
            SetState(MonsterState.InvestigateNoise);
            Debug.Log(
                $"[Monster] id={name} investigating noise={noise.NoiseId} " +
                $"distance={candidate.PathDistance:0.#}m.",
                this);
        }

        private bool TryCreateCandidate(
            NoiseEventData noise,
            out NoiseCandidate candidate)
        {
            candidate = default;
            if (_navigationGraph == null ||
                !_navigationGraph.TryGetPathDistance(
                    transform.position,
                    noise.WorldPosition,
                    out var pathDistance))
            {
                return false;
            }

            candidate = new NoiseCandidate(noise, pathDistance);
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
            _isNoiseAmbushChase = false;
            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
            SetDestination(_patrolPoints[_patrolIndex].position, _config.PatrolSpeed);
            SetState(MonsterState.Patrol);
        }

        private void EnterRoomIdle()
        {
            StopMovement();
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
            _isNoiseAmbushChase = false;
            SetDestination(searchPosition, _config.PatrolSpeed);
            _stateEndsAt = Time.time + _config.SearchSeconds;
            SetState(MonsterState.Search);
        }

        private bool SetDestination(Vector3 destination, float speed)
        {
            _pathFailed = !_navigationGraph.TryBuildPath(
                _body.position,
                destination,
                _path,
                out _);
            _pathIndex = 0;
            _hasPath = !_pathFailed && _path.Count > 0;
            _currentSpeed = speed;
            return !_pathFailed;
        }

        private void SetChaseDestination(Vector3 destination, float speed)
        {
            if (!_senses.HasClearPathToTarget())
            {
                SetDestination(destination, speed);
                return;
            }

            _path.Clear();
            _path.Add(destination);
            _pathIndex = 0;
            _pathFailed = false;
            _hasPath = true;
            _currentSpeed = speed;
        }

        private void StopMovement()
        {
            _hasPath = false;
            _path.Clear();
            _pathIndex = 0;
        }

        private void FaceTarget()
        {
            if (_senses?.Target == null || _body == null)
            {
                return;
            }

            var direction = (Vector2)(
                _senses.Target.transform.position - transform.position);
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                _senses.SetFacingDirection(direction.normalized);
            }
        }

        private bool HasReachedDestination()
        {
            return !_pathFailed && !_hasPath;
        }

        private bool HasRequiredReferences()
        {
            return _body != null && _navigationGraph != null &&
                   _noiseService != null && _config != null &&
                   _roundPhase != null && _senses != null &&
                   _biteController != null && PatrolPointCount > 0;
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
    }
}
