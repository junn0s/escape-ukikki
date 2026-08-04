using UnityEngine;

namespace MonkeyLab.Presentation.Player
{
    /// <summary>
    /// 이동 규칙에는 관여하지 않고 위치 변화만 관찰해 몸체에 보행 리듬을 준다.
    /// 소유자와 원격 플레이어가 같은 표현을 사용한다.
    /// </summary>
    public sealed class PlayerMotionFeel : MonoBehaviour
    {
        private const float MaximumSampledSpeed = 8f;
        private const float MovingThreshold = 0.08f;
        private const float StrideRadiansPerMeter = 2.8f;
        private const float BobDistance = 0.012f;
        private const float SquashAmount = 0.006f;
        private const float LeanDegrees = 1.2f;
        private const float BlendSpeed = 10f;

        /// <summary>
        /// 이 속도 미만의 좌우 이동으로는 방향을 뒤집지 않는다. 수직 이동이나
        /// 제자리 미세 움직임에서 스프라이트가 깜빡이며 뒤집히는 것을 막는다.
        /// </summary>
        private const float FlipThresholdMetersPerSecond = 0.35f;

        [SerializeField] private Transform _movementRoot;
        [SerializeField] private Transform _bodyVisual;
        [SerializeField] private SpriteRenderer[] _flippableSprites =
            System.Array.Empty<SpriteRenderer>();

        private bool _isFacingRight = true;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private Quaternion _baseLocalRotation;
        private Vector3 _previousWorldPosition;
        private float _stepPhase;
        private float _movementBlend;

        public void Configure(
            Transform movementRoot,
            Transform bodyVisual,
            SpriteRenderer[] flippableSprites = null)
        {
            _movementRoot = movementRoot;
            _bodyVisual = bodyVisual;
            _flippableSprites = flippableSprites ??
                System.Array.Empty<SpriteRenderer>();
            CacheBasePose();
        }

        private void Awake()
        {
            CacheBasePose();
        }

        private void OnEnable()
        {
            if (_movementRoot != null)
            {
                _previousWorldPosition = _movementRoot.position;
            }
        }

        private void OnDisable()
        {
            RestoreBasePose();
        }

        private void LateUpdate()
        {
            if (_movementRoot == null || _bodyVisual == null)
            {
                return;
            }

            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var worldDelta =
                _movementRoot.position - _previousWorldPosition;
            var speed = Mathf.Min(
                worldDelta.magnitude / deltaTime,
                MaximumSampledSpeed);
            var targetBlend = speed > MovingThreshold ? 1f : 0f;
            _movementBlend = Mathf.MoveTowards(
                _movementBlend,
                targetBlend,
                BlendSpeed * deltaTime);
            _stepPhase += speed * StrideRadiansPerMeter * deltaTime;

            var wave = Mathf.Sin(_stepPhase) * _movementBlend;
            var compression = Mathf.Abs(Mathf.Cos(_stepPhase)) *
                              _movementBlend * SquashAmount;
            _bodyVisual.localPosition =
                _baseLocalPosition + Vector3.up * wave * BobDistance;
            _bodyVisual.localScale = new Vector3(
                _baseLocalScale.x * (1f + compression),
                _baseLocalScale.y * (1f - compression),
                _baseLocalScale.z);
            var horizontalDirection = worldDelta.sqrMagnitude > 0.0001f
                ? Mathf.Clamp(worldDelta.normalized.x, -1f, 1f)
                : 0f;
            _bodyVisual.localRotation =
                _baseLocalRotation * Quaternion.Euler(
                    0f,
                    0f,
                    -horizontalDirection * LeanDegrees * _movementBlend);
            ApplyFacing(worldDelta.x / deltaTime);
            _previousWorldPosition = _movementRoot.position;
        }

        /// <summary>
        /// 측면 프로필 캐릭터를 이동 방향으로 뒤집는다(아트 가이드 §1.1).
        /// 몸통 회전이 없으므로 좌우 구분은 플립만으로 만든다.
        /// </summary>
        private void ApplyFacing(float horizontalSpeed)
        {
            if (Mathf.Abs(horizontalSpeed) < FlipThresholdMetersPerSecond)
            {
                return;
            }

            var shouldFaceRight = horizontalSpeed > 0f;
            if (shouldFaceRight == _isFacingRight)
            {
                return;
            }

            _isFacingRight = shouldFaceRight;
            foreach (var spriteRenderer in _flippableSprites)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !_isFacingRight;
                }
            }
        }

        private void CacheBasePose()
        {
            if (_bodyVisual != null)
            {
                _baseLocalPosition = _bodyVisual.localPosition;
                _baseLocalScale = _bodyVisual.localScale;
                _baseLocalRotation = _bodyVisual.localRotation;
            }

            if (_movementRoot != null)
            {
                _previousWorldPosition = _movementRoot.position;
            }
        }

        private void RestoreBasePose()
        {
            if (_bodyVisual == null)
            {
                return;
            }

            _bodyVisual.SetLocalPositionAndRotation(
                _baseLocalPosition,
                _baseLocalRotation);
            _bodyVisual.localScale = _baseLocalScale;
        }
    }
}
