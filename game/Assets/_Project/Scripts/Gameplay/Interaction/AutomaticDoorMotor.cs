using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    /// <summary>
    /// 자동문의 시각 패널과 통행 콜라이더만 제어한다.
    /// 열림 여부는 네트워크 권위 계층에서 전달받는다.
    /// </summary>
    public sealed class AutomaticDoorMotor : MonoBehaviour
    {
        private const float ClosedColliderEnableTolerance = 0.025f;
        private static readonly Color ClosedIndicatorColor =
            new(0.10f, 0.62f, 0.72f, 1f);
        private static readonly Color MovingIndicatorColor =
            new(1f, 0.58f, 0.10f, 1f);
        private static readonly Color OpenIndicatorColor =
            new(0.16f, 1f, 0.42f, 1f);

        [SerializeField] private Transform _panelA;
        [SerializeField] private Transform _panelB;
        [SerializeField] private Collider2D _blockingCollider;
        [SerializeField] private DoorBalanceConfig _config;
        [SerializeField] private Vector2 _slideAxis = Vector2.right;
        [SerializeField] private SpriteRenderer[] _statusIndicators =
            System.Array.Empty<SpriteRenderer>();

        private Vector3 _panelAClosedLocalPosition;
        private Vector3 _panelBClosedLocalPosition;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        public void Configure(
            Transform panelA,
            Transform panelB,
            Collider2D blockingCollider,
            DoorBalanceConfig config,
            Vector2 slideAxis,
            SpriteRenderer[] statusIndicators = null)
        {
            _panelA = panelA;
            _panelB = panelB;
            _blockingCollider = blockingCollider;
            _config = config;
            _slideAxis = slideAxis.sqrMagnitude > 0f
                ? slideAxis.normalized
                : Vector2.right;
            _statusIndicators =
                statusIndicators ?? System.Array.Empty<SpriteRenderer>();
            CacheClosedPositions();
            ApplyImmediateClosedState();
        }

        public void SetOpen(bool isOpen)
        {
            _isOpen = isOpen;
            if (_blockingCollider != null && isOpen)
            {
                _blockingCollider.enabled = false;
            }
        }

        /// <summary>
        /// 프레젠테이션 계층이 문 패널의 입면 위치를 보정한 뒤 새 닫힘 위치를
        /// 기준으로 삼는다. 문 이동 규칙은 모르고 현재 Transform만 다시 저장한다.
        /// </summary>
        public void RefreshClosedPositions()
        {
            CacheClosedPositions();
        }

        private void Awake()
        {
            CacheClosedPositions();
        }

        private void Update()
        {
            if (_panelA == null || _panelB == null || _config == null)
            {
                return;
            }

            var slideOffset = (Vector3)(
                _slideAxis * _config.PanelSlideDistanceMeters);
            var panelATarget = _isOpen
                ? _panelAClosedLocalPosition - slideOffset
                : _panelAClosedLocalPosition;
            var panelBTarget = _isOpen
                ? _panelBClosedLocalPosition + slideOffset
                : _panelBClosedLocalPosition;
            var maximumDelta =
                _config.OpenSpeedMetersPerSecond * Time.deltaTime;
            _panelA.localPosition = Vector3.MoveTowards(
                _panelA.localPosition,
                panelATarget,
                maximumDelta);
            _panelB.localPosition = Vector3.MoveTowards(
                _panelB.localPosition,
                panelBTarget,
                maximumDelta);

            var isFullyClosed = !_isOpen &&
                Vector3.SqrMagnitude(
                    _panelA.localPosition - panelATarget) <=
                ClosedColliderEnableTolerance *
                ClosedColliderEnableTolerance &&
                Vector3.SqrMagnitude(
                    _panelB.localPosition - panelBTarget) <=
                ClosedColliderEnableTolerance *
                ClosedColliderEnableTolerance;
            if (isFullyClosed && _blockingCollider != null)
            {
                _blockingCollider.enabled = true;
            }

            UpdateStatusIndicators(isFullyClosed);
        }

        private void CacheClosedPositions()
        {
            if (_panelA != null)
            {
                _panelAClosedLocalPosition = _panelA.localPosition;
            }

            if (_panelB != null)
            {
                _panelBClosedLocalPosition = _panelB.localPosition;
            }
        }

        private void ApplyImmediateClosedState()
        {
            _isOpen = false;
            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = true;
            }

            UpdateStatusIndicators(isFullyClosed: true);
        }

        private void UpdateStatusIndicators(bool isFullyClosed)
        {
            var color = _isOpen
                ? OpenIndicatorColor
                : isFullyClosed
                    ? ClosedIndicatorColor
                    : MovingIndicatorColor;
            if (isFullyClosed)
            {
                var pulse = 0.72f +
                            (Mathf.Sin(Time.unscaledTime * 2.5f) * 0.5f +
                             0.5f) * 0.28f;
                color *= pulse;
                color.a = 1f;
            }

            foreach (var indicator in _statusIndicators)
            {
                if (indicator != null)
                {
                    indicator.color = color;
                }
            }
        }
    }
}
