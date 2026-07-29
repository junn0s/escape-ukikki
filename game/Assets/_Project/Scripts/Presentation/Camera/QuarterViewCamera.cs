using UnityEngine;

namespace MonkeyLab.Presentation.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0f, 14f, -11f);
        [SerializeField] private Vector3 _fixedEulerAngles = new(52f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.16f;

        private Vector3 _velocity;

        public void Configure(Transform target, Vector3 offset, float smoothTime)
        {
            _target = target;
            _offset = offset;
            _smoothTime = smoothTime;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = _target.position + _offset;
            transform.rotation = Quaternion.Euler(_fixedEulerAngles);
            _velocity = Vector3.zero;
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
            transform.rotation = Quaternion.Euler(_fixedEulerAngles);
        }
    }
}
