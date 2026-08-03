using MonkeyLab.Network;
using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class MissionStationNetworkPresenter : MonoBehaviour
    {
        private const float CompletionPulseSeconds = 2.5f;
        private const float ActivityPulseSpeed = 7f;
        private const float FailureFlashSeconds = 0.8f;
        private const float FailureFlashSpeed = 24f;

        [SerializeField] private NetworkFuseStationAuthority _authority;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _completionColor =
            new(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color _failureColor =
            new(1f, 0.08f, 0.04f, 1f);

        private Color _baseColor = Color.white;
        private float _completionPulseUntil;
        private float _failureFlashUntil;
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
            if (_renderer != null)
            {
                _renderer.color = _baseColor;
            }
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
            if (_authority.Station != null)
            {
                _authority.Station.MissionFailed +=
                    HandleLocalMissionFailed;
            }
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
            if (_authority.Station != null)
            {
                _authority.Station.MissionFailed -=
                    HandleLocalMissionFailed;
            }
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

        private void HandleLocalMissionFailed(
            FuseStationPrototype station,
            int submittedValue,
            int expectedValue)
        {
            _failureFlashUntil =
                Time.unscaledTime + FailureFlashSeconds;
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

            if (Time.unscaledTime < _failureFlashUntil)
            {
                var pulse = Mathf.Sin(
                    Time.unscaledTime * FailureFlashSpeed) > 0f;
                _renderer.color = pulse ? _failureColor : Color.white;
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
