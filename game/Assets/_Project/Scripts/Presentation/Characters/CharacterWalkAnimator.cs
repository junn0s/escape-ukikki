using System;
using UnityEngine;

namespace MonkeyLab.Presentation.Characters
{
    /// <summary>
    /// 이동 규칙에는 관여하지 않고 위치 변화만 관찰해 걷기 프레임을 넘긴다.
    /// 소유자와 원격 플레이어, 서버가 움직이는 괴물이 같은 표현을 사용한다.
    /// 걷기 그림이 아직 없으면 정지 프레임만 유지해 교체 전 동작을 그대로 남긴다.
    /// </summary>
    public sealed class CharacterWalkAnimator : MonoBehaviour
    {
        public const int RequiredWalkFrameCount = 4;

        /// <summary>
        /// 걷기 한 바퀴에 해당하는 이동 거리.
        /// <see cref="Player.PlayerMotionFeel"/>의 보행 바운스 한 주기
        /// (2π / StrideRadiansPerMeter ≈ 2.24m)와 같은 값이라, 발이 닿는 프레임과
        /// 몸이 내려앉는 순간이 어긋나지 않는다.
        /// </summary>
        private const float WalkCycleMeters = 2.244f;

        private const float MovingThresholdMetersPerSecond = 0.08f;
        private const float FlipThresholdMetersPerSecond = 0.35f;

        /// <summary>
        /// 멈추자마자 정지 프레임으로 돌아가면 물리 보정 같은 미세한 위치 변화에도
        /// 프레임이 떨린다. 이 시간 동안은 걷기 프레임을 유지한다.
        /// </summary>
        private const float StopHoldSeconds = 0.12f;

        [SerializeField] private Transform _movementRoot;
        [SerializeField] private SpriteRenderer _targetRenderer;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite[] _walkCycle = Array.Empty<Sprite>();
        [SerializeField] private bool _shouldControlFacing;
        [SerializeField] private Transform _facingRoot;

        private Vector3 _previousWorldPosition;
        private float _cycleMeters;
        private float _stoppedSeconds;
        private bool _isFacingRight = true;

        public bool HasWalkCycle =>
            _walkCycle is { Length: RequiredWalkFrameCount };

        public int WalkFrameCount => _walkCycle?.Length ?? 0;

        /// <param name="walkCycle">
        /// 접지A → 모음A → 접지B → 모음B 순서의 서로 다른 네 프레임.
        /// 개수·참조가 잘못되면 정지 프레임만 쓴다.
        /// </param>
        /// <param name="shouldControlFacing">
        /// 좌우 플립을 이 컴포넌트가 맡을지 여부. 플레이어는
        /// <see cref="Player.PlayerMotionFeel"/>이 이미 플립을 담당하므로 false를 준다.
        /// </param>
        public void Configure(
            Transform movementRoot,
            SpriteRenderer targetRenderer,
            Sprite idleSprite,
            Sprite[] walkCycle,
            bool shouldControlFacing,
            Transform facingRoot = null)
        {
            _movementRoot = movementRoot;
            _targetRenderer = targetRenderer;
            _idleSprite = idleSprite;
            _walkCycle = IsValidWalkCycle(walkCycle)
                ? (Sprite[])walkCycle.Clone()
                : Array.Empty<Sprite>();
            _shouldControlFacing = shouldControlFacing;
            _facingRoot = facingRoot;
            _isFacingRight = true;
            ApplyFacingVisual();
            ResetSampling();
            ApplyIdleFrame();
        }

        private void Awake()
        {
            if (_idleSprite == null && _targetRenderer != null)
            {
                _idleSprite = _targetRenderer.sprite;
            }
        }

        private void OnEnable()
        {
            ResetSampling();
            ApplyIdleFrame();
        }

        private void OnDisable()
        {
            ApplyIdleFrame();
        }

        private void LateUpdate()
        {
            if (_movementRoot == null || _targetRenderer == null)
            {
                return;
            }

            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var worldDelta = _movementRoot.position - _previousWorldPosition;
            _previousWorldPosition = _movementRoot.position;

            if (_shouldControlFacing)
            {
                ApplyFacing(worldDelta.x / deltaTime);
            }

            if (!HasWalkCycle)
            {
                return;
            }

            var speed = worldDelta.magnitude / deltaTime;
            if (speed > MovingThresholdMetersPerSecond)
            {
                _stoppedSeconds = 0f;
                _cycleMeters =
                    (_cycleMeters + worldDelta.magnitude) % WalkCycleMeters;
                ApplyWalkFrame();
                return;
            }

            _stoppedSeconds += deltaTime;
            if (_stoppedSeconds < StopHoldSeconds)
            {
                return;
            }

            _cycleMeters = 0f;
            ApplyIdleFrame();
        }

        private void ApplyWalkFrame()
        {
            var slotCount = _walkCycle.Length;
            var metersPerSlot = WalkCycleMeters / slotCount;
            var slot = Mathf.FloorToInt(_cycleMeters / metersPerSlot) % slotCount;
            var frame = _walkCycle[slot];
            if (frame != null)
            {
                _targetRenderer.sprite = frame;
            }
        }

        private void ApplyIdleFrame()
        {
            if (_targetRenderer != null && _idleSprite != null)
            {
                _targetRenderer.sprite = _idleSprite;
            }
        }

        private static bool IsValidWalkCycle(Sprite[] walkCycle)
        {
            if (walkCycle == null ||
                walkCycle.Length != RequiredWalkFrameCount)
            {
                return false;
            }

            for (var index = 0; index < walkCycle.Length; index++)
            {
                if (walkCycle[index] == null)
                {
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (walkCycle[index] == walkCycle[previous])
                    {
                        return false;
                    }
                }
            }

            return true;
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
            ApplyFacingVisual();
        }

        private void ApplyFacingVisual()
        {
            if (_facingRoot == null)
            {
                if (_targetRenderer != null)
                {
                    _targetRenderer.flipX = !_isFacingRight;
                }

                return;
            }

            var localScale = _facingRoot.localScale;
            var absoluteScaleX = Mathf.Max(Mathf.Abs(localScale.x), 0.0001f);
            localScale.x = _isFacingRight
                ? absoluteScaleX
                : -absoluteScaleX;
            _facingRoot.localScale = localScale;
            if (_targetRenderer != null)
            {
                _targetRenderer.flipX = false;
            }
        }

        private void ResetSampling()
        {
            if (_movementRoot != null)
            {
                _previousWorldPosition = _movementRoot.position;
            }

            _cycleMeters = 0f;
            _stoppedSeconds = 0f;
        }
    }
}
