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
    ///
    /// 2D 탑다운이므로 시야 각도는 진행 방향 기준 부채꼴이다.
    /// </summary>
    public sealed class MonsterSenses : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;

        [Tooltip("시야를 가리는 장애물 레이어 (타일맵 벽)")]
        [SerializeField] private LayerMask _obstacleMask;

        [Tooltip("현재 적용 중인 후각 강화 단계 (0~2)")]
        [SerializeField] private int _smellLevel;

        /// <summary>바라보는 방향. MonsterBrain이 이동 방향으로 갱신한다.</summary>
        public Vector2 Facing { get; set; } = Vector2.down;

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
        public bool CanDetect(Transform target) => CanSmell(target) || CanSee(target);

        /// <summary>후각: 반경 안이면 벽 너머도 감지한다.</summary>
        public bool CanSmell(Transform target)
        {
            if (target == null || _balance == null)
            {
                return false;
            }

            float radius = SmellRadius;
            return ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude
                   <= radius * radius;
        }

        /// <summary>시야: 거리·각도·장애물을 모두 통과해야 한다.</summary>
        public bool CanSee(Transform target)
        {
            if (target == null || _balance == null)
            {
                return false;
            }

            Vector2 origin = transform.position;
            Vector2 toTarget = (Vector2)target.position - origin;

            float distance = toTarget.magnitude;
            if (distance > _balance.SightRange || distance < 0.0001f)
            {
                return false;
            }

            float halfAngle = _balance.SightAngleDegrees * 0.5f;
            if (Vector2.Angle(Facing, toTarget) > halfAngle)
            {
                return false;
            }

            // 장애물이 가로막으면 보이지 않는다.
            RaycastHit2D hit = Physics2D.Raycast(
                origin, toTarget.normalized, distance, _obstacleMask);

            return hit.collider == null;
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
            Vector3 left = Quaternion.Euler(0f, 0f, -half) * (Vector3)Facing;
            Vector3 right = Quaternion.Euler(0f, 0f, half) * (Vector3)Facing;

            Gizmos.DrawRay(transform.position, left * _balance.SightRange);
            Gizmos.DrawRay(transform.position, right * _balance.SightRange);
        }
    }
}
