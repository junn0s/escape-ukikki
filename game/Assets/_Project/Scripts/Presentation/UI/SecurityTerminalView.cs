using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 보안실 단말기의 실시간 CCTV와 서버 로그 화면이다.
    /// 월드 E 상호작→서버 점유 검증→로컬 RenderTexture 표시 순서로 열리며,
    /// 현재 표시하는 채널의 카메라 하나만 렌더한다.
    /// </summary>
    public sealed class SecurityTerminalView : MonoBehaviour
    {
        private const int MaxVisibleEntries = 6;

        [SerializeField] private SecurityTerminalPrototype _worldTerminal;
        [SerializeField] private CctvFeedController _feedController;

        private NetworkSecurityTerminalAuthority _terminal;
        private NetworkRoundState _roundState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        private PlayerMotor _lockedMotor;
        private PlayerAimController _lockedAim;
        private bool _isTerminalOpen;

        public void Configure(
            SecurityTerminalPrototype worldTerminal,
            CctvFeedController feedController)
        {
            UnbindWorldTerminal();
            _worldTerminal = worldTerminal;
            _feedController = feedController;
            BindWorldTerminal();
            ApplyTerminalVisual();
        }

        private void OnEnable()
        {
            NetworkSecurityTerminalAuthority.CurrentChanged += BindTerminal;
            NetworkRoundState.CurrentChanged += BindRound;
            BindTerminal();
            BindRound();
            BindWorldTerminal();
        }

        private void OnDisable()
        {
            NetworkSecurityTerminalAuthority.CurrentChanged -= BindTerminal;
            NetworkRoundState.CurrentChanged -= BindRound;
            CloseLocalView(true);
            UnbindTerminal();
            UnbindRound();
            UnbindWorldTerminal();
        }

        private void BindTerminal()
        {
            UnbindTerminal();
            _terminal = NetworkSecurityTerminalAuthority.Current;
            if (_terminal != null)
            {
                _terminal.TerminalStateChanged += HandleTerminalStateChanged;
            }

            HandleTerminalStateChanged();
        }

        private void UnbindTerminal()
        {
            if (_terminal != null)
            {
                _terminal.TerminalStateChanged -= HandleTerminalStateChanged;
            }

            _terminal = null;
        }

        private void BindRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += HandleRoundStateChanged;
            }
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= HandleRoundStateChanged;
            }

            _roundState = null;
        }

        private void BindWorldTerminal()
        {
            if (_worldTerminal == null)
            {
                return;
            }

            _worldTerminal.InteractionRequested -= HandleInteractionRequested;
            _worldTerminal.InteractionRequested += HandleInteractionRequested;
            _worldTerminal.SetInteractionFilter(CanUseTerminal);
        }

        private void UnbindWorldTerminal()
        {
            if (_worldTerminal != null)
            {
                _worldTerminal.InteractionRequested -=
                    HandleInteractionRequested;
                _worldTerminal.SetInteractionFilter(null);
            }
        }

        private void HandleInteractionRequested(GameObject interactor)
        {
            var localPlayer = NetworkManager.Singleton?
                .LocalClient?.PlayerObject;
            if (localPlayer == interactor && CanUseTerminal(interactor))
            {
                _terminal?.RequestViewing();
            }
        }

        private bool CanUseTerminal(GameObject interactor)
        {
            if (_terminal == null ||
                !_terminal.IsUnlocked ||
                _terminal.HasActiveViewer ||
                _roundState == null ||
                _roundState.Phase != RoundPhase.Exploration ||
                interactor == null)
            {
                return false;
            }

            return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private void HandleTerminalStateChanged()
        {
            if (_terminal != null && _terminal.IsLocalClientViewing)
            {
                OpenLocalView();
            }
            else if (_isTerminalOpen)
            {
                CloseLocalView(false);
            }

            ApplyTerminalVisual();
        }

        private void HandleRoundStateChanged()
        {
            if (_roundState == null ||
                _roundState.Phase != RoundPhase.Exploration)
            {
                CloseLocalView(true);
            }
        }

        private void OpenLocalView()
        {
            if (_isTerminalOpen)
            {
                return;
            }

            _isTerminalOpen = true;
            var localPlayer = NetworkManager.Singleton?
                .LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                _lockedMotor = localPlayer.GetComponent<PlayerMotor>();
                _lockedAim = localPlayer.GetComponent<PlayerAimController>();
                _lockedMotor?.SetMovementEnabled(false);
                _lockedAim?.SetAimingEnabled(false);
            }

            _feedController?.BeginViewing();
        }

        private void CloseLocalView(bool requestServerRelease)
        {
            if (requestServerRelease &&
                _terminal != null &&
                _terminal.IsLocalClientViewing)
            {
                _terminal.RequestStopViewing();
            }

            _feedController?.EndViewing();
            _lockedMotor?.SetMovementEnabled(true);
            _lockedAim?.SetAimingEnabled(true);
            _lockedMotor = null;
            _lockedAim = null;
            _isTerminalOpen = false;
        }

        private void ApplyTerminalVisual()
        {
            _worldTerminal?.ApplyNetworkState(
                _terminal != null && _terminal.IsUnlocked,
                _terminal != null && _terminal.HasActiveViewer);
        }

        private void OnGUI()
        {
            if (!_isTerminalOpen ||
                _terminal == null ||
                !_terminal.IsLocalClientViewing)
            {
                return;
            }

            EnsureStyles();
            const float width = 720f;
            const float height = 590f;
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(
                area.x + 18f,
                area.y + 14f,
                area.width - 36f,
                area.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("보안실 CCTV", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", _buttonStyle, GUILayout.Width(72f)))
            {
                CloseLocalView(true);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            GUILayout.EndHorizontal();
            DrawFeed();
            DrawLog();
            GUILayout.EndArea();
        }

        private void DrawFeed()
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = _feedController != null &&
                          _feedController.FeedCount > 1;
            if (GUILayout.Button("◀ 이전", _buttonStyle, GUILayout.Width(90f)))
            {
                _feedController.SelectPrevious();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                _feedController != null
                    ? $"{_feedController.ActiveFeedIndex + 1}" +
                      $"/{_feedController.FeedCount}  " +
                      _feedController.ActiveDisplayName
                    : "CCTV 채널 없음",
                _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("다음 ▶", _buttonStyle, GUILayout.Width(90f)))
            {
                _feedController.SelectNext();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var feedRect = GUILayoutUtility.GetRect(
                640f,
                360f,
                GUILayout.ExpandWidth(true));
            var texture = _feedController?.ActiveTexture;
            if (texture != null)
            {
                GUI.DrawTexture(feedRect, texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUI.Box(feedRect, "CCTV 신호를 준비하는 중입니다.");
            }
        }

        private void DrawLog()
        {
            GUILayout.Space(6f);
            GUILayout.Label("최근 스피커 작동 로그", _titleStyle);
            if (_terminal.LogEntryCount == 0)
            {
                GUILayout.Label("기록 없음", _bodyStyle);
                return;
            }

            var firstIndex = Mathf.Max(
                0,
                _terminal.LogEntryCount - MaxVisibleEntries);
            for (var index = _terminal.LogEntryCount - 1;
                 index >= firstIndex;
                 index--)
            {
                if (!_terminal.TryGetLogEntry(
                        index,
                        out var elapsedSeconds,
                        out var roomName))
                {
                    continue;
                }

                var minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
                var seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
                GUILayout.Label(
                    $"{minutes:00}:{seconds:00}  {roomName}",
                    _bodyStyle);
            }
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
