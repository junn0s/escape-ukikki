using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LobbyRosterNetwork : NetworkBehaviour
    {
        private readonly NetworkList<LobbyPlayerNetworkState> _players = new();
        private readonly LobbyRosterService _service = new();
        private readonly RoleAssignmentService _roleAssignmentService = new();
        private readonly NetworkVariable<bool> _isStartingGame = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action RosterChanged;
        public event Action<string> RequestRejected;

        public int PlayerCount => _players.Count;
        public bool IsStartingGame => _isStartingGame.Value;

        public override void OnNetworkSpawn()
        {
            _players.OnListChanged += HandleListChanged;
            _isStartingGame.OnValueChanged += HandleStartingGameChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

                foreach (var clientId in NetworkManager.ConnectedClientsIds)
                {
                    RegisterConnectedPlayer(clientId);
                }
            }

            RosterChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _players.OnListChanged -= HandleListChanged;
            _isStartingGame.OnValueChanged -= HandleStartingGameChanged;

            if (NetworkManager != null && IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        public IReadOnlyList<LobbyPlayerState> CreateSnapshot()
        {
            var snapshot = new List<LobbyPlayerState>(_players.Count);
            for (var index = 0; index < _players.Count; index++)
            {
                snapshot.Add(_players[index].ToPlayerState());
            }

            return snapshot;
        }

        public bool TryGetLocalPlayer(out LobbyPlayerState player)
        {
            if (!IsSpawned || NetworkManager == null)
            {
                player = default;
                return false;
            }

            var localClientId = NetworkManager.LocalClientId;
            for (var index = 0; index < _players.Count; index++)
            {
                var candidate = _players[index];
                if (candidate.ClientId == localClientId)
                {
                    player = candidate.ToPlayerState();
                    return true;
                }
            }

            player = default;
            return false;
        }

        public void RequestSetReady(bool isReady)
        {
            if (IsSpawned)
            {
                SetReadyRpc(isReady, default);
            }
        }

        public void RequestSetColor(LobbyPlayerColor color)
        {
            if (IsSpawned)
            {
                SetColorRpc(color, default);
            }
        }

        public void RequestStartGame(bool allowDevelopmentStart)
        {
            if (IsSpawned)
            {
                StartGameRpc(allowDevelopmentStart, default);
            }
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(bool isReady, RpcParams rpcParams)
        {
            ApplyRequest(
                rpcParams.Receive.SenderClientId,
                _service.SetReady(
                    rpcParams.Receive.SenderClientId,
                    isReady));
        }

        [Rpc(SendTo.Server)]
        private void SetColorRpc(LobbyPlayerColor color, RpcParams rpcParams)
        {
            ApplyRequest(
                rpcParams.Receive.SenderClientId,
                _service.SetColor(
                    rpcParams.Receive.SenderClientId,
                    color));
        }

        [Rpc(SendTo.Server)]
        private void StartGameRpc(
            bool allowDevelopmentStart,
            RpcParams rpcParams)
        {
            var requesterClientId = rpcParams.Receive.SenderClientId;
            if (_isStartingGame.Value)
            {
                PublishRequestFailureRpc(
                    requesterClientId,
                    LobbyRosterFailureKind.StartAlreadyInProgress);
                return;
            }

            var canUseDevelopmentStart =
                allowDevelopmentStart &&
                (Application.isEditor || Debug.isDebugBuild);
            var result = _service.CanStart(
                requesterClientId,
                canUseDevelopmentStart);
            if (!result.Succeeded)
            {
                PublishRequestFailureRpc(
                    requesterClientId,
                    result.FailureKind);
                return;
            }

            if (!TryApplyAllPlayerStates())
            {
                PublishRequestFailureRpc(
                    requesterClientId,
                    LobbyRosterFailureKind.PlayerObjectUnavailable);
                return;
            }

            if (!TryAssignAllPlayerRoles())
            {
                PublishRequestFailureRpc(
                    requesterClientId,
                    LobbyRosterFailureKind.PlayerObjectUnavailable);
                return;
            }

            // 플레이어 NetworkObject는 씬 전환에도 살아남는다. 초기화하지 않으면
            // 지난 판의 유령 상태와 소지품이 새 판으로 넘어온다.
            ResetAllPlayerRoundStates();

            _isStartingGame.Value = true;
            var sceneManager = NetworkManager != null
                ? NetworkManager.SceneManager
                : null;
            var status = sceneManager != null
                ? sceneManager.LoadScene(
                    NetworkPlayerAvatar.LaboratorySceneName,
                    LoadSceneMode.Single)
                : SceneEventProgressStatus.SceneManagementNotEnabled;
            if (status == SceneEventProgressStatus.Started)
            {
                return;
            }

            _isStartingGame.Value = false;
            PublishRequestFailureRpc(
                requesterClientId,
                LobbyRosterFailureKind.SceneTransitionFailed);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRequestFailureRpc(
            ulong targetClientId,
            LobbyRosterFailureKind failureKind)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                RequestRejected?.Invoke(CreateUserMessage(failureKind));
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            RegisterConnectedPlayer(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var result = _service.RemovePlayer(clientId);
            if (result.Succeeded)
            {
                SynchronizePlayers();
            }
        }

        private void RegisterConnectedPlayer(ulong clientId)
        {
            var result = _service.AddPlayer(
                clientId,
                clientId == NetworkManager.ServerClientId);
            if (result.Succeeded)
            {
                SynchronizePlayers();
            }
        }

        private void ApplyRequest(ulong senderClientId, LobbyRosterResult result)
        {
            if (result.Succeeded)
            {
                SynchronizePlayers();
                return;
            }

            PublishRequestFailureRpc(senderClientId, result.FailureKind);
        }

        private void SynchronizePlayers()
        {
            _players.Clear();
            foreach (var player in _service.Players)
            {
                _players.Add(new LobbyPlayerNetworkState(player));
                TryApplyPlayerState(player);
            }
        }

        private void HandleListChanged(
            NetworkListEvent<LobbyPlayerNetworkState> changeEvent)
        {
            RosterChanged?.Invoke();
        }

        private void HandleStartingGameChanged(
            bool previousValue,
            bool currentValue)
        {
            RosterChanged?.Invoke();
        }

        private bool TryApplyAllPlayerStates()
        {
            foreach (var player in _service.Players)
            {
                if (!TryApplyPlayerState(player))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryApplyPlayerState(LobbyPlayerState player)
        {
            if (!IsServer || NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    player.ClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar))
            {
                return false;
            }

            return avatar.ServerApplyLobbyState(player);
        }

        /// <summary>
        /// 새 판을 시작하기 전 각 플레이어의 라운드 상태를 되돌린다.
        /// 로비로 돌아온 뒤 다시 시작하는 경로(mvp-scope §7 "라운드 완료")에서
        /// 지난 판의 유령·감염·소지품이 남지 않도록 한다.
        /// </summary>
        private void ResetAllPlayerRoundStates()
        {
            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                if (playerObject.TryGetComponent<NetworkInfectionAuthority>(
                        out var infection))
                {
                    infection.ServerResetForNewRound();
                }

                if (playerObject.TryGetComponent<
                        NetworkAntidoteInventoryAuthority>(
                        out var inventory))
                {
                    inventory.ServerResetForNewRound();
                }

                if (playerObject.TryGetComponent<
                        NetworkPlayerMissionJournal>(out var journal))
                {
                    journal.ServerResetForNewRound();
                }
            }
        }

        private bool TryAssignAllPlayerRoles()
        {
            if (!IsServer || NetworkManager == null ||
                _service.Players.Count == 0)
            {
                return false;
            }

            var participantClientIds =
                new ulong[_service.Players.Count];
            for (var index = 0;
                 index < _service.Players.Count;
                 index++)
            {
                participantClientIds[index] =
                    _service.Players[index].ClientId;
            }

            var villainIndex = UnityEngine.Random.Range(
                0,
                participantClientIds.Length);
            var assignments = _roleAssignmentService.AssignRoles(
                participantClientIds,
                villainIndex);
            foreach (var assignment in assignments)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(
                        assignment.ClientId,
                        out var client) ||
                    client.PlayerObject == null ||
                    !client.PlayerObject.TryGetComponent<
                        NetworkPlayerAvatar>(out var avatar) ||
                    !avatar.ServerAssignRole(assignment.Role))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CreateUserMessage(
            LobbyRosterFailureKind failureKind)
        {
            return failureKind switch
            {
                LobbyRosterFailureKind.ColorAlreadyTaken =>
                    "이미 다른 플레이어가 사용 중인 색상입니다.",
                LobbyRosterFailureKind.InvalidColor =>
                    "선택할 수 없는 색상입니다.",
                LobbyRosterFailureKind.LobbyFull =>
                    "로비 인원이 가득 찼습니다.",
                LobbyRosterFailureKind.NotHost =>
                    "호스트만 게임을 시작할 수 있습니다.",
                LobbyRosterFailureKind.NotEnoughPlayers =>
                    "6명이 모두 참가해야 시작할 수 있습니다.",
                LobbyRosterFailureKind.PlayersNotReady =>
                    "모든 플레이어가 준비해야 시작할 수 있습니다.",
                LobbyRosterFailureKind.StartAlreadyInProgress =>
                    "이미 게임을 시작하고 있습니다.",
                LobbyRosterFailureKind.PlayerObjectUnavailable =>
                    "플레이어 생성 준비가 끝나지 않았습니다. 잠시 후 다시 시도해 주세요.",
                LobbyRosterFailureKind.SceneTransitionFailed =>
                    "연구소 씬을 불러오지 못했습니다.",
                _ => "로비 요청을 처리하지 못했습니다."
            };
        }
    }
}
