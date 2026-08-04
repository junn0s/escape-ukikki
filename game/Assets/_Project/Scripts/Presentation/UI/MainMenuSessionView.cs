using System;
using System.Collections.Generic;
using MonkeyLab.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.UI
{
    public sealed class MainMenuSessionView : MonoBehaviour
    {
        private const string MainMenuSceneName = "01_MainMenu";
        private const float PanelWidth = 560f;
        private const float PanelHeight = 640f;
        private const float ButtonHeight = 44f;

        [SerializeField] private GameSessionController _controller;
        [SerializeField] private LobbyRosterNetwork _lobbyRoster;

        private string _joinCode = string.Empty;
        private IReadOnlyList<LobbyPlayerState> _players =
            Array.Empty<LobbyPlayerState>();
        private string _lobbyMessage = string.Empty;

        public GameSessionController Controller => _controller;
        public LobbyRosterNetwork LobbyRoster => _lobbyRoster;

        public void Configure(
            GameSessionController controller,
            LobbyRosterNetwork lobbyRoster)
        {
            _controller = controller;
            _lobbyRoster = lobbyRoster;
        }

        private void Awake()
        {
            if (_controller == null || _lobbyRoster == null)
            {
                Debug.LogError(
                    "[Session] Main menu view is missing its session or lobby reference.",
                    this);
            }
        }

        private void OnEnable()
        {
            if (_lobbyRoster == null)
            {
                return;
            }

            _lobbyRoster.RosterChanged += HandleRosterChanged;
            _lobbyRoster.RequestRejected += HandleRequestRejected;
            RefreshRoster();
        }

        private void OnDisable()
        {
            if (_lobbyRoster == null)
            {
                return;
            }

            _lobbyRoster.RosterChanged -= HandleRosterChanged;
            _lobbyRoster.RequestRejected -= HandleRequestRejected;
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
            if (GUILayout.Button("코드로 참가!", GUILayout.Height(ButtonHeight)))
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
            GUILayout.Label(session.IsHost ? "방이 열렸습니다" : "방에 들어왔습니다");
            GUILayout.Label($"참가 코드: {session.JoinCode}");
            var playerCount = _lobbyRoster != null && _lobbyRoster.IsSpawned
                ? _players.Count
                : session.PlayerCount;
            GUILayout.Label($"인원: {playerCount}/{session.MaxPlayers}");
            GUILayout.Space(12f);

            if (_lobbyRoster == null || !_lobbyRoster.IsSpawned)
            {
                GUILayout.Label("참가자 불러오는 중");
            }
            else
            {
                DrawRoster();
                DrawLocalLobbyControls();
            }

            if (!string.IsNullOrEmpty(_lobbyMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label(_lobbyMessage);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("방 나가기", GUILayout.Height(ButtonHeight)))
            {
                _ = _controller.LeaveSessionAsync();
            }
        }

        private void DrawRoster()
        {
            GUILayout.Label("참가자");
            foreach (var player in _players)
            {
                var hostLabel = player.IsHost ? " · HOST" : string.Empty;
                var readyLabel = player.IsReady ? "준비 완료" : "대기 중";
                GUILayout.Label(
                    $"{player.SlotIndex + 1}. " +
                    $"{CreateColorLabel(player.Color)} · " +
                    $"{player.Nickname} · {readyLabel}{hostLabel}");
            }

            for (var index = _players.Count;
                 index < GameSessionService.RequiredPlayerCount;
                 index++)
            {
                GUILayout.Label($"{index + 1}. 빈 자리");
            }
        }

        private void DrawLocalLobbyControls()
        {
            if (!_lobbyRoster.TryGetLocalPlayer(out var localPlayer))
            {
                GUILayout.Label("내 자리 잡는 중");
                return;
            }

            GUILayout.Space(12f);
            GUILayout.Label("내 색상");
            GUILayout.BeginHorizontal();
            for (var colorIndex = 0;
                 colorIndex < GameSessionService.RequiredPlayerCount;
                 colorIndex++)
            {
                var color = (LobbyPlayerColor)colorIndex;
                GUI.enabled = color != localPlayer.Color;
                if (GUILayout.Button(CreateColorLabel(color)))
                {
                    _lobbyRoster.RequestSetColor(color);
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var readyButtonLabel = localPlayer.IsReady ? "준비 취소" : "준비 완료";
            if (GUILayout.Button(readyButtonLabel, GUILayout.Height(ButtonHeight)))
            {
                _lobbyRoster.RequestSetReady(!localPlayer.IsReady);
            }

            if (!localPlayer.IsHost)
            {
                return;
            }

            GUILayout.Space(8f);
            GUI.enabled = !_lobbyRoster.IsStartingGame;
            if (GUILayout.Button("시작!", GUILayout.Height(ButtonHeight)))
            {
                _lobbyRoster.RequestStartGame(allowDevelopmentStart: false);
            }

            if (Application.isEditor || Debug.isDebugBuild)
            {
                if (GUILayout.Button(
                        "개발용 현재 인원 시작",
                        GUILayout.Height(ButtonHeight)))
                {
                    _lobbyRoster.RequestStartGame(
                        allowDevelopmentStart: true);
                }
            }

            GUI.enabled = true;
            if (_lobbyRoster.IsStartingGame)
            {
                GUILayout.Label("연구소로 이동 중");
            }
        }

        private string CreateStatusMessage()
        {
            return _controller.State switch
            {
                GameSessionState.Creating => "방 만드는 중",
                GameSessionState.Joining => "방 찾는 중",
                GameSessionState.Reconnecting => "다시 연결하는 중",
                GameSessionState.Leaving => "방 정리하는 중",
                // 오류는 해결 방법을 담은 문장을 유지한다 (§15.5 예외).
                GameSessionState.Failed => _controller.FailureMessage,
                _ => "코드를 공유해 6명을 모으세요"
            };
        }

        private void HandleRosterChanged()
        {
            _lobbyMessage = string.Empty;
            RefreshRoster();
        }

        private void HandleRequestRejected(string message)
        {
            _lobbyMessage = message;
        }

        private void RefreshRoster()
        {
            _players = _lobbyRoster != null
                ? _lobbyRoster.CreateSnapshot()
                : Array.Empty<LobbyPlayerState>();
        }

        private static string CreateColorLabel(LobbyPlayerColor color)
        {
            return color switch
            {
                LobbyPlayerColor.Blue => "#1 파랑",
                LobbyPlayerColor.Yellow => "#2 노랑",
                LobbyPlayerColor.Green => "#3 초록",
                LobbyPlayerColor.Red => "#4 빨강",
                LobbyPlayerColor.Purple => "#5 보라",
                LobbyPlayerColor.Orange => "#6 주황",
                _ => "#? 알 수 없음"
            };
        }
    }
}
