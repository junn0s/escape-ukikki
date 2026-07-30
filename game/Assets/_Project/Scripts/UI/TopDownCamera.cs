using UnityEngine;

namespace MonkeyLab.Presentation
{
    /// <summary>
    /// 2D 직교 탑다운 카메라. 플레이어를 따라가며 회전·확대하지 않는다.
    /// GDD §7.1 / map-level-design.md §10
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("직교 크기. 화면 절반 높이를 월드 단위로 나타낸다. 방 하나가 들어오는 값")]
        [SerializeField] private float _orthographicSize = 8f;

        [Tooltip("추적 부드러움. 작을수록 빠르게 따라온다")]
        [SerializeField] private float _followSmoothTime = 0.12f;

        private Camera _camera;
        private Vector3 _velocity;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            // Z는 카메라 깊이로 유지한다. 2D에서 Z를 건드리면 스프라이트가 잘린다.
            var desired = new Vector3(_target.position.x, _target.position.y, transform.position.z);

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _followSmoothTime);
        }

        /// <summary>씬 시작 시 보간 없이 즉시 배치한다.</summary>
        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = new Vector3(
                _target.position.x, _target.position.y, transform.position.z);
            _velocity = Vector3.zero;
        }
    }
}
