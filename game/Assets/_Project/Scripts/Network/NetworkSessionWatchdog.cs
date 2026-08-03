using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 라운드 중 네트워크 세션이 끊기면 로비 씬으로 되돌린다.
    /// 호스트가 종료되면 MVP는 호스트 이전을 지원하지 않으므로 경기를 무효 처리하고
    /// 로비로 돌아간다(GDD §19.1).
    ///
    /// NetworkBehaviour가 아닌 일반 MonoBehaviour다. NGO가 멈추는 순간
    /// NetworkObject는 despawn되어 콜백을 받을 수 없기 때문에,
    /// 세션 종료를 감지할 주체는 네트워크 수명주기 밖에 있어야 한다.
    /// </summary>
    public sealed class NetworkSessionWatchdog : MonoBehaviour
    {
        private NetworkManager _networkManager;
        private bool _hasHandledStop;

        private void OnEnable()
        {
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientStopped += HandleClientStopped;
            _networkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.OnServerStopped -= HandleServerStopped;
            _networkManager = null;
        }

        private async void HandleClientStopped(bool wasHost)
        {
            // 호스트로서 멈춘 경우는 OnServerStopped가 함께 처리한다.
            if (wasHost)
            {
                return;
            }

            var controller = GameSessionController.Current;
            if (controller != null)
            {
                Debug.Log(
                    "[Reconnect] Relay 연결을 다시 시도합니다.",
                    this);
                await controller.ReconnectSessionAsync();
                if (controller.State == GameSessionState.Connected &&
                    controller.NetworkManager != null &&
                    controller.NetworkManager.IsConnectedClient)
                {
                    Debug.Log(
                        "[Reconnect] Relay 재접속이 완료되었습니다.",
                        this);
                    return;
                }
            }

            ReturnToLobby("호스트 재접속에 실패했습니다.");
        }

        private void HandleServerStopped(bool wasHost)
        {
            ReturnToLobby("세션이 종료되었습니다.");
        }

        private void ReturnToLobby(string reason)
        {
            if (_hasHandledStop)
            {
                return;
            }

            var activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName != NetworkPlayerAvatar.LaboratorySceneName)
            {
                return;
            }

            _hasHandledStop = true;
            Debug.Log($"[Session] {reason} 로비로 돌아갑니다.", this);

            // NGO가 이미 멈췄으므로 네트워크 씬 전환을 쓸 수 없다.
            SceneManager.LoadScene(
                NetworkPlayerAvatar.MainMenuSceneName,
                LoadSceneMode.Single);
        }
    }
}
