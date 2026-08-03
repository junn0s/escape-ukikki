using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterSenses : MonoBehaviour
    {
        private const int MaximumBitePathHits = 16;
        private static readonly HashSet<Collider2D> MonsterColliderSet = new();

        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private MonsterTierRuntime _tierRuntime;
        [SerializeField] private MonsterTarget _target;
        [SerializeField] private TopDownNavigationGraph _navigationGraph;
        [SerializeField] private Collider2D _bodyCollider;
        [SerializeField] private LayerMask _biteBlockingMask =
            Physics2D.DefaultRaycastLayers;

        private readonly RaycastHit2D[] _bitePathHits =
            new RaycastHit2D[MaximumBitePathHits];
        private Vector2 _facingDirection = Vector2.down;
        private Collider2D _targetCollider;

        public MonsterTarget Target => _target;
        public MonsterTierRuntime TierRuntime => _tierRuntime;
        public Vector2 FacingDirection => _facingDirection;
        public Collider2D LastPathBlocker { get; private set; }

        public void Configure(
            MonsterBalanceConfig config,
            MonsterTierRuntime tierRuntime,
            MonsterTarget target,
            LayerMask biteBlockingMask,
            TopDownNavigationGraph navigationGraph = null)
        {
            _config = config;
            _tierRuntime = tierRuntime;
            _target = target;
            _biteBlockingMask = biteBlockingMask;
            _navigationGraph = navigationGraph;
            _bodyCollider ??= GetComponent<Collider2D>();
            RegisterBodyCollider();
            _targetCollider = _target != null
                ? _target.GetComponent<Collider2D>()
                : null;
        }

        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            _facingDirection = direction.normalized;
        }

        public bool TryDetectTarget(out MonsterDetectionType detectionType)
        {
            return TrySelectTarget(
                transform.position,
                _tierRuntime != null
                    ? _tierRuntime.CurrentProximityDetectionRadius
                    : 0f,
                MonsterDetectionType.Proximity,
                out detectionType);
        }

        public bool TryDetectTargetAtCloseRange(out MonsterDetectionType detectionType)
        {
            return TryDetectTarget(out detectionType);
        }

        public bool TryDetectTargetNearPosition(
            Vector3 detectionOrigin,
            float radius,
            out MonsterDetectionType detectionType)
        {
            return TrySelectTarget(
                detectionOrigin,
                radius,
                MonsterDetectionType.NoiseAmbush,
                out detectionType);
        }

        public bool IsTargetInBiteRange(MonsterTarget target = null)
        {
            target ??= _target;
            return _config != null && target != null && target.IsDetectable &&
                   IsTargetInBiteReach(target) &&
                   HasClearPathToTarget(target);
        }

        public bool HasClearPathToTarget(MonsterTarget target = null)
        {
            LastPathBlocker = null;
            target ??= _target;
            if (target == null)
            {
                return false;
            }

            var origin = (Vector2)transform.position;
            var targetPosition = (Vector2)target.transform.position;
            if (Vector2.SqrMagnitude(targetPosition - origin) <= Mathf.Epsilon)
            {
                return true;
            }

            var filter = new ContactFilter2D
            {
                useTriggers = true
            };
            filter.SetLayerMask(_biteBlockingMask);
            var hitCount = Physics2D.Linecast(
                origin,
                targetPosition,
                filter,
                _bitePathHits);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _bitePathHits[index];
                var hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger ||
                    hitCollider.transform == transform ||
                    hitCollider.transform.IsChildOf(transform) ||
                    MonsterColliderSet.Contains(hitCollider))
                {
                    continue;
                }

                if (hitCollider.transform == target.transform ||
                    hitCollider.transform.IsChildOf(target.transform))
                {
                    return true;
                }

                LastPathBlocker = hitCollider;
                return false;
            }

            return true;
        }

        private bool TrySelectTarget(
            Vector3 detectionOrigin,
            float radius,
            MonsterDetectionType detectionType,
            out MonsterDetectionType selectedDetectionType)
        {
            selectedDetectionType = MonsterDetectionType.None;
            if (_config == null || radius <= 0f)
            {
                return false;
            }

            MonsterTarget selectedTarget = null;
            var bestSqrDistance = float.PositiveInfinity;
            var bestInstanceId = int.MaxValue;
            foreach (var candidate in MonsterTarget.ActiveTargets)
            {
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    !candidate.CanBeDetectedBy(detectionType) ||
                    !MonsterPerceptionRules.IsWithinRadius(
                        detectionOrigin,
                        candidate.transform.position,
                        radius) ||
                    (_navigationGraph != null &&
                     !_navigationGraph.TryGetPathDistance(
                         transform.position,
                         candidate.transform.position,
                         out _)))
                {
                    continue;
                }

                var sqrDistance = (
                    candidate.transform.position - detectionOrigin).sqrMagnitude;
                var instanceId = candidate.GetInstanceID();
                if (sqrDistance > bestSqrDistance ||
                    (Mathf.Approximately(sqrDistance, bestSqrDistance) &&
                     instanceId >= bestInstanceId))
                {
                    continue;
                }

                selectedTarget = candidate;
                bestSqrDistance = sqrDistance;
                bestInstanceId = instanceId;
            }

            if (selectedTarget == null)
            {
                return false;
            }

            _target = selectedTarget;
            _targetCollider = selectedTarget.GetComponent<Collider2D>();
            selectedDetectionType = detectionType;
            return true;
        }

        private bool IsTargetInBiteReach(MonsterTarget target)
        {
            _bodyCollider ??= GetComponent<Collider2D>();
            if (_target != target || _targetCollider == null)
            {
                _target = target;
                _targetCollider = target.GetComponent<Collider2D>();
            }

            if (_bodyCollider == null || _targetCollider == null)
            {
                return MonsterPerceptionRules.IsWithinRadius(
                    transform.position,
                    target.transform.position,
                    _config.BiteDistance);
            }

            return _bodyCollider.Distance(_targetCollider).distance <=
                   _config.BiteDistance;
        }

        private void OnEnable()
        {
            _bodyCollider ??= GetComponent<Collider2D>();
            RegisterBodyCollider();
        }

        private void OnDisable()
        {
            if (_bodyCollider != null)
            {
                MonsterColliderSet.Remove(_bodyCollider);
            }
        }

        private void RegisterBodyCollider()
        {
            if (_bodyCollider == null)
            {
                return;
            }

            // 같은 추적 대상이나 좁은 복도에서 괴물끼리 서로 밀어 경로를
            // 영구 차단하지 않게 한다. 벽과 플레이어 충돌은 그대로 유지한다.
            foreach (var otherCollider in MonsterColliderSet)
            {
                if (otherCollider == null || otherCollider == _bodyCollider)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    _bodyCollider,
                    otherCollider,
                    true);
            }

            MonsterColliderSet.Add(_bodyCollider);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetColliderRegistry()
        {
            MonsterColliderSet.Clear();
        }
    }
}
