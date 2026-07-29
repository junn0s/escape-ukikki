using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private PlayerMovementConfig _config;

        private bool _canAim = true;

        public void Configure(PlayerInputReader input, Camera worldCamera, PlayerMovementConfig config)
        {
            _input = input;
            _worldCamera = worldCamera;
            _config = config;
        }

        public void SetAimingEnabled(bool isEnabled)
        {
            _canAim = isEnabled;
        }

        private void Update()
        {
            if (!_canAim || _input == null || _config == null)
            {
                return;
            }

            _worldCamera ??= Camera.main;
            if (_worldCamera == null)
            {
                return;
            }

            var ray = _worldCamera.ScreenPointToRay(_input.PointerPosition);
            var groundPlane = new Plane(Vector3.up, transform.position);
            if (!groundPlane.Raycast(ray, out var distance))
            {
                return;
            }

            var direction = ray.GetPoint(distance) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _config.RotationSpeedDegrees * Time.deltaTime);
        }
    }
}
