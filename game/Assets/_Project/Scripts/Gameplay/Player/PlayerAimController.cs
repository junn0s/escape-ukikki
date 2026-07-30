using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private PlayerMovementConfig _config;

        private bool _canAim = true;
        private float _targetAngle;

        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public float AimAngleDegrees { get; private set; }
        public Camera WorldCamera => _worldCamera;

        public void Configure(
            PlayerInputReader input,
            Camera worldCamera,
            PlayerMovementConfig config)
        {
            _input = input;
            _worldCamera = worldCamera;
            _config = config;
        }

        public void SetWorldCamera(Camera worldCamera)
        {
            _worldCamera = worldCamera;
        }

        public void SetAimingEnabled(bool isEnabled)
        {
            _canAim = isEnabled;
        }

        private void Awake()
        {
            _targetAngle = 0f;
            AimAngleDegrees = 0f;
            AimDirection = Vector2.up;
        }

        private void Update()
        {
            if (!_canAim || _input == null || _config == null)
            {
                return;
            }

            if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
            {
                _worldCamera = Camera.main;
            }

            if (_worldCamera == null)
            {
                return;
            }

            var pointerWorld = _worldCamera.ScreenToWorldPoint(
                new Vector3(
                    _input.PointerPosition.x,
                    _input.PointerPosition.y,
                    Mathf.Abs(_worldCamera.transform.position.z)));
            var direction = (Vector2)(pointerWorld - transform.position);
            if (direction.sqrMagnitude >= 0.01f)
            {
                _targetAngle = Vector2.SignedAngle(
                    Vector2.up,
                    direction.normalized);
            }

            AimAngleDegrees = Mathf.MoveTowardsAngle(
                AimAngleDegrees,
                _targetAngle,
                _config.RotationSpeedDegrees * Time.deltaTime);
            AimDirection = Quaternion.Euler(
                0f,
                0f,
                AimAngleDegrees) * Vector2.up;
        }
    }
}
