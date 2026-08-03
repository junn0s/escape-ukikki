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
    ///
    /// 신원을 유지한 실제 재접속(같은 슬롯·역할·미션 복원)은 구현하지 않았다.
    /// MPS Session에서 재접속한 클라이언트는 새 clientId를 받기 때문에
    /// Unity 인증 PlayerId를 연결 승인 페이로드로 넘기는 작업이 선행되어야 한다.
    /// 유예 창은 "성급한 승패 확정"을 막는 부분까지만 담당한다.
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

        /// <summary>
        /// 연결 종료 콜백 시점에는 PlayerObject가 이미 사라져 역할을 읽을 수 없다.
        /// 그래서 살아 있는 동안 역할을 미리 캐시해 둔다.
        /// </summary>
        private readonly Dictionary<ulong, PlayerRole> _knownRoles = new();

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

            _pendingReturns.Clear();
            _knownRoles.Clear();
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

            RefreshKnownRoles();
            ResolveExpiredReturns();
        }

        private void RefreshKnownRoles()
        {
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject != null &&
                    playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) &&
                    avatar.HasAssignedRole)
                {
                    _knownRoles[pair.Key] = avatar.Role;
                }
            }
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
                    // 빌런이 돌아오지 않으면 생존자 승리다(GDD §19.2).
                    NetworkRoundState.Current?
                        .ServerApplyVillainAbandonment();
                    Debug.Log(
                        "[Disconnect] The villain did not return; survivors win.",
                        this);
                    continue;
                }

                // 생존자는 이미 ConnectedClients에서 빠졌으므로 현실 생존자 수에서
                // 자동으로 제외된다. 유예 카운트만 내린다.
                Debug.Log(
                    "[Disconnect] A survivor did not return and is treated as lost.",
                    this);
            }

            if (expiredClientIds.Count > 0)
            {
                NetworkRoundState.Current?.ServerReevaluateWinConditions();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var roundState = NetworkRoundState.Current;
            var isRoundActive =
                roundState != null &&
                roundState.Phase != RoundPhase.RoundResult;
            if (!isRoundActive || !_knownRoles.TryGetValue(clientId, out var role))
            {
                _knownRoles.Remove(clientId);
                return;
            }

            _knownRoles.Remove(clientId);
            _pendingReturns[clientId] = new PendingReturn
            {
                Role = role,
                DeadlineTime = Time.time + _config.DisconnectGraceSeconds
            };

            Debug.Log(
                $"[Disconnect] Waiting {_config.DisconnectGraceSeconds:0}s " +
                "for a participant to return.",
                this);
        }

        private struct PendingReturn
        {
            public PlayerRole Role;
            public float DeadlineTime;
        }
    }
}
