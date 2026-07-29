using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// 괴물의 감지 판정. 시야와 후각을 분리해 평가한다.
    ///
    /// 규칙 (docs/system-design-document.md §10.2):
    /// - 시야는 거리, 각도, 장애물 레이캐스트를 모두 통과해야 한다.
    /// - 후각은 반경 안이면 벽 너머도 감지한다.
    /// - 유령은 감지 대상이 아니다.
    /// </summary>
    public sealed class MonsterSenses : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;

        [Tooltip("시야를 가리는 장애물 레이어")]
        [SerializeField] private LayerMask _obstacleMask;

        [Tooltip("눈 높이. 바닥에서 레이를 쏘면 턱에 걸린다")]
        [SerializeField] private float _eyeHeight = 1.2f;

        [Tooltip("현재 적용 중인 후각 강화 단계 (0~2)")]
        [SerializeField] private int _smellLevel;

        public int SmellLevel
        {
            get => _smellLevel;
            set => _smellLevel = Mathf.Clamp(value, 0, 2);
        }

        public float SmellRadius => _balance != null ? _balance.GetSmellRadius(_smellLevel) : 0f;

        private void Awake()
        {
            if (_balance == null)
            {
                Debug.LogError($"[{nameof(MonsterSenses)}] {nameof(_balance)} 미할당", this);
                enabled = false;
            }
        }

        /// <summary>대상을 시야 또는 후각으로 감지했는지.</summary>
        public bool CanDetect(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            return CanSmell(target) || CanSee(target);
        }

        /// <summary>후각: 반경 안이면 벽 너머도 감지한다.</summary>
        public bool CanSmell(Transform target)
        {
            if (target == null || _balance == null)
            {
                return false;
            }

            float radius = SmellRadius;
            return (target.position - transform.position).sqrMagnitude <= radius * radius;
        }

        /// <summary>시야: 거리·각도·장애물을 모두 통과해야 한다.</summary>
        public bool CanSee(Transform target)
        {
            if (target == null || _balance == null)
            {
                return false;
            }

            Vector3 eye = transform.position + Vector3.up * _eyeHeight;
            Vector3 targetPoint = target.position + Vector3.up * _eyeHeight;
            Vector3 toTarget = targetPoint - eye;

            float distance = toTarget.magnitude;
            if (distance > _balance.SightRange)
            {
                return false;
            }

            float halfAngle = _balance.SightAngleDegrees * 0.5f;
            if (Vector3.Angle(transform.forward, toTarget) > halfAngle)
            {
                return false;
            }

            // 장애물이 가로막으면 보이지 않는다.
            if (Physics.Raycast(eye, toTarget.normalized, distance, _obstacleMask))
            {
                return false;
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (_balance == null)
            {
                return;
            }

            // 후각 반경
            Gizmos.color = new Color(0.84f, 0.23f, 0.26f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, SmellRadius);

            // 시야 부채꼴
            Gizmos.color = new Color(0.91f, 0.72f, 0.29f, 0.5f);
            float half = _balance.SightAngleDegrees * 0.5f;
            Vector3 eye = transform.position + Vector3.up * _eyeHeight;
            Vector3 left = Quaternion.Euler(0f, -half, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, half, 0f) * transform.forward;

            Gizmos.DrawRay(eye, left * _balance.SightRange);
            Gizmos.DrawRay(eye, right * _balance.SightRange);
        }
    }
}
