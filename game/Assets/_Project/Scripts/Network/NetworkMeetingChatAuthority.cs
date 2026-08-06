using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Meeting;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 토론 단계의 텍스트 채팅을 서버 권위로 중계한다.
    /// 채팅 채널은 회의 참가자로 제한하므로(SDD §11.5) NetworkList로 전원에게
    /// 복제하지 않고, 살아 있는 클라이언트에게만 대상 지정 Rpc로 보낸다.
    /// 이렇게 해야 유령과 퇴출자가 채팅을 볼 수 없다는 규칙(GDD §17,
    /// docs/ui-ux-design.md §11.1)이 메모리 수준에서 지켜진다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMeetingChatAuthority : NetworkBehaviour
    {
        public static NetworkMeetingChatAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        [SerializeField] private RoundBalanceConfig _config;

        private readonly List<MeetingChatEntry> _localMessages = new();
        private readonly Dictionary<ulong, double> _lastSentServerTimes = new();
        private readonly List<ulong> _aliveClientIds = new();

        private RoundPhase _lastObservedPhase = RoundPhase.RoleReveal;

        public event Action MessagesChanged;
        public event Action RejectionChanged;

        public RoundBalanceConfig Config => _config;
        public IReadOnlyList<MeetingChatEntry> LocalMessages => _localMessages;
        public ChatRejectionReason LocalRejectionReason { get; private set; }

        /// <summary>텔레메트리 `meeting_resolved.discussionMessagesCount`용이다.</summary>
        public int ServerDiscussionMessageCount { get; private set; }

        public int MaximumLength =>
            _config != null ? _config.ChatMessageMaximumLength : 80;

        public void Configure(RoundBalanceConfig config)
        {
            _config = config;
        }

        public override void OnNetworkSpawn()
        {
            if (_config == null)
            {
                Debug.LogError(
                    "[MeetingChat] Round balance config is missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentChanged?.Invoke();
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback +=
                    HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            _localMessages.Clear();
            _lastSentServerTimes.Clear();
            LocalRejectionReason = ChatRejectionReason.None;
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        /// <summary>토론 화면의 전송 버튼이 호출한다.</summary>
        public void SubmitMessage(string message)
        {
            if (!IsSpawned || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // 클라이언트에서도 먼저 정리해 불필요한 트래픽을 줄인다.
            // 서버는 이 값을 신뢰하지 않고 다시 정리·검증한다.
            var sanitized = MeetingChatRules.Sanitize(message, MaximumLength);
            if (sanitized.Length == 0)
            {
                return;
            }

            LocalRejectionReason = ChatRejectionReason.None;
            RejectionChanged?.Invoke();
            SubmitChatMessageRpc(new FixedString512Bytes(sanitized));
        }

        private void Update()
        {
            var phase = NetworkRoundState.Current?.Phase ?? RoundPhase.RoleReveal;
            if (phase == _lastObservedPhase)
            {
                return;
            }

            _lastObservedPhase = phase;

            // 회의마다 새 단톡방을 연다. 이전 회의 발언이 남지 않는다.
            if (phase != RoundPhase.MeetingDiscussion)
            {
                return;
            }

            _localMessages.Clear();
            _lastSentServerTimes.Clear();
            ServerDiscussionMessageCount = 0;
            LocalRejectionReason = ChatRejectionReason.None;
            MessagesChanged?.Invoke();
            RejectionChanged?.Invoke();
        }

        [Rpc(SendTo.Server)]
        private void SubmitChatMessageRpc(
            FixedString512Bytes message,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _config == null)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar))
            {
                return;
            }

            var playerObject = client.PlayerObject;
            var lifeState =
                playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection)
                    ? infection.LifeState
                    : PlayerLifeState.AliveHealthy;
            var roundState = NetworkRoundState.Current;
            var serverTime = NetworkManager.ServerTime.Time;
            var sanitized = MeetingChatRules.Sanitize(
                message.ToString(),
                _config.ChatMessageMaximumLength);
            var lastSentServerTime = _lastSentServerTimes.TryGetValue(
                senderClientId,
                out var storedTime)
                ? storedTime
                : 0d;

            var rejection = MeetingChatRules.Validate(
                roundState != null &&
                    roundState.Phase == RoundPhase.MeetingDiscussion,
                lifeState,
                avatar.HasAssignedRole,
                sanitized,
                serverTime,
                lastSentServerTime,
                _config.ChatMessageIntervalSeconds);
            if (rejection != ChatRejectionReason.None)
            {
                // 원문은 로그에 남기지 않는다(밸런스 §11).
                PublishRejectionRpc(senderClientId, rejection);
                return;
            }

            _lastSentServerTimes[senderClientId] = serverTime;
            ServerDiscussionMessageCount++;

            // 인원 상한이 6명이라 개별 전송으로 충분하다.
            // 유령은 목록에 없으므로 채팅이 아예 전달되지 않는다.
            CollectAliveClientIds();
            var payload = new FixedString512Bytes(sanitized);
            for (var index = 0; index < _aliveClientIds.Count; index++)
            {
                PublishChatMessageRpc(
                    avatar.SlotIndex,
                    payload,
                    RpcTarget.Single(
                        _aliveClientIds[index],
                        RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishChatMessageRpc(
            byte slotIndex,
            FixedString512Bytes message,
            RpcParams rpcParams)
        {
            _localMessages.Add(
                new MeetingChatEntry(slotIndex, message.ToString()));
            var maximumCount = _config != null
                ? _config.ChatHistoryMaximumCount
                : 60;
            while (_localMessages.Count > maximumCount)
            {
                _localMessages.RemoveAt(0);
            }

            MessagesChanged?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            ChatRejectionReason rejectionReason)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                LocalRejectionReason = rejectionReason;
                RejectionChanged?.Invoke();
                Debug.LogWarning(
                    $"[MeetingChat] Message rejected: {rejectionReason}.",
                    this);
            }
        }

        /// <summary>
        /// 살아 있는 참가자만 모은다. 유령은 대상에서 빠지므로 채팅이 전달되지 않는다.
        /// </summary>
        private void CollectAliveClientIds()
        {
            _aliveClientIds.Clear();
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    !avatar.HasAssignedRole)
                {
                    continue;
                }

                if (playerObject.TryGetComponent<NetworkInfectionAuthority>(
                        out var infection) &&
                    infection.LifeState == PlayerLifeState.DeadGhost)
                {
                    continue;
                }

                _aliveClientIds.Add(pair.Key);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _lastSentServerTimes.Remove(clientId);
        }
    }
}
