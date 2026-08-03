using System;
using UnityEngine;

namespace MonkeyLab.Presentation.Camera
{
    public sealed class CctvFeedController : MonoBehaviour
    {
        [SerializeField] private CctvFeedCamera[] _feeds =
            Array.Empty<CctvFeedCamera>();

        private int _activeFeedIndex;
        private bool _isViewing;

        public event Action FeedChanged;

        public int FeedCount => _feeds?.Length ?? 0;
        public int ActiveFeedIndex => _activeFeedIndex;
        public RenderTexture ActiveTexture => GetActiveFeed()?.Texture;
        public string ActiveDisplayName =>
            GetActiveFeed()?.DisplayName ?? "신호 없음";

        public void Configure(CctvFeedCamera[] feeds)
        {
            EndViewing();
            _feeds = feeds ?? Array.Empty<CctvFeedCamera>();
            _activeFeedIndex = 0;
        }

        public void BeginViewing()
        {
            if (_feeds == null || _feeds.Length == 0)
            {
                return;
            }

            _isViewing = true;
            ApplyActiveFeed();
        }

        public void EndViewing()
        {
            _isViewing = false;
            if (_feeds != null)
            {
                for (var index = 0; index < _feeds.Length; index++)
                {
                    _feeds[index]?.SetRendering(false);
                }
            }
        }

        public void SelectPrevious()
        {
            if (_feeds == null || _feeds.Length == 0)
            {
                return;
            }

            _activeFeedIndex =
                (_activeFeedIndex - 1 + _feeds.Length) % _feeds.Length;
            ApplyActiveFeed();
        }

        public void SelectNext()
        {
            if (_feeds == null || _feeds.Length == 0)
            {
                return;
            }

            _activeFeedIndex = (_activeFeedIndex + 1) % _feeds.Length;
            ApplyActiveFeed();
        }

        private void OnDisable()
        {
            EndViewing();
        }

        private CctvFeedCamera GetActiveFeed()
        {
            return _feeds != null &&
                   _activeFeedIndex >= 0 &&
                   _activeFeedIndex < _feeds.Length
                ? _feeds[_activeFeedIndex]
                : null;
        }

        private void ApplyActiveFeed()
        {
            if (!_isViewing || _feeds == null)
            {
                return;
            }

            for (var index = 0; index < _feeds.Length; index++)
            {
                _feeds[index]?.SetRendering(index == _activeFeedIndex);
            }

            FeedChanged?.Invoke();
        }
    }
}
