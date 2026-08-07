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
        private const float PatrolScoreTolerance = 0.01f;

        private static readonly HashSet<MonsterBrain> ActiveBrainSet = new();

        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private TopDownNavigationGraph _navigationGraph;
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private LocalRoundPhasePrototype _roundPhase;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private MonsterBiteController _biteController;
        [SerializeField] private Transform[] _patrolPoints;

        private readonly List<Vector2> _path = new(24);
        private readonly List<Vector2> _recentPatrolDestinations = new(6);
        private readonly HashSet<Vector2> _evaluatedPatrolDestinations = new();
        private NoiseEventData? _currentNoise;
        private long _lastInvestigatedNoiseId;
        private int _pathIndex;
        private int _pathRecoveryAttempts;
        private float _currentSpeed;
        private float _nextAiTickTime;
        private float _stateEndsAt;
        private float _noiseAccelerationEndsAt;
        private float _forcedNoiseRoamEndsAt;
        private float _activeNoiseAmbushRadius;
        private float _lastMovementProgressAt;
        private float _worldSimulationPausedAt;
        private Vector3 _lastKnownTargetPosition;
        private Vector2 _lastMovementPosition;
        private Vector2 _activeDestination;
        private bool _hasLastKnownTargetPosition;
        private bool _hasPath;
        private bool _pathFailed;
        private bool _biteHasResolved;
        private bool _isNoiseAmbushChase;
        private bool _isForcedNoiseRoam;
        private bool _isInitialized;
        private bool _isSubscribed;
        private bool _hasReservedPatrolDestination;
        private bool _hasActiveDestination;
        private bool _wasWorldSimulationPaused;
        private Vector3 _noiseAmbushOrigin;
        private Vector2 _reservedPatrolDestination;
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
        public bool HasReservedPatrolDestination =>
            _hasReservedPatrolDestination;
        public Vector2 ReservedPatrolDestination =>
            _reservedPatrolDestination;
        public static IEnumerable<MonsterBrain> ActiveBrains =>
            ActiveBrainSet;

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
            ReleasePatrolDestination();
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
            ActiveBrainSet.Add(this);
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
            ActiveBrainSet.Remove(this);
            Unsubscribe();
            ReleasePatrolDestination();
            _biteController?.Cancel();
            StopMovement();
        }

        private void OnDestroy()
        {
            ActiveBrainSet.Remove(this);
            MonsterPatrolReservation.ReleaseAll(this);
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveBrainSet.Clear();
        }

        private void Update()
        {
            if (!_isInitialized || UpdateWorldSimulationPause() ||
                Time.time < _nextAiTickTime)
            {
                return;
            }

            _nextAiTickTime = Time.time + _config.AiTickIntervalSeconds;
            TickState();
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || UpdateWorldSimulationPause() ||
                !_hasPath || _body == null)
            {
                return;
            }

            if (HasMovementStalled())
            {
                if (TryRecoverBlockedPath())
                {
                    return;
                }

                _hasPath = false;
                _pathFailed = true;
                Debug.LogWarning(
                    $"[Monster] id={name} recovered from a blocked path " +
                    $"while state={State}.",
                    this);
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
            var movementDirection = ResolveMovementDirection(direction);
            var moveDistance = _currentSpeed * Time.fixedDeltaTime;
            var nextPosition = Vector2.Distance(_body.position, _path[_pathIndex]) <= moveDistance
                ? _path[_pathIndex]
                : _body.position + movementDirection * moveDistance;
            _body.MovePosition(nextPosition);

            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                _senses.SetFacingDirection(direction.normalized);
            }
        }

        private bool UpdateWorldSimulationPause()
        {
            var isPaused = _roundPhase != null &&
                           _roundPhase.IsWorldSimulationPaused;
            if (isPaused)
            {
                if (!_wasWorldSimulationPaused)
                {
                    _wasWorldSimulationPaused = true;
                    _worldSimulationPausedAt = Time.time;
                }

                if (_body != null)
                {
                    _body.linearVelocity = Vector2.zero;
                    _body.angularVelocity = 0f;
                }

                return true;
            }

            if (!_wasWorldSimulationPaused)
            {
                return false;
            }

            var pausedDuration = Mathf.Max(
                0f,
                Time.time - _worldSimulationPausedAt);
            _wasWorldSimulationPaused = false;
            _worldSimulationPausedAt = 0f;
            ShiftWorldTimers(pausedDuration);
            return false;
        }

        private void ShiftWorldTimers(float pausedDuration)
        {
            if (pausedDuration <= 0f)
            {
                return;
            }

            _nextAiTickTime += pausedDuration;
            _lastMovementProgressAt += pausedDuration;
            if (_stateEndsAt > 0f)
            {
                _stateEndsAt += pausedDuration;
            }

            if (_noiseAccelerationEndsAt > 0f)
            {
                _noiseAccelerationEndsAt += pausedDuration;
            }

            if (_forcedNoiseRoamEndsAt > 0f)
            {
                _forcedNoiseRoamEndsAt += pausedDuration;
            }

            _biteController?.DelayPending(pausedDuration);
        }

        private void TickState()
        {
            if (_activeNoiseAmbushRadius > 0f &&
                Time.time >= _forcedNoiseRoamEndsAt)
            {
                ClearForcedNoiseResponse();
                if (State == MonsterState.Search)
                {
                    MoveToNextPatrolPoint();
                    return;
                }
            }

            if (!_roundPhase.IsMonsterAggressionEnabled &&
                (State is MonsterState.Chase or MonsterState.Bite ||
                 IsForcedNoiseResponseActive()))
            {
                MoveToNextPatrolPoint();
                return;
            }

            if (_roundPhase.IsMonsterAggressionEnabled &&
                !IsCommittedToForcedNoiseRush() &&
                !IsForcedNoiseResponseActive() &&
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
                    if (_isForcedNoiseRoam)
                    {
                        TickForcedNoiseRoam();
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        MoveToNextPatrolPoint();
                    }
                    break;
            }
        }

        private void TickInvestigateNoise()
        {
            var isForcedRush = _currentNoise.HasValue &&
                               IsForcedRushNoise(_currentNoise.Value);
            if (!isForcedRush &&
                Time.time >= _noiseAccelerationEndsAt &&
                _currentSpeed > _config.PatrolSpeed)
            {
                _currentSpeed = _config.PatrolSpeed;
            }

            if (HasReachedDestination())
            {
                if (isForcedRush)
                {
                    BeginForcedNoiseResponse(_currentNoise.Value);
                    if (!TryEnterChase(useNoiseAmbushRadius: true))
                    {
                        EnterForcedNoiseRoam();
                    }
                    return;
                }

                _currentNoise = null;
                if (TryEnterChase())
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
            if (_pathFailed)
            {
                EnterSearchAfterForcedNoise(
                    _hasLastKnownTargetPosition
                        ? _lastKnownTargetPosition
                        : transform.position);
                return;
            }

            MonsterDetectionType detectionType;
            var hasDetectedTarget = _isNoiseAmbushChase
                ? _senses.TryDetectTargetNearPosition(
                    _noiseAmbushOrigin,
                    _activeNoiseAmbushRadius,
                    out detectionType)
                : _senses.TryDetectTarget(out detectionType);
            if (!hasDetectedTarget)
            {
                EnterSearchAfterForcedNoise(
                    _hasLastKnownTargetPosition
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
            // 소등·정지로는 물기가 취소되지 않는다(GDD 1.6). 표적이 사라졌거나
            // 감염·유령으로 감지 대상에서 빠진 경우만 취소한다.
            if (_senses?.Target == null || !_senses.Target.IsDetectable)
            {
                _biteController?.Cancel();
                EnterSearchAfterForcedNoise(
                    _hasLastKnownTargetPosition
                        ? _lastKnownTargetPosition
                        : transform.position);
                return;
            }

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
                if (IsForcedNoiseResponseActive())
                {
                    EnterForcedNoiseRoam();
                }
                else
                {
                    MoveToNextPatrolPoint();
                }
                return;
            }

            if (!TryEnterChase(_isNoiseAmbushChase))
            {
                EnterSearchAfterForcedNoise(
                    _hasLastKnownTargetPosition
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
                    _activeNoiseAmbushRadius,
                    out detectionType)
                : _senses.TryDetectTarget(out detectionType);
            if (!hasDetectedTarget)
            {
                return false;
            }

            _currentNoise = null;
            ReleasePatrolDestination();
            LastDetectionType = detectionType;
            _lastKnownTargetPosition = _senses.Target.transform.position;
            _hasLastKnownTargetPosition = true;
            _isNoiseAmbushChase = useNoiseAmbushRadius;
            _isForcedNoiseRoam = false;
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
            if (!_isInitialized || !_roundPhase.IsMonsterAggressionEnabled)
            {
                return;
            }

            var isForcedRush = IsForcedRushNoise(noise);
            if (IsForcedNoiseResponseActive() && !isForcedRush)
            {
                return;
            }

            if (State is MonsterState.Chase or MonsterState.Bite &&
                !isForcedRush)
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

            if (State == MonsterState.InvestigateNoise &&
                _currentNoise.HasValue &&
                !ShouldReplaceCurrentNoise(
                    candidate,
                    isForcedRush,
                    _currentNoise.Value))
            {
                return;
            }

            ClearForcedNoiseResponse();
            _currentNoise = noise;
            _lastInvestigatedNoiseId = noise.NoiseId;
            if (State == MonsterState.Bite)
            {
                _biteController.Cancel();
                _biteHasResolved = false;
                _resolvedBiteResult = MonsterBiteResult.None;
            }

            ReleasePatrolDestination();
            SetDestination(noise.WorldPosition, _config.NoiseInvestigateSpeed);
            _noiseAccelerationEndsAt = Time.time + _config.NoiseAccelerationSeconds;
            SetState(MonsterState.InvestigateNoise);
            Debug.Log(
                $"[Monster] id={name} investigating noise={noise.NoiseId} " +
                $"distance={candidate.PathDistance:0.#}m.",
                this);
        }

        /// <summary>
        /// 미션 실패와 스피커는 은신을 무시하는 강제 현장 급습이다.
        /// 반응 범위 안의 괴물은 기존 추격·물기 준비보다 이 소리를 우선하고,
        /// 현장 도착 전에는 지나가던 손전등·발걸음에 목표를 바꾸지 않는다.
        /// </summary>
        private bool IsCommittedToForcedNoiseRush()
        {
            return State == MonsterState.InvestigateNoise &&
                   _currentNoise.HasValue &&
                   IsForcedRushNoise(_currentNoise.Value);
        }

        private static bool IsForcedRushNoise(NoiseEventData noise)
        {
            return noise.SourceType is NoiseSourceType.MissionFailure or
                NoiseSourceType.Speaker;
        }

        private bool ShouldReplaceCurrentNoise(
            NoiseCandidate candidate,
            bool isForcedRush,
            NoiseEventData currentNoise)
        {
            var isCurrentForcedRush = IsForcedRushNoise(currentNoise);
            if (isForcedRush != isCurrentForcedRush)
            {
                return isForcedRush;
            }

            return !TryCreateCandidate(
                       currentNoise,
                       out var currentCandidate) ||
                   NoisePriority.HasHigherPriority(
                       candidate,
                       currentCandidate);
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
            ClearForcedNoiseResponse();
            ReleasePatrolDestination();

            var minimumSeparation =
                _config.PatrolDestinationSeparationMeters;
            var hasDestination = TrySelectPatrolDestination(
                                     true,
                                     minimumSeparation,
                                     out var destination) ||
                                 TrySelectPatrolDestination(
                                     false,
                                     minimumSeparation,
                                     out destination) ||
                                 TrySelectPatrolDestination(
                                     false,
                                     0f,
                                     out destination);
            if (!hasDestination)
            {
                _pathFailed = true;
                EnterRoomIdle();
                return;
            }

            _reservedPatrolDestination = destination;
            _hasReservedPatrolDestination = true;
            RecordRecentPatrolDestination(destination);
            SetDestination(destination, _config.PatrolSpeed);
            SetState(MonsterState.Patrol);
        }

        /// <summary>
        /// 활성 원숭이들의 모든 순찰 지점을 후보로 공유한다. 최근 방문지는 피하고,
        /// 다른 원숭이의 현재·예약 위치에서 가장 먼 방을 선택해 맵 전체를 나눠 순찰한다.
        /// </summary>
        private bool TrySelectPatrolDestination(
            bool respectRecentMemory,
            float minimumSeparationMeters,
            out Vector2 destination)
        {
            destination = default;
            _evaluatedPatrolDestinations.Clear();
            var hasCandidate = false;
            var bestCoverageDistanceSquared = float.NegativeInfinity;
            var bestTravelDistance = float.NegativeInfinity;

            foreach (var brain in ActiveBrainSet)
            {
                if (brain == null || brain._patrolPoints == null)
                {
                    continue;
                }

                for (var index = 0;
                     index < brain._patrolPoints.Length;
                     index++)
                {
                    var patrolPoint = brain._patrolPoints[index];
                    if (patrolPoint == null)
                    {
                        continue;
                    }

                    var candidate = (Vector2)patrolPoint.position;
                    if (!_evaluatedPatrolDestinations.Add(candidate) ||
                        Vector2.SqrMagnitude(candidate - _body.position) <=
                        StoppingDistance * StoppingDistance ||
                        (respectRecentMemory &&
                         _recentPatrolDestinations.Contains(candidate)) ||
                        !MonsterPatrolReservation.CanReserve(
                            candidate,
                            this,
                            minimumSeparationMeters) ||
                        !_navigationGraph.TryGetPathDistance(
                            _body.position,
                            candidate,
                            out var travelDistance))
                    {
                        continue;
                    }

                    var coverageDistanceSquared =
                        GetNearestOtherMonsterDistanceSquared(candidate);
                    if (!IsBetterPatrolCandidate(
                            hasCandidate,
                            coverageDistanceSquared,
                            travelDistance,
                            bestCoverageDistanceSquared,
                            bestTravelDistance))
                    {
                        continue;
                    }

                    hasCandidate = true;
                    destination = candidate;
                    bestCoverageDistanceSquared = coverageDistanceSquared;
                    bestTravelDistance = travelDistance;
                }
            }

            return hasCandidate && MonsterPatrolReservation.TryReserve(
                destination,
                this,
                minimumSeparationMeters);
        }

        private float GetNearestOtherMonsterDistanceSquared(
            Vector2 candidate)
        {
            var nearestDistanceSquared = float.MaxValue;
            foreach (var other in ActiveBrainSet)
            {
                if (other == null || other == this)
                {
                    continue;
                }

                var otherPosition = other._hasReservedPatrolDestination
                    ? other._reservedPatrolDestination
                    : (Vector2)other.transform.position;
                nearestDistanceSquared = Mathf.Min(
                    nearestDistanceSquared,
                    Vector2.SqrMagnitude(candidate - otherPosition));
            }

            return nearestDistanceSquared;
        }

        private static bool IsBetterPatrolCandidate(
            bool hasCandidate,
            float coverageDistanceSquared,
            float travelDistance,
            float bestCoverageDistanceSquared,
            float bestTravelDistance)
        {
            if (!hasCandidate ||
                coverageDistanceSquared >
                bestCoverageDistanceSquared + PatrolScoreTolerance)
            {
                return true;
            }

            return Mathf.Abs(
                       coverageDistanceSquared -
                       bestCoverageDistanceSquared) <=
                   PatrolScoreTolerance &&
                   travelDistance > bestTravelDistance;
        }

        private void RecordRecentPatrolDestination(Vector2 destination)
        {
            _recentPatrolDestinations.Remove(destination);
            _recentPatrolDestinations.Add(destination);
            while (_recentPatrolDestinations.Count >
                   _config.PatrolRecentDestinationCount)
            {
                _recentPatrolDestinations.RemoveAt(0);
            }
        }

        private void ReleasePatrolDestination()
        {
            if (!_hasReservedPatrolDestination)
            {
                return;
            }

            MonsterPatrolReservation.Release(
                _reservedPatrolDestination,
                this);
            _hasReservedPatrolDestination = false;
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
            ClearForcedNoiseResponse();
            SetDestination(searchPosition, _config.PatrolSpeed);
            _stateEndsAt = Time.time + _config.SearchSeconds;
            SetState(MonsterState.Search);
        }

        private void BeginForcedNoiseResponse(NoiseEventData noise)
        {
            _currentNoise = null;
            _noiseAmbushOrigin = noise.WorldPosition;
            _activeNoiseAmbushRadius =
                _config.GetForcedNoiseAmbushRadius(noise.SourceType);
            _forcedNoiseRoamEndsAt =
                Time.time + _config.ForcedNoiseRoamSeconds;
        }

        private void TickForcedNoiseRoam()
        {
            if (!IsForcedNoiseResponseActive())
            {
                ClearForcedNoiseResponse();
                MoveToNextPatrolPoint();
                return;
            }

            if (TryEnterChase(useNoiseAmbushRadius: true))
            {
                return;
            }

            if (_pathFailed || HasReachedDestination())
            {
                TrySetNextForcedNoiseRoamDestination();
            }
        }

        private void EnterForcedNoiseRoam()
        {
            if (!IsForcedNoiseResponseActive())
            {
                ClearForcedNoiseResponse();
                MoveToNextPatrolPoint();
                return;
            }

            _biteController.Cancel();
            _currentNoise = null;
            LastDetectionType = MonsterDetectionType.None;
            _isNoiseAmbushChase = false;
            _isForcedNoiseRoam = true;
            _stateEndsAt = _forcedNoiseRoamEndsAt;
            SetState(MonsterState.Search);
            TrySetNextForcedNoiseRoamDestination();
        }

        private void EnterSearchAfterForcedNoise(Vector3 searchPosition)
        {
            if (IsForcedNoiseResponseActive())
            {
                EnterForcedNoiseRoam();
                return;
            }

            EnterSearch(searchPosition);
        }

        private bool TrySetNextForcedNoiseRoamDestination()
        {
            if (!_navigationGraph.TryGetRoamDestination(
                    _body.position,
                    _noiseAmbushOrigin,
                    _activeNoiseAmbushRadius,
                    StoppingDistance * 2f,
                    out var destination))
            {
                StopMovement();
                return false;
            }

            return SetDestination(destination, _config.ChaseSpeed);
        }

        private bool IsForcedNoiseResponseActive()
        {
            return _activeNoiseAmbushRadius > 0f &&
                   Time.time < _forcedNoiseRoamEndsAt;
        }

        private void ClearForcedNoiseResponse()
        {
            _activeNoiseAmbushRadius = 0f;
            _forcedNoiseRoamEndsAt = 0f;
            _isForcedNoiseRoam = false;
            _isNoiseAmbushChase = false;
        }

        private bool SetDestination(
            Vector3 destination,
            float speed,
            bool resetMovementWatchdog = true)
        {
            _activeDestination = destination;
            _hasActiveDestination = true;
            _pathFailed = !_navigationGraph.TryBuildPath(
                _body.position,
                destination,
                _path,
                out _);
            _pathIndex = 0;
            _hasPath = !_pathFailed && _path.Count > 0;
            _currentSpeed = speed;
            if (resetMovementWatchdog)
            {
                _pathRecoveryAttempts = 0;
                ResetMovementWatchdog();
            }

            return !_pathFailed;
        }

        private void SetChaseDestination(Vector3 destination, float speed)
        {
            _activeDestination = destination;
            _hasActiveDestination = true;
            var shouldResetMovementWatchdog =
                State != MonsterState.Chase || !_hasPath || _pathFailed;
            if (!_senses.HasClearPathToTarget())
            {
                SetDestination(
                    destination,
                    speed,
                    shouldResetMovementWatchdog);
                return;
            }

            _path.Clear();
            _path.Add(destination);
            _pathIndex = 0;
            _pathFailed = false;
            _hasPath = true;
            _currentSpeed = speed;
            if (shouldResetMovementWatchdog)
            {
                ResetMovementWatchdog();
            }
        }

        private Vector2 ResolveMovementDirection(Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude <= Mathf.Epsilon ||
                _config.MovementSeparationRadiusMeters <= 0f ||
                _config.MovementSeparationWeight <= 0f)
            {
                return desiredDirection.normalized;
            }

            var desired = desiredDirection.normalized;
            var separation = Vector2.zero;
            var separationRadius =
                _config.MovementSeparationRadiusMeters;
            var separationRadiusSquared =
                separationRadius * separationRadius;
            foreach (var other in ActiveBrainSet)
            {
                if (other == null || other == this ||
                    !other.isActiveAndEnabled)
                {
                    continue;
                }

                var offset = _body.position - (Vector2)other.transform.position;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= separationRadiusSquared)
                {
                    continue;
                }

                if (distanceSquared <= Mathf.Epsilon)
                {
                    var side = GetInstanceID() < other.GetInstanceID()
                        ? -1f
                        : 1f;
                    offset = new Vector2(-desired.y, desired.x) * side;
                    distanceSquared = 0f;
                }

                var distance = Mathf.Sqrt(distanceSquared);
                var strength = 1f - distance / separationRadius;
                separation += offset.normalized * strength;
            }

            if (separation.sqrMagnitude <= Mathf.Epsilon)
            {
                return desired;
            }

            var steering = desired +
                           Vector2.ClampMagnitude(separation, 1f) *
                           _config.MovementSeparationWeight;
            return steering.sqrMagnitude > Mathf.Epsilon
                ? steering.normalized
                : desired;
        }

        private bool HasMovementStalled()
        {
            if (Vector2.Distance(_body.position, _lastMovementPosition) >
                StoppingDistance)
            {
                _pathRecoveryAttempts = 0;
                ResetMovementWatchdog();
                return false;
            }

            return Time.time - _lastMovementProgressAt >=
                   _config.PathStallSeconds;
        }

        private bool TryRecoverBlockedPath()
        {
            if (!_hasActiveDestination ||
                _pathRecoveryAttempts >= _config.PathRecoveryAttemptLimit)
            {
                return false;
            }

            _pathRecoveryAttempts++;
            _pathFailed = !_navigationGraph.TryBuildPath(
                _body.position,
                _activeDestination,
                _path,
                out _);
            _pathIndex = 0;
            _hasPath = !_pathFailed && _path.Count > 0;
            ResetMovementWatchdog();

            if (_hasPath)
            {
                Debug.Log(
                    $"[Monster] id={name} replanned blocked path " +
                    $"attempt={_pathRecoveryAttempts}/" +
                    $"{_config.PathRecoveryAttemptLimit}.",
                    this);
            }

            return _hasPath;
        }

        private void ResetMovementWatchdog()
        {
            if (_body == null)
            {
                return;
            }

            _lastMovementPosition = _body.position;
            _lastMovementProgressAt = Time.time;
        }

        private void StopMovement()
        {
            _hasPath = false;
            _hasActiveDestination = false;
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
            return !_pathFailed &&
                   (!_hasPath || HasReachedDistributedNoiseApproach());
        }

        /// <summary>
        /// 같은 소음을 조사하는 선두가 현장에 먼저 도착하면 후속 원숭이는
        /// 분리 반경 안의 각자 위치를 도착점으로 인정한다. 감지 원점은 실제
        /// 소음 위치를 유지하므로 판정 범위는 바뀌지 않고 시각적 포개짐만 막는다.
        /// </summary>
        private bool HasReachedDistributedNoiseApproach()
        {
            if (State != MonsterState.InvestigateNoise ||
                !_currentNoise.HasValue ||
                !_hasActiveDestination ||
                _pathIndex < _path.Count - 1)
            {
                return false;
            }

            var separationRadius =
                _config.MovementSeparationRadiusMeters;
            if (separationRadius <= 0f ||
                Vector2.Distance(_body.position, _activeDestination) >
                separationRadius)
            {
                return false;
            }

            var noiseId = _currentNoise.Value.NoiseId;
            var separationRadiusSquared =
                separationRadius * separationRadius;
            foreach (var other in ActiveBrainSet)
            {
                if (other == null || other == this ||
                    other._lastInvestigatedNoiseId != noiseId ||
                    Vector2.SqrMagnitude(
                        (Vector2)other.transform.position -
                        _activeDestination) > separationRadiusSquared)
                {
                    continue;
                }

                return true;
            }

            return false;
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
