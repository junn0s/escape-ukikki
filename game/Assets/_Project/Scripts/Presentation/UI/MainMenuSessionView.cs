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
            var playerCount = _lobbyRoster != null && _lobbyRoster.IsSpawned
                ? _players.Count
                : session.PlayerCount;
            GUILayout.Label($"인원: {playerCount}/{session.MaxPlayers}");
            GUILayout.Space(12f);

            if (_lobbyRoster == null || !_lobbyRoster.IsSpawned)
            {
                GUILayout.Label("로비 참가자 상태를 동기화하고 있습니다...");
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
            if (GUILayout.Button("세션 나가기", GUILayout.Height(ButtonHeight)))
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
                var readyLabel = player.IsReady ? "준비" : "대기";
                GUILayout.Label(
                    $"{player.SlotIndex + 1}. " +
                    $"{CreateColorLabel(player.Color)} · " +
                    $"{player.Nickname} · {readyLabel}{hostLabel}");
            }

            for (var index = _players.Count;
                 index < GameSessionService.RequiredPlayerCount;
                 index++)
            {
                GUILayout.Label($"{index + 1}. 비어 있음");
            }
        }

        private void DrawLocalLobbyControls()
        {
            if (!_lobbyRoster.TryGetLocalPlayer(out var localPlayer))
            {
                GUILayout.Label("내 로비 정보를 기다리고 있습니다...");
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

            var readyButtonLabel = localPlayer.IsReady ? "준비 해제" : "준비";
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
            if (GUILayout.Button("게임 시작", GUILayout.Height(ButtonHeight)))
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
                GUILayout.Label("연구소로 이동하고 있습니다...");
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
