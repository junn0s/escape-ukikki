using MonkeyLab.Gameplay.Infection;
using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    /// <summary>
    /// 유령 이동이다. 유령은 벽을 통과하지만 맵 밖으로 나갈 수 없다(GDD §17).
    ///
    /// 살아 있을 때는 아무것도 하지 않고 평소 이동을 그대로 둔다.
    /// 유령이 되면 콜라이더를 트리거로 바꿔 벽을 통과시키고,
    /// 맵 경계로 위치를 제한한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GhostMovementController : MonoBehaviour
    {
        [SerializeField] private InfectionService _infectionService;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private PlayerMovementConfig _config;
        [SerializeField] private Rect _mapBounds =
            new(-42.5f, -25f, 80.5f, 57f);

        private bool _wasGhost;
        private bool _originalIsTrigger;

        public bool IsGhost =>
            _infectionService != null &&
            _infectionService.State == PlayerLifeState.DeadGhost;

        public Rect MapBounds => _mapBounds;

        public void Configure(
            InfectionService infectionService,
            Rigidbody2D body,
            Collider2D playerCollider,
            PlayerMovementConfig config,
            Rect mapBounds)
        {
            _infectionService = infectionService;
            _body = body;
            _collider = playerCollider;
            _config = config;
            _mapBounds = mapBounds;
        }

        /// <summary>맵 경계 안으로 위치를 제한한다.</summary>
        public Vector2 ClampToMap(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, _mapBounds.xMin, _mapBounds.xMax),
                Mathf.Clamp(position.y, _mapBounds.yMin, _mapBounds.yMax));
        }

        private void Awake()
        {
            _body ??= GetComponent<Rigidbody2D>();
            _collider ??= GetComponent<Collider2D>();
            if (_collider != null)
            {
                _originalIsTrigger = _collider.isTrigger;
            }
        }

        private void FixedUpdate()
        {
            var isGhost = IsGhost;
            if (isGhost != _wasGhost)
            {
                ApplyGhostState(isGhost);
                _wasGhost = isGhost;
            }

            if (!isGhost || _body == null)
            {
                return;
            }

            var clamped = ClampToMap(_body.position);
            if (clamped != _body.position)
            {
                _body.position = clamped;
            }
        }

        private void ApplyGhostState(bool isGhost)
        {
            if (_collider != null)
            {
                // 트리거로 바꾸면 벽과 충돌하지 않는다.
                _collider.isTrigger = isGhost || _originalIsTrigger;
            }
        }
    }
}
