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
    /// 유령끼리만 사용하는 독립 채팅 채널이다(GDD §17).
    /// 문자열은 NetworkList에 넣지 않고 서버가 현재 유령인 클라이언트에게만
    /// 대상 지정 Rpc로 보낸다. 생존자 클라이언트에는 원문이 전송되지 않는다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGhostChatAuthority : NetworkBehaviour
    {
        public static NetworkGhostChatAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        [SerializeField] private RoundBalanceConfig _config;

        private readonly List<MeetingChatEntry> _localMessages = new();
        private readonly Dictionary<ulong, double> _lastSentServerTimes = new();
        private readonly List<ulong> _ghostClientIds = new();

        public event Action MessagesChanged;

        public IReadOnlyList<MeetingChatEntry> LocalMessages => _localMessages;
        public RoundBalanceConfig Config => _config;
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
                Debug.LogError("[GhostChat] Round balance config is missing.", this);
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
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        public void SubmitMessage(string message)
        {
            if (!IsSpawned || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var sanitized = MeetingChatRules.Sanitize(message, MaximumLength);
            if (sanitized.Length > 0)
            {
                SubmitGhostMessageRpc(new FixedString512Bytes(sanitized));
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitGhostMessageRpc(
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

            var lifeState = client.PlayerObject.TryGetComponent<
                    NetworkInfectionAuthority>(out var infection)
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
            var rejection = GhostChatRules.Validate(
                roundState != null &&
                roundState.Outcome == RoundOutcome.None &&
                roundState.Phase != RoundPhase.RoleReveal,
                lifeState,
                avatar.IsConfigured,
                sanitized,
                serverTime,
                lastSentServerTime,
                _config.ChatMessageIntervalSeconds);
            if (rejection != GhostChatRejectionReason.None)
            {
                PublishRejectionRpc(
                    rejection,
                    RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
                return;
            }

            _lastSentServerTimes[senderClientId] = serverTime;
            CollectGhostClientIds();
            var payload = new FixedString512Bytes(sanitized);
            for (var index = 0; index < _ghostClientIds.Count; index++)
            {
                PublishGhostMessageRpc(
                    avatar.SlotIndex,
                    payload,
                    RpcTarget.Single(
                        _ghostClientIds[index],
                        RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishGhostMessageRpc(
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

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishRejectionRpc(
            GhostChatRejectionReason rejection,
            RpcParams rpcParams)
        {
            Debug.LogWarning(
                $"[GhostChat] Message rejected: {rejection}.",
                this);
        }

        private void CollectGhostClientIds()
        {
            _ghostClientIds.Clear();
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject != null &&
                    playerObject.TryGetComponent<NetworkInfectionAuthority>(
                        out var infection) &&
                    infection.LifeState == PlayerLifeState.DeadGhost)
                {
                    _ghostClientIds.Add(pair.Key);
                }
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            _lastSentServerTimes.Remove(clientId);
        }
    }
}
