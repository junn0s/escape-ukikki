using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private PlayerMovementConfig _config;

        private float _verticalVelocity;
        private bool _canMove = true;

        public Vector3 HorizontalVelocity { get; private set; }
        public bool IsMovementEnabled => _canMove;

        public void Configure(
            PlayerInputReader input,
            CharacterController controller,
            PlayerMovementConfig config)
        {
            _input = input;
            _controller = controller;
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
            _controller ??= GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_input == null || _controller == null || _config == null)
            {
                return;
            }

            var input = _canMove ? Vector2.ClampMagnitude(_input.Move, 1f) : Vector2.zero;
            var direction = new Vector3(input.x, 0f, input.y);
            HorizontalVelocity = direction * _config.MoveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity -= _config.Gravity * Time.deltaTime;
            var velocity = HorizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
