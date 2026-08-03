using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 유령일 때만 보이는 별도 채팅 탭이다. 생존자 회의 채팅과 UI·네트워크
    /// 모두 분리해 정보가 섞이지 않는다.
    /// </summary>
    public sealed class GhostChatView : MonoBehaviour
    {
        private const string ChatFieldName = "GhostChatField";

        private NetworkGhostChatAuthority _authority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        private string _draft = string.Empty;
        private Vector2 _scroll;
        private bool _isOpen;

        private void OnEnable()
        {
            NetworkGhostChatAuthority.CurrentChanged += Bind;
            Bind();
        }

        private void OnDisable()
        {
            NetworkGhostChatAuthority.CurrentChanged -= Bind;
            Unbind();
            _isOpen = false;
            _draft = string.Empty;
        }

        private void Bind()
        {
            Unbind();
            _authority = NetworkGhostChatAuthority.Current;
            if (_authority != null)
            {
                _authority.MessagesChanged += HandleMessagesChanged;
            }
        }

        private void Unbind()
        {
            if (_authority != null)
            {
                _authority.MessagesChanged -= HandleMessagesChanged;
            }

            _authority = null;
        }

        private void HandleMessagesChanged()
        {
            _scroll.y = float.MaxValue;
        }

        private void OnGUI()
        {
            if (!IsLocalPlayerGhost() ||
                NetworkRoundState.Current?.Outcome !=
                    RoundOutcome.None)
            {
                _isOpen = false;
                return;
            }

            EnsureStyles();
            var toggleRect = new Rect(16f, Screen.height - 168f, 220f, 30f);
            if (GUI.Button(
                    toggleRect,
                    _isOpen ? "유령 채팅 닫기" : "유령 채팅 열기",
                    _buttonStyle))
            {
                _isOpen = !_isOpen;
            }

            if (!_isOpen)
            {
                return;
            }

            var area = new Rect(16f, Screen.height - 510f, 390f, 330f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(
                area.x + 14f,
                area.y + 12f,
                area.width - 28f,
                area.height - 24f));
            GUILayout.Label("유령 전용 채팅", _titleStyle);
            GUILayout.Label(
                "이 대화는 현재 유령에게만 전달됩니다.",
                _bodyStyle);
            DrawMessages();
            DrawInput();
            GUILayout.EndArea();
        }

        private void DrawMessages()
        {
            _scroll = GUILayout.BeginScrollView(
                _scroll,
                GUILayout.Height(205f));
            var messages = _authority?.LocalMessages;
            if (messages == null || messages.Count == 0)
            {
                GUILayout.Label("아직 메시지가 없습니다.", _bodyStyle);
            }
            else
            {
                for (var index = 0; index < messages.Count; index++)
                {
                    var entry = messages[index];
                    GUILayout.Label(
                        $"[{FormatSlotName(entry.SlotIndex)}] {entry.Text}",
                        _bodyStyle);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawInput()
        {
            if (_authority == null)
            {
                return;
            }

            var submitRequested =
                Event.current.type == EventType.KeyDown &&
                Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
                GUI.GetNameOfFocusedControl() == ChatFieldName;
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(ChatFieldName);
            _draft = GUILayout.TextField(_draft, _authority.MaximumLength);
            if (GUILayout.Button("전송", _buttonStyle, GUILayout.Width(64f)))
            {
                submitRequested = true;
            }

            GUILayout.EndHorizontal();
            if (!submitRequested || string.IsNullOrWhiteSpace(_draft))
            {
                return;
            }

            _authority.SubmitMessage(_draft);
            _draft = string.Empty;
            if (Event.current.type == EventType.KeyDown)
            {
                Event.current.Use();
            }
        }

        private static bool IsLocalPlayerGhost()
        {
            var playerObject = NetworkManager.Singleton?
                .LocalClient?.PlayerObject;
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) &&
                   infection.LifeState == PlayerLifeState.DeadGhost;
        }

        private static string FormatSlotName(byte slotIndex)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                foreach (var client in networkManager.ConnectedClients)
                {
                    var playerObject = client.Value?.PlayerObject;
                    if (playerObject != null &&
                        playerObject.TryGetComponent<NetworkPlayerAvatar>(
                            out var avatar) &&
                        avatar.SlotIndex == slotIndex)
                    {
                        return string.IsNullOrWhiteSpace(avatar.Nickname)
                            ? $"{slotIndex + 1}번"
                            : avatar.Nickname;
                    }
                }
            }

            return $"{slotIndex + 1}번";
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.82f, 1f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.9f, 1f) }
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
        }
    }
}
