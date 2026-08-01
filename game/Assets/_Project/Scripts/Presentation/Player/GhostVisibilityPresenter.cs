using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.Player
{
    /// <summary>
    /// 유령 표시 규칙이다(GDD §17).
    ///
    /// - 살아 있는 플레이어는 유령을 볼 수 없다.
    /// - 유령끼리는 서로 보인다.
    /// - 자기 자신이 유령이면 자기 모습은 보인다.
    ///
    /// 표시 여부만 다루고 판정은 서버가 한다.
    /// </summary>
    public sealed class GhostVisibilityPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkInfectionAuthority _infection;
        [SerializeField] private Renderer[] _renderers =
            Array.Empty<Renderer>();
        [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.45f;

        private bool _isHidden;

        public bool IsHidden => _isHidden;

        public void Configure(
            NetworkInfectionAuthority infection,
            Renderer[] renderers)
        {
            _infection = infection;
            _renderers = renderers ?? Array.Empty<Renderer>();
        }

        private void LateUpdate()
        {
            if (_infection == null)
            {
                return;
            }

            var isThisGhost =
                _infection.LifeState == PlayerLifeState.DeadGhost;
            var shouldHide = isThisGhost && !IsLocalViewerGhost();
            if (shouldHide != _isHidden)
            {
                ApplyVisibility(shouldHide);
                _isHidden = shouldHide;
            }

            if (!shouldHide && isThisGhost)
            {
                ApplyGhostTint();
            }
        }

        /// <summary>보는 사람이 유령이면 다른 유령이 보인다.</summary>
        private static bool IsLocalViewerGhost()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject =
                networkManager != null && networkManager.IsClient
                    ? networkManager.LocalClient?.PlayerObject
                    : null;
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkInfectionAuthority>(
                       out var localInfection) &&
                   localInfection.LifeState == PlayerLifeState.DeadGhost;
        }

        private void ApplyVisibility(bool shouldHide)
        {
            for (var index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] != null)
                {
                    _renderers[index].enabled = !shouldHide;
                }
            }
        }

        private void ApplyGhostTint()
        {
            for (var index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] is SpriteRenderer spriteRenderer)
                {
                    var color = spriteRenderer.color;
                    if (!Mathf.Approximately(color.a, _ghostAlpha))
                    {
                        color.a = _ghostAlpha;
                        spriteRenderer.color = color;
                    }
                }
            }
        }
    }
}
