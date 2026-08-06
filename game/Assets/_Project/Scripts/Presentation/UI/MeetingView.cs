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
        private readonly List<NetworkPlayerAvatar> _candidates = new();
        private readonly List<NetworkPlayerAvatar> _participants = new();

        private const string ChatFieldName = "MeetingChatField";

        private NetworkRoundState _roundState;
        private NetworkMeetingAuthority _meetingAuthority;
        private NetworkMeetingChatAuthority _chatAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _meetingIntroStyle;
        private GUIStyle _meetingIntroHintStyle;
        private ulong _localVoteTargetId =
            NetworkMeetingAuthority.NoExileTargetId;
        private bool _hasLocalVote;
        private string _chatDraft = string.Empty;
        private Vector2 _chatScroll;
        private RoundPhase? _lastRoundPhase;
        private float _meetingIntroStartedAt;
        private float _meetingIntroUntil;

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
            var width = Mathf.Min(900f, Screen.width - 40f);
            var height = Mathf.Min(620f, Screen.height - 100f);
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("긴급 회의 · 투표 채팅", _titleStyle);
            GUILayout.Label(
                $"토론 {_roundState.RemainingPhaseSeconds:0}초",
                _bodyStyle);
            GUILayout.Label(
                "토론이 끝나면 투표가 자동으로 시작됩니다.",
                _hintStyle);

            RefreshParticipants();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawParticipantRoster();
            GUILayout.BeginVertical();

            // 사망·퇴출자는 채팅을 볼 수 없다(docs/ui-ux-design.md §11.1, GDD §17).
            // 서버가 유령을 전송 대상에서 제외하므로 목록 자체가 비어 있다.
            if (!IsLocalPlayerAlive())
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "유령 — 살아 있는 플레이어와 대화할 수 없습니다.",
                    _bodyStyle);
            }
            else
            {
                DrawChatLog();
                DrawChatInput();
                if (_chatAuthority != null &&
                    _chatAuthority.LocalRejectionReason !=
                    ChatRejectionReason.None)
                {
                    GUILayout.Label(
                        FormatChatRejection(
                            _chatAuthority.LocalRejectionReason),
                        _hintStyle);
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawParticipantRoster()
        {
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("회의 참가자", _titleStyle);
            if (_participants.Count == 0)
            {
                GUILayout.Label("참가자 동기화 중", _hintStyle);
            }

            for (var index = 0; index < _participants.Count; index++)
            {
                var participant = _participants[index];
                var isAlive = IsClientAlive(participant.NetworkObject);
                GUILayout.Label(
                    $"{(isAlive ? "●" : "✕")} " +
                    $"{FormatPlayerName(participant)}" +
                    $"{(isAlive ? string.Empty : " · 유령")}",
                    isAlive ? _bodyStyle : _hintStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawChatLog()
        {
            _chatScroll = GUILayout.BeginScrollView(
                _chatScroll,
                GUILayout.Height(Mathf.Min(390f, Screen.height - 285f)));
            var messages = _chatAuthority?.LocalMessages;
            if (messages == null || messages.Count == 0)
            {
                GUILayout.Label(
                    "단서와 동선을 근거로 투표하세요",
                    _bodyStyle);
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

        private void DrawChatInput()
        {
            if (_chatAuthority == null)
            {
                return;
            }

            var isSubmitRequested =
                Event.current.type == EventType.KeyDown &&
                Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter &&
                GUI.GetNameOfFocusedControl() == ChatFieldName;

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(ChatFieldName);
            _chatDraft = GUILayout.TextField(
                _chatDraft,
                _chatAuthority.MaximumLength);
            if (GUILayout.Button("전송", _buttonStyle, GUILayout.Width(64f)))
            {
                isSubmitRequested = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"{_chatDraft.Length}/{_chatAuthority.MaximumLength}자",
                _bodyStyle);

            if (!isSubmitRequested || string.IsNullOrWhiteSpace(_chatDraft))
            {
                return;
            }

            _chatAuthority.SubmitMessage(_chatDraft);
            _chatDraft = string.Empty;
            if (Event.current.type == EventType.KeyDown)
            {
                Event.current.Use();
            }
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
