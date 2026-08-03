using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MonkeyLab.Network
{
    public sealed class GameSessionController : MonoBehaviour
    {
        public static GameSessionController Current { get; private set; }

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;

        private GameSessionService _service;
        private readonly Dictionary<ulong, string> _approvedIdentities = new();
        private readonly Dictionary<ulong, string> _activeIdentities = new();
        private readonly Dictionary<string, ulong> _activeClientsByIdentity = new();
        private readonly Dictionary<ulong, PlayerReconnectSnapshot>
            _latestSnapshots = new();
        private readonly Dictionary<string, PendingReconnectSnapshot>
            _pendingReconnects = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, string> _restoreIdentities = new();

        public GameSessionState State => EnsureService().State;
        public GameSessionInfo CurrentSession => EnsureService().CurrentSession;
        public string FailureMessage => EnsureService().FailureMessage;
        public bool IsBusy => EnsureService().IsBusy;
        public NetworkManager NetworkManager => _networkManager;
        public UnityTransport Transport => _transport;

        public void Configure(NetworkManager networkManager, UnityTransport transport)
        {
            _networkManager = networkManager;
            _transport = transport;
        }

        public Task CreateSessionAsync()
        {
            PrepareConnectionIdentity();
            return EnsureService().CreateSessionAsync();
        }

        public Task JoinSessionAsync(string joinCode)
        {
            PrepareConnectionIdentity();
            return EnsureService().JoinSessionAsync(joinCode);
        }

        public Task LeaveSessionAsync()
        {
            return EnsureService().LeaveSessionAsync();
        }

        public Task ReconnectSessionAsync()
        {
            PrepareConnectionIdentity();
            return EnsureService().ReconnectSessionAsync();
        }

        private void Awake()
        {
            if (_networkManager == null || _transport == null)
            {
                Debug.LogError(
                    "[Session] NetworkManager and UnityTransport are required.",
                    this);
                enabled = false;
                return;
            }

            if (_networkManager.NetworkConfig.NetworkTransport != _transport)
            {
                Debug.LogError(
                    "[Session] NetworkManager must use the configured UnityTransport.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback =
                HandleConnectionApproval;
            _networkManager.OnClientConnectedCallback +=
                HandleClientConnected;
            _networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;
            EnsureService();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                if (_networkManager.ConnectionApprovalCallback ==
                    HandleConnectionApproval)
                {
                    _networkManager.ConnectionApprovalCallback = null;
                }

                _networkManager.OnClientConnectedCallback -=
                    HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// NGO가 연결 종료 PlayerObject를 없애기 직전에 호출한다.
        /// 서버 메모리에만 저장하므로 역할·미션·레시피가 다른 참가자에게 노출되지 않는다.
        /// </summary>
        public void ServerCaptureBeforeDisconnect(
            ulong clientId,
            GameObject playerObject)
        {
            if (_networkManager == null || !_networkManager.IsServer ||
                playerObject == null ||
                !TryCreateSnapshot(playerObject, out var snapshot))
            {
                return;
            }

            _latestSnapshots[clientId] = snapshot;
        }

        /// <summary>유예가 끝난 신원은 이후 같은 PlayerId로 들어와도 복원하지 않는다.</summary>
        public void ServerExpireReconnect(ulong previousClientId)
        {
            if (_networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            _latestSnapshots.Remove(previousClientId);
            string expiredIdentity = null;
            foreach (var pair in _pendingReconnects)
            {
                if (pair.Value.PreviousClientId == previousClientId)
                {
                    expiredIdentity = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(expiredIdentity))
            {
                _pendingReconnects.Remove(expiredIdentity);
            }
        }

        /// <summary>
        /// 재접속 유예가 만료된 생존자의 서버 스냅샷에서 미완료 미션을
        /// 공용 복구 목록으로 옮긴다. 스냅샷은 역할·완료 목록을 포함하지만
        /// 전송하지 않고 서버 메모리 안에서만 사용한다(GDD §19.2).
        /// </summary>
        public int ServerPromoteRecoveryMissions(ulong previousClientId)
        {
            if (_networkManager == null || !_networkManager.IsServer ||
                !_latestSnapshots.TryGetValue(
                    previousClientId,
                    out var snapshot) ||
                snapshot.Role != PlayerRole.Survivor ||
                NetworkRoundState.Current == null)
            {
                return 0;
            }

            return NetworkRoundState.Current.ServerRegisterRecoveryMissions(
                previousClientId,
                snapshot.AssignedMissionIds,
                snapshot.CompletedMissionIds);
        }

        private void PrepareConnectionIdentity()
        {
            var onlineServices = new UnityOnlineServicesGateway();
            if (!onlineServices.IsSignedIn ||
                !ConnectionIdentityPayload.TryEncode(
                    onlineServices.PlayerId,
                    out var payload))
            {
                Debug.LogError(
                    "[Session] A signed-in Unity PlayerId is required before connecting.",
                    this);
                _networkManager.NetworkConfig.ConnectionData =
                    Array.Empty<byte>();
                return;
            }

            _networkManager.NetworkConfig.ConnectionData = payload;
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Pending = false;
            response.CreatePlayerObject = true;
            if (!ConnectionIdentityPayload.TryDecode(
                    request.Payload,
                    out var playerId))
            {
                response.Approved = false;
                response.Reason = "온라인 참가자 신원을 확인하지 못했습니다.";
                return;
            }

            if (_activeClientsByIdentity.TryGetValue(
                    playerId,
                    out var activeClientId) &&
                activeClientId != request.ClientNetworkId)
            {
                response.Approved = false;
                response.Reason = "같은 참가자가 이미 연결되어 있습니다.";
                return;
            }

            _approvedIdentities[request.ClientNetworkId] = playerId;
            if (_pendingReconnects.TryGetValue(
                    playerId,
                    out var pending) &&
                NetworkDisconnectPolicyAuthority.Current != null &&
                NetworkDisconnectPolicyAuthority.Current
                    .ServerCanRestore(pending.PreviousClientId))
            {
                _restoreIdentities[request.ClientNetworkId] = playerId;
            }

            response.Approved = true;
            response.Reason = string.Empty;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            if (!_approvedIdentities.TryGetValue(clientId, out var playerId))
            {
                // 호스트 자신은 시작 순서에 따라 승인 캐시보다 먼저 콜백될 수 있다.
                if (clientId != Unity.Netcode.NetworkManager.ServerClientId ||
                    !ConnectionIdentityPayload.TryDecode(
                        _networkManager.NetworkConfig.ConnectionData,
                        out playerId))
                {
                    return;
                }
            }

            _activeIdentities[clientId] = playerId;
            _activeClientsByIdentity[playerId] = clientId;
            if (!_restoreIdentities.Remove(clientId) ||
                !_pendingReconnects.TryGetValue(
                    playerId,
                    out var pending))
            {
                return;
            }

            if (!TryRestoreSnapshot(clientId, pending.Snapshot))
            {
                Debug.LogError(
                    "[Reconnect] PlayerObject was not ready for state restoration.",
                    this);
                return;
            }

            _pendingReconnects.Remove(playerId);
            _latestSnapshots.Remove(pending.PreviousClientId);
            NetworkRoundState.Current?.ServerRebindPlayer(
                pending.PreviousClientId,
                clientId);
            NetworkDisconnectPolicyAuthority.Current?
                .ServerCompleteReconnect(
                    pending.PreviousClientId,
                    clientId);
            Debug.Log(
                "[Reconnect] Restored role, missions, infection and inventory.",
                this);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_networkManager == null || !_networkManager.IsServer ||
                clientId == Unity.Netcode.NetworkManager.ServerClientId ||
                !_activeIdentities.Remove(clientId, out var playerId))
            {
                return;
            }

            _approvedIdentities.Remove(clientId);
            _activeClientsByIdentity.Remove(playerId);
            if (!_latestSnapshots.TryGetValue(clientId, out var snapshot) ||
                NetworkDisconnectPolicyAuthority.Current == null ||
                !NetworkDisconnectPolicyAuthority.Current
                    .ServerBeginDisconnect(clientId, snapshot.Role))
            {
                _latestSnapshots.Remove(clientId);
                return;
            }

            _pendingReconnects[playerId] = new PendingReconnectSnapshot(
                clientId,
                snapshot);
        }

        private bool TryRestoreSnapshot(
            ulong clientId,
            PlayerReconnectSnapshot snapshot)
        {
            if (!_networkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                !client.PlayerObject.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                !client.PlayerObject.TryGetComponent<
                    NetworkInfectionAuthority>(out var infection) ||
                !client.PlayerObject.TryGetComponent<
                    NetworkAntidoteInventoryAuthority>(out var inventory))
            {
                return false;
            }

            return avatar.ServerRestoreReconnectSnapshot(
                       snapshot.SlotIndex,
                       snapshot.Color,
                       snapshot.Nickname,
                       snapshot.Role,
                       snapshot.Position) &&
                   journal.ServerRestoreReconnectSnapshot(
                       snapshot.AssignedMissionIds,
                       snapshot.CompletedMissionIds) &&
                   infection.ServerRestoreReconnectSnapshot(
                       snapshot.LifeState,
                       snapshot.DurationAtBiteSeconds,
                       snapshot.RemainingInfectionSeconds,
                       snapshot.ToxicityTierAtBite) &&
                   inventory.ServerRestoreReconnectSnapshot(
                       snapshot.CarriedAntidoteCount,
                       snapshot.HasRecipe);
        }

        private static bool TryCreateSnapshot(
            GameObject playerObject,
            out PlayerReconnectSnapshot snapshot)
        {
            snapshot = null;
            if (!playerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                !avatar.HasAssignedRole ||
                !playerObject.TryGetComponent<
                    NetworkPlayerMissionJournal>(out var journal) ||
                !journal.ServerCreateReconnectSnapshot(
                    out var assignedMissionIds,
                    out var completedMissionIds) ||
                !playerObject.TryGetComponent<
                    NetworkInfectionAuthority>(out var infection) ||
                !playerObject.TryGetComponent<
                    NetworkAntidoteInventoryAuthority>(out var inventory))
            {
                return false;
            }

            snapshot = new PlayerReconnectSnapshot(
                avatar.SlotIndex,
                avatar.Color,
                avatar.Nickname,
                avatar.Role,
                playerObject.transform.position,
                assignedMissionIds,
                completedMissionIds,
                infection.LifeState,
                infection.DurationAtBiteSeconds,
                infection.RemainingSeconds,
                infection.ToxicityTierAtBite,
                inventory.CarriedCount,
                inventory.HasRecipe);
            return true;
        }

        private GameSessionService EnsureService()
        {
            return _service ??= new GameSessionService(new UnityGameSessionGateway());
        }

        private sealed class PendingReconnectSnapshot
        {
            public PendingReconnectSnapshot(
                ulong previousClientId,
                PlayerReconnectSnapshot snapshot)
            {
                PreviousClientId = previousClientId;
                Snapshot = snapshot;
            }

            public ulong PreviousClientId { get; }
            public PlayerReconnectSnapshot Snapshot { get; }
        }

        private sealed class PlayerReconnectSnapshot
        {
            public PlayerReconnectSnapshot(
                byte slotIndex,
                LobbyPlayerColor color,
                string nickname,
                PlayerRole role,
                Vector3 position,
                ulong[] assignedMissionIds,
                ulong[] completedMissionIds,
                PlayerLifeState lifeState,
                float durationAtBiteSeconds,
                float remainingInfectionSeconds,
                int toxicityTierAtBite,
                int carriedAntidoteCount,
                bool hasRecipe)
            {
                SlotIndex = slotIndex;
                Color = color;
                Nickname = nickname;
                Role = role;
                Position = position;
                AssignedMissionIds = assignedMissionIds;
                CompletedMissionIds = completedMissionIds;
                LifeState = lifeState;
                DurationAtBiteSeconds = durationAtBiteSeconds;
                RemainingInfectionSeconds = remainingInfectionSeconds;
                ToxicityTierAtBite = toxicityTierAtBite;
                CarriedAntidoteCount = carriedAntidoteCount;
                HasRecipe = hasRecipe;
            }

            public byte SlotIndex { get; }
            public LobbyPlayerColor Color { get; }
            public string Nickname { get; }
            public PlayerRole Role { get; }
            public Vector3 Position { get; }
            public ulong[] AssignedMissionIds { get; }
            public ulong[] CompletedMissionIds { get; }
            public PlayerLifeState LifeState { get; }
            public float DurationAtBiteSeconds { get; }
            public float RemainingInfectionSeconds { get; }
            public int ToxicityTierAtBite { get; }
            public int CarriedAntidoteCount { get; }
            public bool HasRecipe { get; }
        }
    }
}
