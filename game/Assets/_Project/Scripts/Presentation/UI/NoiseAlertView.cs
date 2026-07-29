using MonkeyLab.Gameplay.Noise;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class NoiseAlertView : MonoBehaviour
    {
        private const float BannerDurationSeconds = 3f;

        [SerializeField] private NoiseService _noiseService;

        private bool _isSubscribed;
        private string _message = string.Empty;
        private float _visibleUntil;

        public void Configure(NoiseService noiseService)
        {
            Unsubscribe();
            _noiseService = noiseService;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_message) || Time.unscaledTime > _visibleUntil)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.72f, 0.20f, 0.08f);
            GUI.Box(
                new Rect((Screen.width - 660f) * 0.5f, 88f, 660f, 52f),
                _message,
                style);
            GUI.backgroundColor = previousColor;
        }

        private void HandleNoiseEmitted(NoiseEventData noise)
        {
            _message = noise.SourceType == NoiseSourceType.MissionFailure
                ? $"큰 전기 스파크! {noise.PathRadius:0}m 안의 원숭이가 소리를 조사합니다."
                : $"{noise.RoomId}에서 {noise.Intensity} 소음이 발생했습니다.";
            _visibleUntil = Time.unscaledTime + BannerDurationSeconds;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _noiseService == null)
            {
                return;
            }

            _noiseService.NoiseEmitted += HandleNoiseEmitted;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _noiseService == null)
            {
                return;
            }

            _noiseService.NoiseEmitted -= HandleNoiseEmitted;
            _isSubscribed = false;
        }
    }
}
