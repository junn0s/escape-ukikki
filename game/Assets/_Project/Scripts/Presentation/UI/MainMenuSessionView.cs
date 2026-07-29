using MonkeyLab.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.UI
{
    public sealed class MainMenuSessionView : MonoBehaviour
    {
        private const string MainMenuSceneName = "01_MainMenu";
        private const float PanelWidth = 560f;
        private const float PanelHeight = 330f;
        private const float ButtonHeight = 44f;

        [SerializeField] private GameSessionController _controller;

        private string _joinCode = string.Empty;

        public GameSessionController Controller => _controller;

        public void Configure(GameSessionController controller)
        {
            _controller = controller;
        }

        private void Awake()
        {
            if (_controller == null)
            {
                Debug.LogError("[Session] Main menu view is missing its controller.", this);
            }
        }

        private void OnGUI()
        {
            if (_controller == null ||
                SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                return;
            }

            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("ESCAPE UKIKKI ONLINE", GUI.skin.box);

            if (_controller.State == GameSessionState.Connected)
            {
                DrawConnectedSession();
            }
            else
            {
                DrawConnectionControls();
            }

            GUILayout.EndArea();
        }

        private void DrawConnectionControls()
        {
            GUI.enabled = !_controller.IsBusy;
            if (GUILayout.Button("방 만들기", GUILayout.Height(ButtonHeight)))
            {
                _ = _controller.CreateSessionAsync();
            }

            GUILayout.Space(16f);
            GUILayout.Label("참가 코드");
            _joinCode = GUILayout.TextField(_joinCode).ToUpperInvariant();
            if (GUILayout.Button("코드로 참가", GUILayout.Height(ButtonHeight)))
            {
                _ = _controller.JoinSessionAsync(_joinCode);
            }

            GUI.enabled = true;
            GUILayout.Space(12f);
            GUILayout.Label(CreateStatusMessage());
        }

        private void DrawConnectedSession()
        {
            var session = _controller.CurrentSession;
            GUILayout.Label(session.IsHost ? "방을 만들었습니다." : "방에 참가했습니다.");
            GUILayout.Label($"참가 코드: {session.JoinCode}");
            GUILayout.Label($"인원: {session.PlayerCount}/{session.MaxPlayers}");
            GUILayout.Space(12f);
            if (GUILayout.Button("세션 나가기", GUILayout.Height(ButtonHeight)))
            {
                _ = _controller.LeaveSessionAsync();
            }
        }

        private string CreateStatusMessage()
        {
            return _controller.State switch
            {
                GameSessionState.Creating => "Relay 방을 만들고 있습니다...",
                GameSessionState.Joining => "참가 코드로 연결하고 있습니다...",
                GameSessionState.Leaving => "세션을 정리하고 있습니다...",
                GameSessionState.Failed => _controller.FailureMessage,
                _ => "6명이 참가 코드를 공유해 같은 방에 접속합니다."
            };
        }
    }
}
