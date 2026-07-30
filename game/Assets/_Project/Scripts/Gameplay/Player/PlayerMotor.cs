using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private PlayerMovementConfig _config;

        private bool _canMove = true;

        public Vector3 HorizontalVelocity { get; private set; }
        public bool IsMovementEnabled => _canMove;

        public void Configure(
            PlayerInputReader input,
            Rigidbody2D body,
            PlayerMovementConfig config)
        {
            _input = input;
            _body = body;
            _config = config;
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            _canMove = isEnabled;
            if (!isEnabled)
            {
                HorizontalVelocity = Vector3.zero;
            }
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
            var velocity = input * _config.MoveSpeed;
            HorizontalVelocity = new Vector3(velocity.x, velocity.y, 0f);
            _body.MovePosition(_body.position + velocity * Time.fixedDeltaTime);
        }
    }
}
