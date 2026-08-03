using UnityEngine;
using MonkeyLab.Presentation.Settings;

namespace MonkeyLab.Presentation.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        private const float TraumaDecayPerSecond = 1.8f;
        private const float MaximumShakeDistance = 0.34f;
        private const float ShakeFrequency = 18f;

        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0f, 0f, -10f);
        [SerializeField, Min(1f)] private float _orthographicSize = 9f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.12f;
        [SerializeField, Min(0f)] private float _lookAheadDistance = 1.15f;
        [SerializeField, Min(0.01f)] private float _lookAheadSmoothTime = 0.18f;

        private Vector3 _velocity;
        private Vector3 _followPosition;
        private Vector3 _lookAhead;
        private Vector3 _lookAheadVelocity;
        private Vector3 _previousTargetPosition;
        private float _trauma;
        private UnityEngine.Camera _camera;

        public Transform Target => _target;

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

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

        public void SetTarget(Transform target, bool shouldSnap)
        {
            _target = target;
            if (shouldSnap)
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
            _followPosition = transform.position;
            _previousTargetPosition = _target.position;
            _lookAhead = Vector3.zero;
            _lookAheadVelocity = Vector3.zero;
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

            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var targetDelta = _target.position - _previousTargetPosition;
            var movementDirection = targetDelta.sqrMagnitude > 0.0001f
                ? targetDelta.normalized
                : Vector3.zero;
            var desiredLookAhead = movementDirection * _lookAheadDistance;
            _lookAhead = Vector3.SmoothDamp(
                _lookAhead,
                desiredLookAhead,
                ref _lookAheadVelocity,
                _lookAheadSmoothTime,
                Mathf.Infinity,
                deltaTime);

            var desiredPosition = _target.position + _offset + _lookAhead;
            _followPosition = Vector3.SmoothDamp(
                _followPosition,
                desiredPosition,
                ref _velocity,
                _smoothTime,
                Mathf.Infinity,
                deltaTime);

            var shakeStrength = _trauma * _trauma * MaximumShakeDistance *
                                LocalGameSettings.ScreenShakeIntensity;
            var noiseTime = Time.unscaledTime * ShakeFrequency;
            var shakeOffset = new Vector3(
                (Mathf.PerlinNoise(noiseTime, 0.37f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.73f, noiseTime) - 0.5f) * 2f,
                0f) * shakeStrength;
            transform.position = _followPosition + shakeOffset;
            transform.rotation = Quaternion.identity;
            _trauma = Mathf.MoveTowards(
                _trauma,
                0f,
                TraumaDecayPerSecond * deltaTime);
            _previousTargetPosition = _target.position;
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
