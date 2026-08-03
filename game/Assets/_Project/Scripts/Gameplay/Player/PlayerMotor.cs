using MonkeyLab.Gameplay.Monsters;
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
        [SerializeField] private MonsterTarget _monsterTarget;
        [SerializeField] private bool _shouldReportMovementAudibility = true;

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

        public void BindMonsterTarget(MonsterTarget monsterTarget)
        {
            _monsterTarget = monsterTarget;
        }

        public void SetMovementAudibilityReporting(bool isEnabled)
        {
            _shouldReportMovementAudibility = isEnabled;
            if (!isEnabled)
            {
                _monsterTarget?.SetMovingAudibly(false);
            }
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            _canMove = isEnabled;
            if (!isEnabled)
            {
                HorizontalVelocity = Vector3.zero;
                if (_shouldReportMovementAudibility)
                {
                    _monsterTarget?.SetMovingAudibly(false);
                }
            }
        }

        public void SetBatteryCarrying(bool isCarrying)
        {
            _isCarryingBattery = isCarrying;
        }

        private void Awake()
        {
            _body ??= GetComponent<Rigidbody2D>();
            _monsterTarget ??= GetComponent<MonsterTarget>();
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
            if (_shouldReportMovementAudibility)
            {
                _monsterTarget?.SetMovingAudibly(
                    !isGhost && input.sqrMagnitude > 0.01f);
            }
            var speed = isGhost
                ? _config.GhostMoveSpeed
                : _isCarryingBattery
                    ? _config.BatteryCarryMoveSpeed
                    : _config.MoveSpeed;
            var velocity = input * speed;
            HorizontalVelocity = new Vector3(velocity.x, velocity.y, 0f);
            _body.MovePosition(_body.position + velocity * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            if (_shouldReportMovementAudibility)
            {
                _monsterTarget?.SetMovingAudibly(false);
            }
        }
    }
}
