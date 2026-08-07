using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Meeting;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 회의 호출 버튼과 토론·투표·결과 화면이다.
    /// 표는 투표 중에 공개하지 않고 진행 수만 보여준다.
    /// </summary>
    public sealed class MeetingView : MonoBehaviour
    {
        private const float MeetingIntroDurationSeconds = 1.4f;
        private const string MeetingPanelResourcePath =
            "UI/T_MeetingChatPanel";
        private const float MeetingPanelAspect = 16f / 9f;
        private readonly List<NetworkPlayerAvatar> _candidates = new();
        private readonly List<NetworkPlayerAvatar> _participants = new();

        private NetworkRoundState _roundState;
        private NetworkMeetingAuthority _meetingAuthority;
        private NetworkMeetingChatAuthority _chatAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _meetingIntroStyle;
        private GUIStyle _meetingIntroHintStyle;
        private GUIStyle _participantStyle;
        private GUIStyle _chatNameStyle;
        private GUIStyle _chatBubbleStyle;
        private GUIStyle _chatBubbleBackgroundStyle;
        private GUIStyle _chatEmptyStyle;
        private Texture2D _meetingPanelTexture;
        private Texture2D _roundedBubbleTexture;
        private MeetingChatInputComposer _chatComposer;
        private ulong _localVoteTargetId =
            NetworkMeetingAuthority.NoExileTargetId;
        private bool _hasLocalVote;
        private Vector2 _chatScroll;
        private RoundPhase? _lastRoundPhase;
        private float _meetingIntroStartedAt;
        private float _meetingIntroUntil;

        private void Awake()
        {
            _meetingPanelTexture = Resources.Load<Texture2D>(
                MeetingPanelResourcePath);
            _chatComposer = GetComponent<MeetingChatInputComposer>();
            if (_chatComposer == null)
            {
                _chatComposer = gameObject.AddComponent<
                    MeetingChatInputComposer>();
            }
        }

        private void Update()
        {
            UpdateChatComposer();
        }

        private void OnEnable()
        {
            NetworkRoundState.CurrentChanged += BindRound;
            NetworkMeetingAuthority.CurrentChanged += BindMeeting;
            NetworkMeetingChatAuthority.CurrentChanged += BindChat;
            BindRound();
            BindMeeting();
            BindChat();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            NetworkMeetingAuthority.CurrentChanged -= BindMeeting;
            NetworkMeetingChatAuthority.CurrentChanged -= BindChat;
            UnbindRound();
            UnbindMeeting();
            UnbindChat();
            _chatComposer?.Hide();
        }

        private void OnDestroy()
        {
            if (_roundedBubbleTexture != null)
            {
                Destroy(_roundedBubbleTexture);
            }
        }

        private void BindChat()
        {
            UnbindChat();
            _chatAuthority = NetworkMeetingChatAuthority.Current;
            if (_chatAuthority != null)
            {
                _chatAuthority.MessagesChanged += HandleChatMessagesChanged;
            }
        }

        private void UnbindChat()
        {
            if (_chatAuthority != null)
            {
                _chatAuthority.MessagesChanged -= HandleChatMessagesChanged;
            }

            _chatAuthority = null;
        }

        private void HandleChatMessagesChanged()
        {
            // 새 발언이 오면 항상 마지막 줄이 보이게 한다.
            _chatScroll.y = float.MaxValue;
        }

        private void BindRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += HandleRoundStateChanged;
            }

            HandleRoundStateChanged();
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= HandleRoundStateChanged;
            }

            _roundState = null;
            _lastRoundPhase = null;
        }

        private void BindMeeting()
        {
            UnbindMeeting();
            _meetingAuthority = NetworkMeetingAuthority.Current;
            if (_meetingAuthority != null)
            {
                _meetingAuthority.MeetingStateChanged +=
                    HandleMeetingStateChanged;
            }
        }

        private void UnbindMeeting()
        {
            if (_meetingAuthority != null)
            {
                _meetingAuthority.MeetingStateChanged -=
                    HandleMeetingStateChanged;
            }

            _meetingAuthority = null;
        }

        private void HandleRoundStateChanged()
        {
            var currentPhase = _roundState?.Phase;
            if (currentPhase == RoundPhase.MeetingDiscussion &&
                _lastRoundPhase != RoundPhase.MeetingDiscussion)
            {
                _meetingIntroStartedAt = Time.unscaledTime;
                _meetingIntroUntil =
                    Time.unscaledTime + MeetingIntroDurationSeconds;
                _chatScroll = Vector2.zero;
                _chatComposer?.Clear();
            }

            _lastRoundPhase = currentPhase;
            if (_roundState != null &&
                _roundState.Phase != RoundPhase.MeetingVote)
            {
                _hasLocalVote = false;
                _localVoteTargetId =
                    NetworkMeetingAuthority.NoExileTargetId;
            }
        }

        private void HandleMeetingStateChanged()
        {
        }

        private void OnGUI()
        {
            if (_roundState == null || !_roundState.IsSpawned)
            {
                return;
            }

            EnsureStyles();
            switch (_roundState.Phase)
            {
                case RoundPhase.Exploration:
                    DrawCallButton();
                    break;
                case RoundPhase.MeetingDiscussion:
                    if (Time.unscaledTime < _meetingIntroUntil)
                    {
                        DrawMeetingIntro();
                    }
                    else
                    {
                        DrawDiscussion();
                    }
                    break;
                case RoundPhase.MeetingVote:
                    DrawVote();
                    break;
                case RoundPhase.MeetingResult:
                    DrawResult();
                    break;
            }
        }

        private void DrawCallButton()
        {
            if (_meetingAuthority == null || !IsLocalPlayerAlive())
            {
                return;
            }

            var remainingMeetings =
                _roundState.Config.MaximumMeetingCount -
                _roundState.UsedMeetingCount;
            var firstMeetingWait = Mathf.Max(
                0f,
                _roundState.Config.FirstMeetingLockSeconds -
                _roundState.ElapsedExplorationSeconds);
            var cooldownWait = _roundState.UsedMeetingCount > 0
                ? Mathf.Max(
                    0f,
                    _roundState.Config.MeetingCooldownSeconds -
                    _roundState.SecondsSinceLastMeeting)
                : 0f;
            var canCall = remainingMeetings > 0 &&
                          firstMeetingWait <= 0f && cooldownWait <= 0f;
            var status = remainingMeetings <= 0
                ? "이번 라운드의 회의를 모두 사용했습니다."
                : firstMeetingWait > 0f
                    ? $"시작 보호 중 · {Mathf.CeilToInt(firstMeetingWait)}초 뒤 사용 가능"
                    : cooldownWait > 0f
                        ? $"회의 쿨타임 · {Mathf.CeilToInt(cooldownWait)}초"
                        : _meetingAuthority.LocalRejectionReason !=
                          MeetingRejectionReason.None
                            ? FormatMeetingRejection(
                                _meetingAuthority.LocalRejectionReason)
                            : "채팅 토론이 열린 뒤 투표가 자동으로 시작됩니다.";

            var safeArea = Screen.safeArea;
            var rect = new Rect(
                safeArea.x + 16f,
                safeArea.yMax - 126f,
                364f,
                110f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 24f),
                "긴급 회의 · 투표 채팅",
                _titleStyle);
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 34f, rect.width - 28f, 22f),
                status,
                _hintStyle);

            GUI.enabled = canCall;
            var label = remainingMeetings > 0
                ? $"회의 요청 · 채팅 시작  (남은 {remainingMeetings}회)"
                : "남은 회의 없음";
            if (GUI.Button(
                    new Rect(
                        rect.x + 14f,
                        rect.yMax - 44f,
                        rect.width - 28f,
                        34f),
                    label,
                    _buttonStyle))
            {
                _meetingAuthority.RequestMeeting();
            }

            GUI.enabled = true;
        }

        private void DrawDiscussion()
        {
            GUI.depth = -8000;
            var layout = CreateDiscussionLayout();
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.002f, 0.006f, 0.012f, 1f));
            if (_meetingPanelTexture != null)
            {
                GUI.DrawTexture(
                    layout.PanelRect,
                    _meetingPanelTexture,
                    ScaleMode.StretchToFill,
                    false);
            }
            else
            {
                DrawSolidRect(
                    layout.PanelRect,
                    new Color(0.025f, 0.055f, 0.085f, 1f));
            }

            GUI.Label(
                layout.HeaderTitleRect,
                "긴급 단톡방 · RX-9 보안 채널",
                _titleStyle);
            var previousBodyAlignment = _bodyStyle.alignment;
            _bodyStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(
                layout.HeaderTimerRect,
                $"토론 {Mathf.CeilToInt(_roundState.RemainingPhaseSeconds):00}초",
                _bodyStyle);
            _bodyStyle.alignment = previousBodyAlignment;
            RefreshParticipants();
            DrawParticipantRoster(layout.ParticipantRect);

            // 사망·퇴출자는 채팅을 볼 수 없다(docs/ui-ux-design.md §11.1, GDD §17).
            // 서버가 유령을 전송 대상에서 제외하므로 목록 자체가 비어 있다.
            if (!IsLocalPlayerAlive())
            {
                GUI.Label(
                    layout.ChatLogRect,
                    "유령 — 살아 있는 플레이어와 대화할 수 없습니다.",
                    _chatEmptyStyle);
            }
            else
            {
                DrawChatLog(layout.ChatLogRect);
                DrawChatInput(layout);
                if (_chatAuthority != null &&
                    _chatAuthority.LocalRejectionReason !=
                    ChatRejectionReason.None)
                {
                    GUI.Label(
                        layout.ChatStatusRect,
                        FormatChatRejection(
                            _chatAuthority.LocalRejectionReason),
                        _hintStyle);
                }
            }
        }

        private void DrawParticipantRoster(Rect rect)
        {
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 28f),
                "회의 참가자",
                _titleStyle);
            if (_participants.Count == 0)
            {
                GUI.Label(
                    new Rect(rect.x + 14f, rect.y + 46f, rect.width - 28f, 30f),
                    "참가자 동기화 중",
                    _hintStyle);
            }

            const float rowHeight = 48f;
            for (var index = 0; index < _participants.Count; index++)
            {
                var participant = _participants[index];
                var isAlive = IsClientAlive(participant.NetworkObject);
                var row = new Rect(
                    rect.x + 10f,
                    rect.y + 46f + index * (rowHeight + 7f),
                    rect.width - 20f,
                    rowHeight);
                var playerColor = GetPlayerColor(participant.Color);
                DrawSolidRect(
                    row,
                    isAlive
                        ? new Color(0.025f, 0.065f, 0.095f, 0.92f)
                        : new Color(0.025f, 0.035f, 0.05f, 0.78f));
                DrawSolidRect(
                    new Rect(row.x, row.y, 6f, row.height),
                    isAlive
                        ? playerColor
                        : new Color(0.28f, 0.32f, 0.36f));
                _participantStyle.normal.textColor = isAlive
                    ? Color.white
                    : new Color(0.48f, 0.55f, 0.62f);
                GUI.Label(
                    new Rect(
                        row.x + 16f,
                        row.y + 3f,
                        row.width - 24f,
                        row.height - 6f),
                    $"{(isAlive ? "●" : "✕")} " +
                    $"{FormatPlayerName(participant)}" +
                    $"{(isAlive ? string.Empty : " · 유령")}",
                    _participantStyle);
            }
        }

        private void DrawChatLog(Rect rect)
        {
            DrawSolidRect(rect, new Color(0.012f, 0.025f, 0.043f, 0.78f));
            var messages = _chatAuthority?.LocalMessages;
            if (messages == null || messages.Count == 0)
            {
                GUI.Label(
                    new Rect(rect.x + 20f, rect.yMax - 60f, rect.width - 40f, 36f),
                    "단서와 동선을 근거로 대화를 시작하세요",
                    _chatEmptyStyle);
                return;
            }

            var viewWidth = Mathf.Max(100f, rect.width - 20f);
            var messagesHeight = CalculateMessagesHeight(messages, viewWidth);
            var contentHeight = Mathf.Max(rect.height - 4f, messagesHeight + 12f);
            var viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            _chatScroll = GUI.BeginScrollView(
                rect,
                _chatScroll,
                viewRect,
                false,
                true);

            var y = Mathf.Max(6f, contentHeight - messagesHeight - 6f);
            var hasLocalSlot = TryGetLocalSlotIndex(out var localSlotIndex);
            for (var index = 0; index < messages.Count; index++)
            {
                var entry = messages[index];
                var isLocal = hasLocalSlot &&
                              entry.SlotIndex == localSlotIndex;
                y += DrawChatMessage(
                    entry,
                    y,
                    viewWidth - 12f,
                    isLocal);
            }

            GUI.EndScrollView();
        }

        private void DrawMeetingIntro()
        {
            GUI.depth = -9000;
            var elapsed = Mathf.Max(
                0f,
                Time.unscaledTime - _meetingIntroStartedAt);
            var progress = Mathf.Clamp01(
                elapsed / MeetingIntroDurationSeconds);
            var fade = Mathf.Min(
                Mathf.Clamp01(progress * 5f),
                Mathf.Clamp01((1f - progress) * 5f));
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.015f, 0.018f, 0.025f, 0.94f * fade));

            var stripeHeight = Mathf.Lerp(0f, 190f, EaseOutCubic(progress));
            var stripe = new Rect(
                0f,
                Screen.height * 0.5f - stripeHeight * 0.5f,
                Screen.width,
                stripeHeight);
            DrawSolidRect(stripe, new Color(0.72f, 0.08f, 0.1f, fade));
            DrawSolidRect(
                new Rect(stripe.x, stripe.y, stripe.width, 6f),
                new Color(1f, 0.42f, 0.24f, fade));
            DrawSolidRect(
                new Rect(stripe.x, stripe.yMax - 6f, stripe.width, 6f),
                new Color(1f, 0.42f, 0.24f, fade));

            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.Label(
                new Rect(0f, stripe.center.y - 56f, Screen.width, 74f),
                "긴급 회의!",
                _meetingIntroStyle);
            GUI.Label(
                new Rect(0f, stripe.center.y + 22f, Screen.width, 30f),
                "살아 있는 전원과 단서를 공유한 뒤 투표합니다",
                _meetingIntroHintStyle);
            GUI.color = previousColor;
        }

        private void DrawChatInput(DiscussionLayout layout)
        {
            if (_chatAuthority == null || _chatComposer == null)
            {
                return;
            }

            DrawSolidRect(
                layout.ChatInputFrameRect,
                new Color(0.02f, 0.055f, 0.08f, 0.96f));
            if (GUI.Button(
                    layout.SendButtonRect,
                    "전송",
                    _buttonStyle))
            {
                _chatComposer.RequestSubmit();
            }

            GUI.Label(
                layout.ChatCounterRect,
                $"{_chatComposer.DraftLength}/{_chatAuthority.MaximumLength}",
                _hintStyle);
            GUI.Label(
                layout.ChatGuideRect,
                "Enter 또는 전송 · 토론 종료 후 자동 투표",
                _hintStyle);
        }

        private void UpdateChatComposer()
        {
            if (_chatComposer == null || _roundState == null ||
                _chatAuthority == null ||
                _roundState.Phase != RoundPhase.MeetingDiscussion ||
                Time.unscaledTime < _meetingIntroUntil ||
                !IsLocalPlayerAlive())
            {
                _chatComposer?.Hide();
                return;
            }

            var layout = CreateDiscussionLayout();
            _chatComposer.Show(
                layout.ChatInputRect,
                _chatAuthority.MaximumLength);
            if (!_chatComposer.ConsumeSubmitRequest())
            {
                return;
            }

            var draft = _chatComposer.Draft;
            if (string.IsNullOrWhiteSpace(draft))
            {
                return;
            }

            _chatAuthority.SubmitMessage(draft);
            _chatComposer.Clear();
            _chatScroll.y = float.MaxValue;
        }

        private float CalculateMessagesHeight(
            IReadOnlyList<MeetingChatEntry> messages,
            float viewWidth)
        {
            var height = 0f;
            for (var index = 0; index < messages.Count; index++)
            {
                height += GetChatMessageHeight(messages[index], viewWidth);
            }

            return height;
        }

        private float DrawChatMessage(
            MeetingChatEntry entry,
            float y,
            float viewWidth,
            bool isLocal)
        {
            GetChatBubbleMetrics(
                entry,
                viewWidth,
                out var bubbleWidth,
                out var bubbleHeight);
            var playerColor = GetSlotColor(entry.SlotIndex);
            var bubbleX = isLocal
                ? viewWidth - bubbleWidth
                : 8f;
            var nameRect = new Rect(
                bubbleX,
                y,
                bubbleWidth,
                21f);
            _chatNameStyle.alignment = isLocal
                ? TextAnchor.MiddleRight
                : TextAnchor.MiddleLeft;
            _chatNameStyle.normal.textColor = playerColor;
            GUI.Label(
                nameRect,
                FormatSlotName(entry.SlotIndex),
                _chatNameStyle);

            var bubbleRect = new Rect(
                bubbleX,
                nameRect.yMax + 2f,
                bubbleWidth,
                bubbleHeight);
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(
                Mathf.Lerp(0.14f, playerColor.r, 0.34f),
                Mathf.Lerp(0.18f, playerColor.g, 0.34f),
                Mathf.Lerp(0.22f, playerColor.b, 0.34f),
                0.98f);
            GUI.Box(
                bubbleRect,
                GUIContent.none,
                _chatBubbleBackgroundStyle);
            GUI.backgroundColor = previousBackground;

            DrawSolidRect(
                isLocal
                    ? new Rect(
                        bubbleRect.xMax - 4f,
                        bubbleRect.y + 8f,
                        4f,
                        bubbleRect.height - 16f)
                    : new Rect(
                        bubbleRect.x,
                        bubbleRect.y + 8f,
                        4f,
                        bubbleRect.height - 16f),
                playerColor);
            GUI.Label(
                new Rect(
                    bubbleRect.x + 14f,
                    bubbleRect.y + 7f,
                    bubbleRect.width - 28f,
                    bubbleRect.height - 14f),
                entry.Text,
                _chatBubbleStyle);
            return 21f + 2f + bubbleHeight + 12f;
        }

        private float GetChatMessageHeight(
            MeetingChatEntry entry,
            float viewWidth)
        {
            GetChatBubbleMetrics(
                entry,
                viewWidth,
                out _,
                out var bubbleHeight);
            return 21f + 2f + bubbleHeight + 12f;
        }

        private void GetChatBubbleMetrics(
            MeetingChatEntry entry,
            float viewWidth,
            out float bubbleWidth,
            out float bubbleHeight)
        {
            var content = new GUIContent(entry.Text);
            var maximumWidth = Mathf.Max(180f, viewWidth * 0.72f);
            var idealWidth = _chatBubbleStyle.CalcSize(content).x + 34f;
            bubbleWidth = Mathf.Clamp(
                idealWidth,
                Mathf.Min(132f, maximumWidth),
                maximumWidth);
            bubbleHeight = Mathf.Max(
                40f,
                _chatBubbleStyle.CalcHeight(
                    content,
                    bubbleWidth - 28f) + 16f);
        }

        private static bool TryGetLocalSlotIndex(out byte slotIndex)
        {
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject != null &&
                playerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) &&
                avatar.IsConfigured)
            {
                slotIndex = avatar.SlotIndex;
                return true;
            }

            slotIndex = NetworkPlayerAvatar.UnassignedSlot;
            return false;
        }

        private static Color GetSlotColor(byte slotIndex)
        {
            return TryFindAvatarBySlot(slotIndex, out var avatar)
                ? GetPlayerColor(avatar.Color)
                : new Color(0.48f, 0.78f, 0.86f);
        }

        private static Color GetPlayerColor(LobbyPlayerColor color)
        {
            return color switch
            {
                LobbyPlayerColor.Blue => new Color(0.18f, 0.60f, 1f),
                LobbyPlayerColor.Yellow => new Color(1f, 0.82f, 0.14f),
                LobbyPlayerColor.Green => new Color(0.18f, 0.82f, 0.38f),
                LobbyPlayerColor.Red => new Color(0.95f, 0.22f, 0.22f),
                LobbyPlayerColor.Purple => new Color(0.68f, 0.36f, 0.95f),
                LobbyPlayerColor.Orange => new Color(1f, 0.50f, 0.14f),
                _ => new Color(0.52f, 0.68f, 0.74f)
            };
        }

        private static DiscussionLayout CreateDiscussionLayout()
        {
            const float outerMargin = 14f;
            var availableWidth = Mathf.Max(320f, Screen.width - outerMargin * 2f);
            var availableHeight = Mathf.Max(180f, Screen.height - outerMargin * 2f);
            float width;
            float height;
            if (availableWidth / availableHeight > MeetingPanelAspect)
            {
                height = availableHeight;
                width = height * MeetingPanelAspect;
            }
            else
            {
                width = availableWidth;
                height = width / MeetingPanelAspect;
            }

            var panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            var headerTitle = new Rect(
                panel.x + panel.width * 0.055f,
                panel.y + panel.height * 0.025f,
                panel.width * 0.55f,
                panel.height * 0.055f);
            var headerTimer = new Rect(
                panel.xMax - panel.width * 0.28f,
                headerTitle.y,
                panel.width * 0.22f,
                headerTitle.height);
            var participants = new Rect(
                panel.x + panel.width * 0.032f,
                panel.y + panel.height * 0.105f,
                panel.width * 0.208f,
                panel.height * 0.79f);
            var chatSurface = new Rect(
                panel.x + panel.width * 0.263f,
                panel.y + panel.height * 0.105f,
                panel.width * 0.704f,
                panel.height * 0.79f);
            var inputHeight = Mathf.Clamp(
                panel.height * 0.065f,
                38f,
                58f);
            var sendWidth = Mathf.Clamp(
                chatSurface.width * 0.105f,
                72f,
                108f);
            var input = new Rect(
                chatSurface.x + 12f,
                chatSurface.yMax - inputHeight - 12f,
                chatSurface.width - sendWidth - 34f,
                inputHeight);
            var send = new Rect(
                input.xMax + 10f,
                input.y,
                sendWidth,
                input.height);
            var log = new Rect(
                chatSurface.x + 10f,
                chatSurface.y + 36f,
                chatSurface.width - 20f,
                Mathf.Max(
                    90f,
                    input.y - (chatSurface.y + 36f) - 26f));
            return new DiscussionLayout(
                panel,
                headerTitle,
                headerTimer,
                participants,
                log,
                input,
                send,
                new Rect(
                    chatSurface.x + 12f,
                    chatSurface.y + 7f,
                    chatSurface.width - 24f,
                    24f),
                new Rect(
                    input.xMax - 75f,
                    input.y - 23f,
                    75f,
                    20f),
                new Rect(input.x - 4f, input.y - 4f,
                    send.xMax - input.x + 4f, input.height + 8f),
                new Rect(
                    input.x,
                    input.y - 23f,
                    Mathf.Max(80f, input.width - 82f),
                    20f));
        }

        private readonly struct DiscussionLayout
        {
            public DiscussionLayout(
                Rect panelRect,
                Rect headerTitleRect,
                Rect headerTimerRect,
                Rect participantRect,
                Rect chatLogRect,
                Rect chatInputRect,
                Rect sendButtonRect,
                Rect chatGuideRect,
                Rect chatCounterRect,
                Rect chatInputFrameRect,
                Rect chatStatusRect)
            {
                PanelRect = panelRect;
                HeaderTitleRect = headerTitleRect;
                HeaderTimerRect = headerTimerRect;
                ParticipantRect = participantRect;
                ChatLogRect = chatLogRect;
                ChatInputRect = chatInputRect;
                SendButtonRect = sendButtonRect;
                ChatGuideRect = chatGuideRect;
                ChatCounterRect = chatCounterRect;
                ChatInputFrameRect = chatInputFrameRect;
                ChatStatusRect = chatStatusRect;
            }

            public Rect PanelRect { get; }
            public Rect HeaderTitleRect { get; }
            public Rect HeaderTimerRect { get; }
            public Rect ParticipantRect { get; }
            public Rect ChatLogRect { get; }
            public Rect ChatInputRect { get; }
            public Rect SendButtonRect { get; }
            public Rect ChatGuideRect { get; }
            public Rect ChatCounterRect { get; }
            public Rect ChatInputFrameRect { get; }
            public Rect ChatStatusRect { get; }
        }

        private static string FormatSlotName(byte slotIndex)
        {
            if (TryFindAvatarBySlot(slotIndex, out var avatar))
            {
                return FormatPlayerName(avatar);
            }

            return $"{slotIndex + 1}번";
        }

        private void DrawVote()
        {
            RefreshCandidates();
            var height = Mathf.Min(
                Screen.height - 100f,
                170f + _candidates.Count * 28f);
            var area = new Rect(
                Screen.width * 0.5f - 220f,
                80f,
                440f,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("투표", _titleStyle);
            GUILayout.Label(
                $"남은 시간 {_roundState.RemainingPhaseSeconds:0}초",
                _bodyStyle);
            if (_meetingAuthority != null)
            {
                GUILayout.Label(
                    $"투표 완료 {_meetingAuthority.CastVoteCount}" +
                    $"/{_meetingAuthority.EligibleVoterCount}",
                    _bodyStyle);
            }

            GUILayout.Label(
                _hasLocalVote
                    ? "표가 전송됐습니다. 시간 내에는 변경할 수 있습니다."
                    : "플레이어 한 명 또는 건너뛰기를 선택하세요.",
                _hintStyle);

            var canVote = IsLocalPlayerAlive() && _meetingAuthority != null;
            GUI.enabled = canVote;
            for (var index = 0; index < _candidates.Count; index++)
            {
                var candidate = _candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                var clientId = candidate.OwnerClientId;
                var isChosen = _hasLocalVote &&
                               _localVoteTargetId == clientId;
                var label = isChosen
                    ? $"▶ {FormatPlayerName(candidate)}"
                    : FormatPlayerName(candidate);
                if (GUILayout.Button(label, _buttonStyle))
                {
                    _meetingAuthority.RequestVote(clientId);
                    _localVoteTargetId = clientId;
                    _hasLocalVote = true;
                }
            }

            var isAbstaining = _hasLocalVote &&
                               _localVoteTargetId ==
                               NetworkMeetingAuthority.NoExileTargetId;
            if (GUILayout.Button(
                    isAbstaining ? "▶ 건너뛰기" : "건너뛰기",
                    _buttonStyle))
            {
                _meetingAuthority.RequestVote(
                    NetworkMeetingAuthority.NoExileTargetId);
                _localVoteTargetId =
                    NetworkMeetingAuthority.NoExileTargetId;
                _hasLocalVote = true;
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawResult()
        {
            var recordCount = _meetingAuthority?.LocalVoteRecords.Count ?? 0;
            var height = Mathf.Min(
                Screen.height - 100f,
                150f + recordCount * 24f);
            var area = new Rect(
                Screen.width * 0.5f - 220f,
                80f,
                440f,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("투표 결과", _titleStyle);
            if (_meetingAuthority == null ||
                !_meetingAuthority.HasLocalResult)
            {
                GUILayout.Label("서버가 표를 집계하고 있습니다.", _bodyStyle);
            }
            else if (_meetingAuthority.LocalExiledClientId ==
                     NetworkMeetingAuthority.NoExileTargetId)
            {
                GUILayout.Label(CreateNoExileResultText(), _bodyStyle);
            }
            else
            {
                // 퇴출된 플레이어의 역할은 라운드가 끝날 때까지 공개하지 않는다.
                GUILayout.Label(
                    $"{FormatClientName(_meetingAuthority.LocalExiledClientId)} 퇴출",
                    _bodyStyle);
            }

            if (_meetingAuthority != null &&
                _meetingAuthority.HasLocalResult)
            {
                GUILayout.Space(8f);
                GUILayout.Label("최종 투표 내역", _titleStyle);
                var records = _meetingAuthority.LocalVoteRecords;
                for (var index = 0; index < records.Count; index++)
                {
                    var record = records[index];
                    var targetName = record.TargetClientId ==
                                     NetworkMeetingAuthority.NoExileTargetId
                        ? "기권"
                        : FormatClientName(record.TargetClientId);
                    GUILayout.Label(
                        $"{FormatClientName(record.VoterClientId)} → " +
                        targetName,
                        _bodyStyle);
                }

                GUILayout.Space(4f);
                GUILayout.Label(
                    "퇴출자의 역할은 라운드 종료 전까지 공개되지 않습니다.",
                    _hintStyle);
            }

            GUILayout.EndArea();
        }

        private void RefreshCandidates()
        {
            RefreshParticipants();
        }

        private void RefreshParticipants()
        {
            _candidates.Clear();
            _participants.Clear();
            var networkManager = NetworkManager.Singleton;
            var spawnManager = networkManager?.SpawnManager;
            if (spawnManager == null)
            {
                return;
            }

            // ConnectedClients는 서버 권한 컬렉션이므로 클라이언트 UI에서
            // 사용하지 않는다. 각 클라이언트에 복제된 PlayerObject를 기준으로
            // 참가자를 구성해 비호스트에서도 6명 전원을 표시한다.
            foreach (var networkObject in spawnManager.SpawnedObjectsList)
            {
                if (networkObject == null ||
                    !networkObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    !avatar.IsConfigured)
                {
                    continue;
                }

                // HasAssignedRole은 소유자 전용 NetworkVariable이므로
                // 원격 플레이어 표시 필터로 쓰면 안 된다.
                _participants.Add(avatar);
                if (IsClientAlive(networkObject))
                {
                    _candidates.Add(avatar);
                }
            }

            _participants.Sort(CompareBySlot);
            _candidates.Sort(CompareBySlot);
        }

        private static bool IsClientAlive(NetworkObject playerObject)
        {
            return !playerObject.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private static bool IsLocalPlayerAlive()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject =
                networkManager != null && networkManager.IsClient
                    ? networkManager.LocalClient?.PlayerObject
                    : null;
            return playerObject != null && IsClientAlive(playerObject);
        }

        private static string FormatPlayerName(NetworkPlayerAvatar avatar)
        {
            var nickname = string.IsNullOrWhiteSpace(avatar.Nickname)
                ? "이름 없음"
                : avatar.Nickname;
            return $"{avatar.SlotIndex + 1}번 · " +
                   $"{FormatColor(avatar.Color)} · {nickname}";
        }

        private static string FormatClientName(ulong clientId)
        {
            if (TryFindAvatarByClientId(clientId, out var avatar))
            {
                return FormatPlayerName(avatar);
            }

            return $"클라이언트 {clientId}";
        }

        private string CreateNoExileResultText()
        {
            if (_meetingAuthority == null)
            {
                return "아무도 퇴출되지 않았습니다.";
            }

            var voteCounts = new Dictionary<ulong, int>();
            var abstainCount = 0;
            var records = _meetingAuthority.LocalVoteRecords;
            for (var index = 0; index < records.Count; index++)
            {
                var targetClientId = records[index].TargetClientId;
                if (targetClientId ==
                    NetworkMeetingAuthority.NoExileTargetId)
                {
                    abstainCount++;
                    continue;
                }

                voteCounts.TryGetValue(targetClientId, out var count);
                voteCounts[targetClientId] = count + 1;
            }

            var highestPlayerVoteCount = 0;
            var leaderCount = 0;
            foreach (var pair in voteCounts)
            {
                if (pair.Value > highestPlayerVoteCount)
                {
                    highestPlayerVoteCount = pair.Value;
                    leaderCount = 1;
                }
                else if (pair.Value == highestPlayerVoteCount)
                {
                    leaderCount++;
                }
            }

            if (abstainCount > highestPlayerVoteCount)
            {
                return "기권 최다 — 아무도 퇴출되지 않았습니다.";
            }

            if (abstainCount > 0 &&
                abstainCount == highestPlayerVoteCount)
            {
                return "기권과 최다 득표 동률 — 퇴출 없음";
            }

            return leaderCount > 1
                ? "동률 — 아무도 퇴출되지 않았습니다."
                : "아무도 퇴출되지 않았습니다.";
        }

        private static bool TryFindAvatarByClientId(
            ulong clientId,
            out NetworkPlayerAvatar avatar)
        {
            var spawnManager = NetworkManager.Singleton?.SpawnManager;
            if (spawnManager != null)
            {
                foreach (var networkObject in spawnManager.SpawnedObjectsList)
                {
                    if (networkObject != null &&
                        networkObject.OwnerClientId == clientId &&
                        networkObject.TryGetComponent(out avatar))
                    {
                        return true;
                    }
                }
            }

            avatar = null;
            return false;
        }

        private static bool TryFindAvatarBySlot(
            byte slotIndex,
            out NetworkPlayerAvatar avatar)
        {
            var spawnManager = NetworkManager.Singleton?.SpawnManager;
            if (spawnManager != null)
            {
                foreach (var networkObject in spawnManager.SpawnedObjectsList)
                {
                    if (networkObject != null &&
                        networkObject.TryGetComponent(out avatar) &&
                        avatar.IsConfigured &&
                        avatar.SlotIndex == slotIndex)
                    {
                        return true;
                    }
                }
            }

            avatar = null;
            return false;
        }

        private static int CompareBySlot(
            NetworkPlayerAvatar left,
            NetworkPlayerAvatar right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.SlotIndex.CompareTo(right.SlotIndex);
        }

        private static string FormatColor(LobbyPlayerColor color)
        {
            return color switch
            {
                LobbyPlayerColor.Blue => "파랑",
                LobbyPlayerColor.Yellow => "노랑",
                LobbyPlayerColor.Green => "초록",
                LobbyPlayerColor.Red => "빨강",
                LobbyPlayerColor.Purple => "보라",
                LobbyPlayerColor.Orange => "주황",
                _ => "알 수 없음"
            };
        }

        private static string FormatMeetingRejection(
            MeetingRejectionReason reason)
        {
            return reason switch
            {
                MeetingRejectionReason.NotExploring =>
                    "탐색 중에만 회의를 요청할 수 있습니다.",
                MeetingRejectionReason.CallerDead =>
                    "유령은 회의를 요청할 수 없습니다.",
                MeetingRejectionReason.FirstMeetingLocked =>
                    "시작 보호 시간이 끝난 뒤 회의를 요청하세요.",
                MeetingRejectionReason.OnCooldown =>
                    "회의 공용 쿨타임이 남아 있습니다.",
                MeetingRejectionReason.MeetingLimitReached =>
                    "이번 라운드의 회의를 모두 사용했습니다.",
                MeetingRejectionReason.RoundAlreadyEnded =>
                    "라운드가 이미 종료됐습니다.",
                _ => "회의 요청을 서버가 거부했습니다."
            };
        }

        private static string FormatChatRejection(
            ChatRejectionReason reason)
        {
            return reason switch
            {
                ChatRejectionReason.NotDiscussionPhase =>
                    "토론 시간에만 메시지를 보낼 수 있습니다.",
                ChatRejectionReason.NotAlive =>
                    "유령은 살아 있는 플레이어의 채팅에 참여할 수 없습니다.",
                ChatRejectionReason.NotParticipant =>
                    "현재 회의 참가자가 아닙니다.",
                ChatRejectionReason.EmptyMessage =>
                    "보낼 수 있는 내용이 없습니다.",
                ChatRejectionReason.TooFrequent =>
                    "메시지는 1초에 한 번만 보낼 수 있습니다.",
                _ => "메시지 전송을 서버가 거부했습니다."
            };
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.74f, 0.82f, 0.88f) }
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 15
            };
            _meetingIntroStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _meetingIntroHintStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = new Color(1f, 0.86f, 0.72f) }
            };
            _participantStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _chatNameStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _chatBubbleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(0.96f, 0.98f, 1f) }
            };
            _chatEmptyStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.52f, 0.64f, 0.72f) }
            };
            if (_chatBubbleBackgroundStyle == null)
            {
                _roundedBubbleTexture ??= CreateRoundedTexture(32, 9f);
                _chatBubbleBackgroundStyle = new GUIStyle(GUI.skin.box)
                {
                    border = new RectOffset(10, 10, 10, 10),
                    normal = { background = _roundedBubbleTexture }
                };
            }
        }

        private static Texture2D CreateRoundedTexture(int size, float radius)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "T_RuntimeMeetingBubble",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color32[size * size];
            var center = (size - 1f) * 0.5f;
            var half = center;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x - center) - (half - radius), 0f);
                    var dy = Mathf.Max(Mathf.Abs(y - center) - (half - radius), 0f);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                    colors[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(colors);
            texture.Apply(false, true);
            return texture;
        }

        private static float EaseOutCubic(float value)
        {
            var inverted = 1f - Mathf.Clamp01(value);
            return 1f - inverted * inverted * inverted;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
