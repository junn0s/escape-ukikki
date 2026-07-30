using MonkeyLab.Network;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class MissionStationNetworkPresenter : MonoBehaviour
    {
        private const float CompletionPulseSeconds = 2.5f;
        private const float ActivityPulseSpeed = 7f;

        [SerializeField] private NetworkFuseStationAuthority _authority;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _completionColor =
            new(0.15f, 1f, 0.35f, 1f);

        private Color _baseColor = Color.white;
        private float _completionPulseUntil;
        private bool _isSubscribed;

        public NetworkFuseStationAuthority Authority => _authority;
        public SpriteRenderer TargetRenderer => _renderer;

        public void Configure(
            NetworkFuseStationAuthority authority,
            SpriteRenderer targetRenderer)
        {
            Unsubscribe();
            _authority = authority;
            _renderer = targetRenderer;
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }

            Subscribe();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ApplyVisual();
        }

        private void Subscribe()
        {
            if (_isSubscribed || _authority == null)
            {
                return;
            }

            _authority.PublicVisualStateChanged +=
                HandlePublicVisualStateChanged;
            _authority.PublicMissionCompleted +=
                HandlePublicMissionCompleted;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _authority == null)
            {
                return;
            }

            _authority.PublicVisualStateChanged -=
                HandlePublicVisualStateChanged;
            _authority.PublicMissionCompleted -=
                HandlePublicMissionCompleted;
            _isSubscribed = false;
        }

        private void HandlePublicVisualStateChanged()
        {
            ApplyVisual();
        }

        private void HandlePublicMissionCompleted()
        {
            _completionPulseUntil =
                Time.unscaledTime + CompletionPulseSeconds;
        }

        private void ApplyVisual()
        {
            if (_authority == null || _renderer == null)
            {
                return;
            }

            if (_authority.IsCompleted ||
                Time.unscaledTime < _completionPulseUntil)
            {
                _renderer.color = _completionColor;
                return;
            }

            if (_authority.IsOccupied)
            {
                var pulse =
                    Mathf.Sin(Time.unscaledTime * ActivityPulseSpeed) *
                    0.5f + 0.5f;
                _renderer.color =
                    Color.Lerp(_baseColor, Color.white, 0.35f + pulse * 0.3f);
                return;
            }

            _renderer.color = _baseColor;
        }
    }
}
