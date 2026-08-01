using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 보안실 서버 로그 열람 화면이다.
    /// 프로젝트 50% 이후에만 열리고, 유령은 조작할 수 없다(GDD §17).
    /// 로그에는 작동 시각과 방만 나오고 누가 눌렀는지는 나오지 않는다.
    /// </summary>
    public sealed class SecurityTerminalView : MonoBehaviour
    {
        private const int MaxVisibleEntries = 8;

        private NetworkSecurityTerminalAuthority _terminal;
        private NetworkRoundState _roundState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        private bool _isLogOpen;

        private void OnEnable()
        {
            NetworkSecurityTerminalAuthority.CurrentChanged += BindTerminal;
            NetworkRoundState.CurrentChanged += BindRound;
            BindTerminal();
            BindRound();
        }

        private void OnDisable()
        {
            NetworkSecurityTerminalAuthority.CurrentChanged -= BindTerminal;
            NetworkRoundState.CurrentChanged -= BindRound;
            UnbindTerminal();
            UnbindRound();
        }

        private void BindTerminal()
        {
            UnbindTerminal();
            _terminal = NetworkSecurityTerminalAuthority.Current;
            if (_terminal != null)
            {
                _terminal.TerminalStateChanged += RepaintView;
            }
        }

        private void UnbindTerminal()
        {
            if (_terminal != null)
            {
                _terminal.TerminalStateChanged -= RepaintView;
            }

            _terminal = null;
            _isLogOpen = false;
        }

        private void BindRound()
        {
            _roundState = NetworkRoundState.Current;
        }

        private void UnbindRound()
        {
            _roundState = null;
        }

        private void RepaintView()
        {
        }

        private void OnGUI()
        {
            if (_terminal == null || !_terminal.IsUnlocked ||
                _roundState == null ||
                _roundState.Phase != RoundPhase.Exploration ||
                !IsLocalPlayerAlive())
            {
                return;
            }

            EnsureStyles();
            var toggleRect = new Rect(16f, Screen.height - 132f, 220f, 30f);
            if (GUI.Button(
                    toggleRect,
                    _isLogOpen ? "보안 로그 닫기" : "보안 로그 열기",
                    _buttonStyle))
            {
                _isLogOpen = !_isLogOpen;
            }

            if (_isLogOpen)
            {
                DrawLog();
            }
        }

        private void DrawLog()
        {
            var entryCount = Mathf.Min(
                _terminal.LogEntryCount,
                MaxVisibleEntries);
            var height = 52f + Mathf.Max(1, entryCount) * 22f;
            var area = new Rect(
                16f,
                Screen.height - 140f - height,
                300f,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    area.x + 12f,
                    area.y + 10f,
                    area.width - 24f,
                    area.height - 20f));
            GUILayout.Label("보안실 스피커 로그", _titleStyle);
            if (_terminal.LogEntryCount == 0)
            {
                GUILayout.Label("기록 없음", _bodyStyle);
            }
            else
            {
                // 최근 기록부터 보여준다.
                var firstIndex = Mathf.Max(
                    0,
                    _terminal.LogEntryCount - MaxVisibleEntries);
                for (var index = _terminal.LogEntryCount - 1;
                     index >= firstIndex;
                     index--)
                {
                    if (_terminal.TryGetLogEntry(
                            index,
                            out var elapsedSeconds,
                            out var roomName))
                    {
                        var minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
                        var seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
                        GUILayout.Label(
                            $"{minutes:00}:{seconds:00}  {roomName}",
                            _bodyStyle);
                    }
                }
            }

            GUILayout.EndArea();
        }

        private static bool IsLocalPlayerAlive()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject =
                networkManager != null && networkManager.IsClient
                    ? networkManager.LocalClient?.PlayerObject
                    : null;
            return playerObject != null &&
                   (!playerObject.TryGetComponent<NetworkInfectionAuthority>(
                        out var infection) ||
                    infection.LifeState != PlayerLifeState.DeadGhost);
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 0.9f, 1f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
        }
    }
}
