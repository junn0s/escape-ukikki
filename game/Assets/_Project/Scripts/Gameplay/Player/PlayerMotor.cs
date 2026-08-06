using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private PlayerMovementConfig _config;
        [SerializeField] private GhostMovementController _ghostMovement;

        private bool _canMove = true;
        private bool _isCarryingBattery;

        public Vector3 HorizontalVelocity { get; private set; }
        public bool IsMovementEnabled => _canMove;
        public bool IsCarryingBattery => _isCarryingBattery;

        public void Configure(
            PlayerInputReader input,
            Rigidbody2D body,
            PlayerMovementConfig config)
        {
            _input = input;
            _body = body;
            _config = config;
        }

        public void SetGhostMovement(GhostMovementController ghostMovement)
        {
            _ghostMovement = ghostMovement;
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            _canMove = isEnabled;
            if (!isEnabled)
            {
                HorizontalVelocity = Vector3.zero;
            }
        }

        public void SetBatteryCarrying(bool isCarrying)
        {
            _isCarryingBattery = isCarrying;
        }

        private void Awake()
        {
            _body ??= GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (_input == null || _body == null || _config == null)
            {
                return;
            }

            var input = _canMove ? Vector2.ClampMagnitude(_input.Move, 1f) : Vector2.zero;
            // 유령은 별도 속도를 쓴다(balance-and-telemetry.md §3).
            var isGhost = _ghostMovement != null && _ghostMovement.IsGhost;
            var speed = isGhost
                ? _config.GhostMoveSpeed
                : _isCarryingBattery
                    ? _config.BatteryCarryMoveSpeed
                    : _config.MoveSpeed;
            var velocity = input * speed;
            HorizontalVelocity = new Vector3(velocity.x, velocity.y, 0f);
            var nextPosition =
                _body.position + velocity * Time.fixedDeltaTime;
            if (isGhost)
            {
                // GhostMovementController의 FixedUpdate 순서와 무관하게
                // 이번 물리 틱의 목표부터 외곽 결계 안으로 제한한다.
                nextPosition = _ghostMovement.ClampToMap(nextPosition);
            }

            _body.MovePosition(nextPosition);
        }
    }
}
