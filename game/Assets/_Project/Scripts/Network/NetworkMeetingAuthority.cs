using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Meeting;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 회의 호출·투표·집계의 서버 권위 상태다.
    ///
    /// 표는 투표 중에 공개하지 않는다. 누가 누구를 찍었는지 실시간으로 보이면
    /// 토론이 무의미해지므로, 결과 단계에서 최종표만 공개한다.
    /// 퇴출된 플레이어의 역할도 공개하지 않는다(GDD §16.4).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMeetingAuthority : NetworkBehaviour
    {
        public const ulong NoExileTargetId = ulong.MaxValue;

        public readonly struct MeetingVoteRecord
        {
            public MeetingVoteRecord(
                ulong voterClientId,
                ulong targetClientId)
            {
                VoterClientId = voterClientId;
                TargetClientId = targetClientId;
            }

            public ulong VoterClientId { get; }
            public ulong TargetClientId { get; }
        }

        private readonly NetworkVariable<int> _castVoteCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _eligibleVoterCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Dictionary<ulong, uint> _lastProcessedSequences =
            new();
        private readonly List<MeetingVoteRecord> _localVoteRecords = new();

        private VoteTally _tally;
        private uint _localSequence;
        private ulong _localExiledClientId = NoExileTargetId;
        private bool _hasLocalResult;

        public static NetworkMeetingAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        public event Action MeetingStateChanged;

        public int CastVoteCount => _castVoteCount.Value;
        public int EligibleVoterCount => _eligibleVoterCount.Value;
        public bool HasLocalResult => _hasLocalResult;
        public ulong LocalExiledClientId => _localExiledClientId;
        public IReadOnlyList<MeetingVoteRecord> LocalVoteRecords =>
            _localVoteRecords;
        public MeetingRejectionReason LocalRejectionReason { get; private set; }

        public override void OnNetworkSpawn()
        {
            Current = this;
            CurrentChanged?.Invoke();
            _castVoteCount.OnValueChanged += HandleVoteCountChanged;
        }

        public override void OnNetworkDespawn()
        {
            _castVoteCount.OnValueChanged -= HandleVoteCountChanged;
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            _lastProcessedSequences.Clear();
            _localVoteRecords.Clear();
            _tally = null;
        }

        /// <summary>살아 있는 플레이어가 회의를 호출한다.</summary>
        public void RequestMeeting()
        {
            if (IsSpawned)
            {
                LocalRejectionReason = MeetingRejectionReason.None;
                RequestMeetingRpc(NextLocalSequence());
            }
        }

        /// <summary>투표한다. 기권은 NoExileTargetId를 넘긴다.</summary>
        public void RequestVote(ulong targetClientId)
        {
            if (IsSpawned)
            {
                RequestVoteRpc(targetClientId, NextLocalSequence());
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestMeetingRpc(
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsNewSequence(senderClientId, clientSequence))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;

            var roundState = NetworkRoundState.Current;
            if (roundState == null)
            {
                return;
            }

            var rejectionReason = MeetingCallRules.Validate(
                roundState.Phase == RoundPhase.Exploration,
                roundState.Outcome != RoundOutcome.None,
                IsClientAlive(senderClientId),
                roundState.ElapsedExplorationSeconds,
                roundState.Config.FirstMeetingLockSeconds,
                roundState.SecondsSinceLastMeeting,
                roundState.Config.MeetingCooldownSeconds,
                roundState.UsedMeetingCount,
                roundState.Config.MaximumMeetingCount);

            if (rejectionReason != MeetingRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, rejectionReason);
                return;
            }

            // 동시 호출이 와도 먼저 받은 유효 요청 하나만 승인된다(SDD §15.1).
            if (!roundState.ServerTryBeginMeeting())
            {
                PublishRejectionRpc(
                    senderClientId,
                    MeetingRejectionReason.NotExploring);
                return;
            }

            _tally = new VoteTally(CollectAliveClientIds());
            _eligibleVoterCount.Value = _tally.EligibleVoterCount;
            _castVoteCount.Value = 0;
            _hasLocalResult = false;
            _localExiledClientId = NoExileTargetId;
            Debug.Log(
                $"[Meeting] Called by client {senderClientId}. " +
                $"voters={_tally.EligibleVoterCount}.",
                this);
            PublishMeetingStartedRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestVoteRpc(
            ulong targetClientId,
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsNewSequence(senderClientId, clientSequence))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;

            var roundState = NetworkRoundState.Current;
            if (roundState == null || _tally == null ||
                roundState.Phase != RoundPhase.MeetingVote ||
                !IsClientAlive(senderClientId))
            {
                return;
            }

            var tallyTarget = targetClientId == NoExileTargetId
                ? VoteTally.AbstainTargetId
                : targetClientId;
            if (!_tally.TryCastVote(senderClientId, tallyTarget))
            {
                return;
            }

            _castVoteCount.Value = _tally.CastVoteCount;

            // 전원이 투표를 마치면 남은 시간을 기다리지 않는다.
            if (_tally.CastVoteCount >= _tally.EligibleVoterCount &&
                roundState.ServerTryFinishVoteEarly())
            {
                ServerResolveMeeting();
            }
        }

        /// <summary>
        /// 결과 단계 진입 시 서버가 한 번만 호출한다.
        /// </summary>
        public void ServerResolveMeeting()
        {
            if (!IsServer || _tally == null)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            var hasExile = _tally.TryResolveExile(out var exiledClientId);
            var finalVotes = _tally.CreateFinalVoteSnapshot();
            var voterIds = new List<ulong>(finalVotes.Keys);
            voterIds.Sort();
            var publishedVoterIds = new ulong[voterIds.Count];
            var publishedTargetIds = new ulong[voterIds.Count];
            for (var index = 0; index < voterIds.Count; index++)
            {
                var voterId = voterIds[index];
                publishedVoterIds[index] = voterId;
                publishedTargetIds[index] = finalVotes[voterId];
            }

            if (hasExile && roundState != null)
            {
                roundState.ServerApplyExile(exiledClientId);
            }

            Debug.Log(
                hasExile
                    ? $"[Meeting] Exiled client {exiledClientId}."
                    : "[Meeting] No exile (tie or abstain majority).",
                this);

            PublishResultRpc(
                hasExile ? exiledClientId : NoExileTargetId,
                publishedVoterIds,
                publishedTargetIds);
            _tally = null;
        }

        /// <summary>진행 중인 회의의 투표권과 기존 표를 재접속 ID로 옮긴다.</summary>
        public bool ServerRebindPlayer(
            ulong previousClientId,
            ulong currentClientId)
        {
            if (!IsServer || _tally == null ||
                !_tally.RebindPlayer(previousClientId, currentClientId))
            {
                return false;
            }

            _lastProcessedSequences.Remove(previousClientId);
            _castVoteCount.Value = _tally.CastVoteCount;
            _eligibleVoterCount.Value = _tally.EligibleVoterCount;
            return true;
        }

        private List<ulong> CollectAliveClientIds()
        {
            var aliveClientIds = new List<ulong>();
            if (NetworkManager == null)
            {
                return aliveClientIds;
            }

            foreach (var client in NetworkManager.ConnectedClients)
            {
                if (IsClientAlive(client.Key))
                {
                    aliveClientIds.Add(client.Key);
                }
            }

            return aliveClientIds;
        }

        private bool IsClientAlive(ulong clientId)
        {
            if (NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                !avatar.HasAssignedRole)
            {
                return false;
            }

            var infection =
                client.PlayerObject
                    .GetComponent<NetworkInfectionAuthority>();
            return infection == null ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishMeetingStartedRpc()
        {
            _hasLocalResult = false;
            _localExiledClientId = NoExileTargetId;
            _localVoteRecords.Clear();
            LocalRejectionReason = MeetingRejectionReason.None;
            MeetingStateChanged?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishResultRpc(
            ulong exiledClientId,
            ulong[] voterClientIds,
            ulong[] targetClientIds)
        {
            _hasLocalResult = true;
            _localExiledClientId = exiledClientId;
            _localVoteRecords.Clear();
            var recordCount = Math.Min(
                voterClientIds?.Length ?? 0,
                targetClientIds?.Length ?? 0);
            for (var index = 0; index < recordCount; index++)
            {
                _localVoteRecords.Add(
                    new MeetingVoteRecord(
                        voterClientIds[index],
                        targetClientIds[index]));
            }

            MeetingStateChanged?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            MeetingRejectionReason rejectionReason)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                LocalRejectionReason = rejectionReason;
                MeetingStateChanged?.Invoke();
                Debug.LogWarning(
                    $"[Meeting] Request rejected: {rejectionReason}.",
                    this);
            }
        }

        private void HandleVoteCountChanged(
            int previousValue,
            int currentValue)
        {
            MeetingStateChanged?.Invoke();
        }

        private bool IsNewSequence(ulong clientId, uint clientSequence)
        {
            return !_lastProcessedSequences.TryGetValue(
                       clientId,
                       out var previousSequence) ||
                   clientSequence > previousSequence;
        }

        private uint NextLocalSequence()
        {
            _localSequence++;
            if (_localSequence == 0)
            {
                _localSequence = 1;
            }

            return _localSequence;
        }
    }
}
