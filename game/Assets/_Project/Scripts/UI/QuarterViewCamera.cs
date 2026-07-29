using UnityEngine;

namespace MonkeyLab.Presentation
{
    /// <summary>
    /// 고정 각도 쿼터뷰 카메라. 플레이어를 부드럽게 따라가며 회전하지 않는다.
    /// GDD §7.1
    /// </summary>
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("내려다보는 각도. GDD 기준 50~60도")]
        [Range(40f, 70f)]
        [SerializeField] private float _pitchDegrees = 55f;

        [Tooltip("수평 회전. 플레이어가 바꿀 수 없다")]
        [SerializeField] private float _yawDegrees = 45f;

        [Tooltip("타깃과의 거리")]
        [SerializeField] private float _distance = 12f;

        [Tooltip("추적 부드러움. 작을수록 빠르게 따라온다")]
        [SerializeField] private float _followSmoothTime = 0.15f;

        [Tooltip("캐릭터 발밑이 아니라 상체를 기준으로 잡기 위한 높이")]
        [SerializeField] private float _targetHeightOffset = 1.0f;

        private Vector3 _velocity;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
            Vector3 focus = _target.position + Vector3.up * _targetHeightOffset;
            Vector3 desired = focus - rotation * Vector3.forward * _distance;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _followSmoothTime);

            // 방 진입 시 각도가 급변하지 않도록 회전은 고정값을 그대로 쓴다 (GDD §7.1).
            transform.rotation = rotation;
        }

        /// <summary>씬 시작 시 보간 없이 즉시 배치한다.</summary>
        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
            Vector3 focus = _target.position + Vector3.up * _targetHeightOffset;

            transform.position = focus - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;
            _velocity = Vector3.zero;
        }
    }
}
