using UnityEngine;
using MonkeyLab.Presentation.Settings;

namespace MonkeyLab.Presentation.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        public const float DefaultOrthographicSize = 9f;

        private const float TraumaDecayPerSecond = 1.8f;
        private const float MaximumShakeDistance = 0.34f;
        private const float ShakeFrequency = 18f;

        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0f, 0f, -10f);
        [SerializeField, Min(1f)]
        private float _orthographicSize = DefaultOrthographicSize;

        private float _trauma;
        private UnityEngine.Camera _camera;

        public Transform Target => _target;

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

        public void Configure(
            Transform target,
            float orthographicSize)
        {
            var shouldSnapToTarget = _target != target;
            _target = target;
            _orthographicSize = orthographicSize;
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

            var shakeStrength = _trauma * _trauma * MaximumShakeDistance *
                                LocalGameSettings.ScreenShakeIntensity;
            var noiseTime = Time.unscaledTime * ShakeFrequency;
            var shakeOffset = new Vector3(
                (Mathf.PerlinNoise(noiseTime, 0.37f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.73f, noiseTime) - 0.5f) * 2f,
                0f) * shakeStrength;
            transform.position = _target.position + _offset + shakeOffset;
            transform.rotation = Quaternion.identity;
            _trauma = Mathf.MoveTowards(
                _trauma,
                0f,
                TraumaDecayPerSecond * Time.unscaledDeltaTime);
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
