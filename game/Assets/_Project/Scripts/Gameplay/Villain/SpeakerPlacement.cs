using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 방 하나에 놓인 스피커다. 소음 발생 지점과 붉은 LED 흔적 위치를 함께 나타낸다.
    /// </summary>
    public sealed class SpeakerPlacement : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private string _roomId;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _idleColor = new(0.55f, 0.58f, 0.62f, 1f);
        [SerializeField] private Color _playingColor = new(1f, 0.85f, 0.3f, 1f);

        public string RoomId => _roomId;
        public string DisplayName =>
            string.IsNullOrEmpty(_displayName) ? _roomId : _displayName;

        public void Configure(
            SpriteRenderer speakerRenderer,
            string roomId,
            string displayName)
        {
            _renderer = speakerRenderer;
            _roomId = roomId;
            _displayName = displayName;
        }

        /// <summary>재생 연출이다. 영구 흔적은 별도의 ClueMarker가 담당한다.</summary>
        public void PlayActivationFeedback(float playbackSeconds)
        {
            if (_renderer == null)
            {
                return;
            }

            CancelInvoke(nameof(RestoreIdleColor));
            _renderer.color = _playingColor;
            Invoke(nameof(RestoreIdleColor), playbackSeconds);
        }

        private void RestoreIdleColor()
        {
            if (_renderer != null)
            {
                _renderer.color = _idleColor;
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RestoreIdleColor));
        }
    }
}
