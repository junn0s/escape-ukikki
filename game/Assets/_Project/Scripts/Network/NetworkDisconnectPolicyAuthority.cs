using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 라운드 중 연결이 끊긴 참가자를 처리한다(GDD §19.2).
    /// 30초 유예 안에는 판정을 확정하지 않고, 유예가 지나면
    /// 빌런이면 생존자 승리, 생존자면 현실 생존자 수에서 제외한다.
    /// 실제 복원 스냅샷과 Unity 인증 PlayerId↔새 clientId 연결은
    /// GameSessionController가 담당하고, 이 클래스는 유예와 승패만 판정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkDisconnectPolicyAuthority : NetworkBehaviour
    {
        public static NetworkDisconnectPolicyAuthority Current
        {
            get;
            private set;
        }

        [SerializeField] private RoundBalanceConfig _config;

        /// <summary>연결이 끊긴 참가자의 역할과 유예 만료 시각이다.</summary>
        private readonly Dictionary<ulong, PendingReturn> _pendingReturns =
            new();

        public RoundBalanceConfig Config => _config;

        /// <summary>유예를 기다리는 생존자 수다. 현실 생존자 수에 더해 센다.</summary>
        public int PendingSurvivorCount { get; private set; }

        public void Configure(RoundBalanceConfig config)
        {
            _config = config;
        }

        public override void OnNetworkSpawn()
        {
            if (_config == null)
            {
                Debug.LogError(
                    "[Disconnect] Round balance config is missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
        }

        public override void OnNetworkDespawn()
        {
            _pendingReturns.Clear();
            PendingSurvivorCount = 0;
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            ResolveExpiredReturns();
        }

        /// <summary>
        /// PlayerObject가 사라지기 전에 만든 서버 스냅샷의 역할로 유예를 시작한다.
        /// </summary>
        public bool ServerBeginDisconnect(ulong clientId, PlayerRole role)
        {
            var roundState = NetworkRoundState.Current;
            if (!IsServer || roundState == null ||
                roundState.Phase == RoundPhase.RoundResult ||
                (role != PlayerRole.Survivor &&
                 role != PlayerRole.Villain))
            {
                return false;
            }

            _pendingReturns[clientId] = new PendingReturn
            {
                Role = role,
                DeadlineTime = Time.time + _config.DisconnectGraceSeconds
            };
            RefreshPendingSurvivorCount();
            Debug.Log(
                $"[Disconnect] Waiting {_config.DisconnectGraceSeconds:0}s " +
                "for a participant to return.",
                this);
            return true;
        }

        public bool ServerCanRestore(ulong previousClientId)
        {
            return IsServer &&
                   _pendingReturns.TryGetValue(
                       previousClientId,
                       out var pending) &&
                   Time.time < pending.DeadlineTime;
        }

        public bool ServerCompleteReconnect(
            ulong previousClientId,
            ulong currentClientId)
        {
            if (!IsServer ||
                !_pendingReturns.Remove(previousClientId))
            {
                return false;
            }

            RefreshPendingSurvivorCount();
            Debug.Log(
                $"[Reconnect] Client {currentClientId} returned during grace.",
                this);
            NetworkRoundState.Current?.ServerReevaluateWinConditions();
            return true;
        }

        private void ResolveExpiredReturns()
        {
            if (_pendingReturns.Count == 0)
            {
                return;
            }

            var expiredClientIds = new List<ulong>();
            var pendingSurvivors = 0;
            foreach (var pair in _pendingReturns)
            {
                if (Time.time >= pair.Value.DeadlineTime)
                {
                    expiredClientIds.Add(pair.Key);
                    continue;
                }

                if (pair.Value.Role == PlayerRole.Survivor)
                {
                    pendingSurvivors++;
                }
            }

            PendingSurvivorCount = pendingSurvivors;

            for (var index = 0; index < expiredClientIds.Count; index++)
            {
                var clientId = expiredClientIds[index];
                var pending = _pendingReturns[clientId];
                _pendingReturns.Remove(clientId);

                if (pending.Role == PlayerRole.Villain)
                {
                    GameSessionController.Current?
                        .ServerExpireReconnect(clientId);
                    // 빌런이 돌아오지 않으면 생존자 승리다(GDD §19.2).
                    NetworkRoundState.Current?
                        .ServerApplyVillainAbandonment();
                    Debug.Log(
                        "[Disconnect] The villain did not return; survivors win.",
                        this);
                    continue;
                }

                var recoveryMissionCount =
                    GameSessionController.Current?
                        .ServerPromoteRecoveryMissions(clientId) ?? 0;
                GameSessionController.Current?
                    .ServerExpireReconnect(clientId);
                // 생존자는 이미 ConnectedClients에서 빠졌으므로 현실 생존자 수에서
                // 자동으로 제외된다. 남은 개인 미션은 공용 복구 목록으로 옮긴다.
                Debug.Log(
                    $"[Disconnect] A survivor did not return; " +
                    $"{recoveryMissionCount} missions became public recovery work.",
                    this);
            }

            if (expiredClientIds.Count > 0)
            {
                RefreshPendingSurvivorCount();
                NetworkRoundState.Current?.ServerReevaluateWinConditions();
            }
        }

        private void RefreshPendingSurvivorCount()
        {
            var count = 0;
            foreach (var pending in _pendingReturns.Values)
            {
                if (pending.Role == PlayerRole.Survivor &&
                    Time.time < pending.DeadlineTime)
                {
                    count++;
                }
            }

            PendingSurvivorCount = count;
        }

        private struct PendingReturn
        {
            public PlayerRole Role;
            public float DeadlineTime;
        }
    }
}
