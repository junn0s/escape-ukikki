using UnityEngine;

namespace MonkeyLab.Presentation.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0f, 0f, -10f);
        [SerializeField, Min(1f)] private float _orthographicSize = 9f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.12f;

        private Vector3 _velocity;
        private UnityEngine.Camera _camera;

        public void Configure(
            Transform target,
            float orthographicSize,
            float smoothTime)
        {
            var shouldSnapToTarget = _target != target;
            _target = target;
            _orthographicSize = orthographicSize;
            _smoothTime = smoothTime;
            ApplyCameraSettings();
            if (shouldSnapToTarget)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                _target.position + _offset,
                Quaternion.identity);
            _velocity = Vector3.zero;
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            ApplyCameraSettings();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            var desiredPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _velocity,
                _smoothTime);
            transform.rotation = Quaternion.identity;
        }

        private void ApplyCameraSettings()
        {
            _camera ??= GetComponent<UnityEngine.Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 100f;
        }
    }
}
