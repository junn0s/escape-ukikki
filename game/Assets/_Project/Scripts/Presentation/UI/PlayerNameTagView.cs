using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 라운드 화면에서 각 플레이어 위에 색상과 닉네임을 표시한다(mvp-scope §3.3).
    /// 색상만으로 구분하지 않고 슬롯 번호를 함께 붙인다(GDD §21 접근성).
    ///
    /// 유령 표시 규칙은 <c>GhostVisibilityPresenter</c>와 같다.
    /// 살아 있는 사람에게는 유령의 이름표도 보이지 않는다(GDD §17).
    /// </summary>
    public sealed class PlayerNameTagView : MonoBehaviour
    {
        private const float VerticalOffsetMeters = 1.1f;

        [SerializeField] private UnityEngine.Camera _worldCamera;

        private GUIStyle _tagStyle;

        public void Configure(UnityEngine.Camera worldCamera)
        {
            _worldCamera = worldCamera;
        }

        private void OnGUI()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsClient)
            {
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            EnsureStyle();
            var isLocalViewerGhost = IsLocalViewerGhost(networkManager);
            foreach (var client in networkManager.ConnectedClients)
            {
                var playerObject = client.Value?.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    !avatar.IsConfigured)
                {
                    continue;
                }

                if (!IsVisibleToLocalViewer(playerObject, isLocalViewerGhost))
                {
                    continue;
                }

                DrawTag(camera, playerObject.transform.position, avatar);
            }
        }

        private void DrawTag(
            UnityEngine.Camera camera,
            Vector3 worldPosition,
            NetworkPlayerAvatar avatar)
        {
            var headPosition = worldPosition +
                new Vector3(0f, VerticalOffsetMeters, 0f);
            var screenPoint = camera.WorldToScreenPoint(headPosition);
            if (screenPoint.z < 0f)
            {
                return;
            }

            var label = BuildLabel(avatar);
            const float width = 180f;
            const float height = 22f;
            var rect = new Rect(
                screenPoint.x - width * 0.5f,
                Screen.height - screenPoint.y - height,
                width,
                height);
            GUI.Label(rect, label, _tagStyle);
        }

        private static string BuildLabel(NetworkPlayerAvatar avatar)
        {
            var nickname = avatar.Nickname;
            return string.IsNullOrWhiteSpace(nickname)
                ? $"{avatar.SlotIndex + 1}번 ({avatar.Color})"
                : $"{avatar.SlotIndex + 1}. {nickname}";
        }

        private static bool IsVisibleToLocalViewer(
            NetworkObject playerObject,
            bool isLocalViewerGhost)
        {
            var isGhost =
                playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection) &&
                infection.LifeState == PlayerLifeState.DeadGhost;
            return !isGhost || isLocalViewerGhost;
        }

        private static bool IsLocalViewerGhost(NetworkManager networkManager)
        {
            var playerObject = networkManager.LocalClient?.PlayerObject;
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkInfectionAuthority>(
                       out var localInfection) &&
                   localInfection.LifeState == PlayerLifeState.DeadGhost;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (_worldCamera != null)
            {
                return _worldCamera;
            }

            // 카메라는 씬 전환 때 교체되므로 없을 때만 다시 찾는다.
            _worldCamera = UnityEngine.Camera.main;
            return _worldCamera;
        }

        private void EnsureStyle()
        {
            _tagStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
            };
        }
    }
}
