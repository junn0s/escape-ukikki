using UnityEngine;
using UnityEngine.AI;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterSenses : MonoBehaviour
    {
        private const float EyeHeight = 1.1f;
        private const float TargetCenterHeight = 0.9f;
        private const float NavMeshSampleDistance = 1.5f;

        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private MonsterTierRuntime _tierRuntime;
        [SerializeField] private MonsterTarget _target;
        [SerializeField] private LayerMask _visionBlockingMask = Physics.DefaultRaycastLayers;

        public MonsterTarget Target => _target;
        public MonsterTierRuntime TierRuntime => _tierRuntime;

        public void Configure(
            MonsterBalanceConfig config,
            MonsterTierRuntime tierRuntime,
            MonsterTarget target,
            LayerMask visionBlockingMask)
        {
            _config = config;
            _tierRuntime = tierRuntime;
            _target = target;
            _visionBlockingMask = visionBlockingMask;
        }

        public bool TryDetectTarget(out MonsterDetectionType detectionType)
        {
            detectionType = MonsterDetectionType.None;
            if (_config == null || _tierRuntime == null || _target == null || !_target.IsDetectable)
            {
                return false;
            }

            if (HasSight())
            {
                detectionType = MonsterDetectionType.Sight;
                return true;
            }

            if (HasSmell())
            {
                detectionType = MonsterDetectionType.Smell;
                return true;
            }

            return false;
        }

        public bool TryDetectTargetAtCloseRange(out MonsterDetectionType detectionType)
        {
            detectionType = MonsterDetectionType.None;
            if (_config == null || _tierRuntime == null || _target == null ||
                !_target.IsDetectable || !HasSmell())
            {
                return false;
            }

            detectionType = MonsterDetectionType.Smell;
            return true;
        }

        public bool IsTargetInBiteRangeWithLineOfSight()
        {
            return _config != null && _target != null && _target.IsDetectable &&
                   MonsterPerceptionRules.IsWithinRadius(
                       transform.position,
                       _target.transform.position,
                       _config.BiteDistance) &&
                   HasClearLineOfSight();
        }

        public bool HasClearLineOfSight()
        {
            if (_target == null)
            {
                return false;
            }

            var origin = transform.position + Vector3.up * EyeHeight;
            var targetPosition = _target.transform.position + Vector3.up * TargetCenterHeight;
            var direction = targetPosition - origin;
            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            if (!Physics.Raycast(
                    origin,
                    direction / distance,
                    out var hit,
                    distance,
                    _visionBlockingMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.transform == _target.transform || hit.transform.IsChildOf(_target.transform);
        }

        private bool HasSight()
        {
            return MonsterPerceptionRules.IsWithinVisionCone(
                       transform.forward,
                       _target.transform.position - transform.position,
                       _config.VisionDistance,
                       _config.VisionAngleDegrees) &&
                   HasClearLineOfSight();
        }

        private bool HasSmell()
        {
            if (!MonsterPerceptionRules.IsWithinRadius(
                    transform.position,
                    _target.transform.position,
                    _tierRuntime.CurrentSmellRadius))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(
                    transform.position,
                    out var sourceHit,
                    NavMeshSampleDistance,
                    NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(
                    _target.transform.position,
                    out var targetHit,
                    NavMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            return NavMesh.CalculatePath(
                       sourceHit.position,
                       targetHit.position,
                       NavMesh.AllAreas,
                       path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
