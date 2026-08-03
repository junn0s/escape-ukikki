using UnityEngine;

namespace MonkeyLab.Presentation.Camera
{
    /// <summary>
    /// 한 CCTV 채널의 직교 카메라와 런타임 RenderTexture를 관리한다.
    /// 현재 보는 채널만 렌더해 다중 카메라 비용을 막는다(TDD §15.1).
    /// </summary>
    public sealed class CctvFeedCamera : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private string _displayName;
        [SerializeField, Min(128)] private int _textureWidth = 640;
        [SerializeField, Min(128)] private int _textureHeight = 360;

        private RenderTexture _renderTexture;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? gameObject.name
            : _displayName;
        public RenderTexture Texture => _renderTexture;

        public void Configure(
            UnityEngine.Camera feedCamera,
            string displayName,
            int textureWidth = 640,
            int textureHeight = 360)
        {
            _camera = feedCamera;
            _displayName = displayName;
            _textureWidth = Mathf.Max(128, textureWidth);
            _textureHeight = Mathf.Max(128, textureHeight);
            if (_camera != null)
            {
                _camera.enabled = false;
            }
        }

        public void SetRendering(bool shouldRender)
        {
            if (_camera == null)
            {
                return;
            }

            if (shouldRender)
            {
                EnsureRenderTexture();
                _camera.targetTexture = _renderTexture;
            }

            _camera.enabled = shouldRender;
            if (!shouldRender)
            {
                _camera.targetTexture = null;
            }
        }

        private void Awake()
        {
            _camera ??= GetComponent<UnityEngine.Camera>();
            if (_camera != null)
            {
                _camera.enabled = false;
            }
        }

        private void OnDisable()
        {
            SetRendering(false);
        }

        private void OnDestroy()
        {
            if (_renderTexture == null)
            {
                return;
            }

            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null &&
                _renderTexture.width == _textureWidth &&
                _renderTexture.height == _textureHeight)
            {
                if (!_renderTexture.IsCreated())
                {
                    _renderTexture.Create();
                }

                return;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            _renderTexture = new RenderTexture(
                _textureWidth,
                _textureHeight,
                16,
                RenderTextureFormat.ARGB32)
            {
                name = $"RT_CCTV_{gameObject.name}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _renderTexture.Create();
        }
    }
}
