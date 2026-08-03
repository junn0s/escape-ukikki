using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// CCTV와 보안실 서버 로그다.
    ///
    /// 프로젝트 50% 미만에서는 잠겨 있고, 50% 도달 사건으로 서버가 활성화한다
    /// (SDD §14.3). 로그에는 작동 시각과 방만 남기고 신원은 남기지 않는다.
    ///
    /// 로그는 전원에게 복제한다. 단말기에 접근해야 볼 수 있는 정보이지만
    /// 중간 참가·재접속에서도 같은 로그를 봐야 하므로 NetworkList를 쓴다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkSecurityTerminalAuthority : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> _isUnlocked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkList<SecurityLogEntry> _logEntries =
            new(
                new List<SecurityLogEntry>(),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _activeViewerClientId = new(
            NoViewerClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [SerializeField] private string[] _roomOrder = Array.Empty<string>();
        [SerializeField] private string[] _roomDisplayNames =
            Array.Empty<string>();
        [SerializeField] private Vector2 _terminalWorldPosition;
        [SerializeField, Min(0.5f)] private float _interactionRange = 2.5f;

        public const ulong NoViewerClientId = ulong.MaxValue;

        public static NetworkSecurityTerminalAuthority Current
        {
            get;
            private set;
        }

        public static event Action CurrentChanged;

        public event Action TerminalStateChanged;

        public bool IsUnlocked => _isUnlocked.Value;
        public bool HasActiveViewer =>
            _activeViewerClientId.Value != NoViewerClientId;
        public ulong ActiveViewerClientId => _activeViewerClientId.Value;
        public bool IsLocalClientViewing =>
            NetworkManager != null &&
            _activeViewerClientId.Value == NetworkManager.LocalClientId;
        public int LogEntryCount => _logEntries.Count;

        public void Configure(
            string[] roomOrder,
            string[] roomDisplayNames)
        {
            _roomOrder = roomOrder ?? Array.Empty<string>();
            _roomDisplayNames =
                roomDisplayNames ?? Array.Empty<string>();
        }

        public void Configure(
            string[] roomOrder,
            string[] roomDisplayNames,
            Vector2 terminalWorldPosition,
            float interactionRange)
        {
            Configure(roomOrder, roomDisplayNames);
            _terminalWorldPosition = terminalWorldPosition;
            _interactionRange = Mathf.Max(0.5f, interactionRange);
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            CurrentChanged?.Invoke();
            _isUnlocked.OnValueChanged += HandleUnlockChanged;
            _activeViewerClientId.OnValueChanged += HandleViewerChanged;
            _logEntries.OnListChanged += HandleLogChanged;

            if (IsServer)
            {
                _isUnlocked.Value = false;
                _activeViewerClientId.Value = NoViewerClientId;
                _logEntries.Clear();
                if (NetworkManager != null)
                {
                    NetworkManager.OnClientDisconnectCallback +=
                        HandleClientDisconnected;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            _isUnlocked.OnValueChanged -= HandleUnlockChanged;
            _activeViewerClientId.OnValueChanged -= HandleViewerChanged;
            _logEntries.OnListChanged -= HandleLogChanged;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState == null)
            {
                return;
            }

            if (!_isUnlocked.Value &&
                roundState.ProjectMilestone >=
                ProjectMilestone.SecurityAccess)
            {
                _isUnlocked.Value = true;
                Debug.Log(
                    "[Security] CCTV and server log unlocked at 50%.",
                    this);
            }

            if (HasActiveViewer && !ServerCanContinueViewing(
                    _activeViewerClientId.Value,
                    roundState))
            {
                _activeViewerClientId.Value = NoViewerClientId;
            }
        }

        public void RequestViewing()
        {
            if (IsSpawned)
            {
                RequestViewingRpc();
            }
        }

        public void RequestStopViewing()
        {
            if (IsSpawned)
            {
                RequestStopViewingRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestViewingRpc(RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if ((_activeViewerClientId.Value == NoViewerClientId ||
                 _activeViewerClientId.Value == clientId) &&
                ServerCanContinueViewing(clientId, NetworkRoundState.Current))
            {
                _activeViewerClientId.Value = clientId;
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestStopViewingRpc(RpcParams rpcParams = default)
        {
            if (_activeViewerClientId.Value ==
                rpcParams.Receive.SenderClientId)
            {
                _activeViewerClientId.Value = NoViewerClientId;
            }
        }

        private bool ServerCanContinueViewing(
            ulong clientId,
            NetworkRoundState roundState)
        {
            if (!_isUnlocked.Value ||
                roundState == null ||
                roundState.Phase != RoundPhase.Exploration ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            if (client.PlayerObject.TryGetComponent<
                    NetworkInfectionAuthority>(out var infection) &&
                infection.LifeState == PlayerLifeState.DeadGhost)
            {
                return false;
            }

            return ((Vector2)client.PlayerObject.transform.position -
                    _terminalWorldPosition).sqrMagnitude <=
                   _interactionRange * _interactionRange;
        }

        /// <summary>
        /// 스피커 작동을 로그에 남긴다. 서버에서만 호출한다.
        /// 잠금 여부와 무관하게 기록은 계속 쌓인다. 잠금은 열람만 막는다.
        /// </summary>
        public void ServerRecordSpeakerUse(string roomId)
        {
            if (!IsServer || string.IsNullOrEmpty(roomId))
            {
                return;
            }

            var roomIndex = IndexOfRoom(roomId);
            if (roomIndex < 0)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            var elapsedSeconds =
                roundState != null
                    ? roundState.ElapsedExplorationSeconds
                    : 0f;
            _logEntries.Add(
                new SecurityLogEntry(elapsedSeconds, (byte)roomIndex));
        }

        public bool TryGetLogEntry(
            int index,
            out float elapsedSeconds,
            out string roomDisplayName)
        {
            elapsedSeconds = 0f;
            roomDisplayName = string.Empty;
            if (index < 0 || index >= _logEntries.Count)
            {
                return false;
            }

            var entry = _logEntries[index];
            elapsedSeconds = entry.ElapsedSeconds;
            roomDisplayName = GetRoomDisplayName(entry.RoomIndex);
            return true;
        }

        private string GetRoomDisplayName(byte roomIndex)
        {
            if (_roomDisplayNames != null &&
                roomIndex < _roomDisplayNames.Length)
            {
                return _roomDisplayNames[roomIndex];
            }

            return _roomOrder != null && roomIndex < _roomOrder.Length
                ? _roomOrder[roomIndex]
                : $"방 {roomIndex}";
        }

        private int IndexOfRoom(string roomId)
        {
            if (_roomOrder == null)
            {
                return -1;
            }

            for (var index = 0; index < _roomOrder.Length; index++)
            {
                if (_roomOrder[index] == roomId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void HandleUnlockChanged(bool previous, bool current)
        {
            TerminalStateChanged?.Invoke();
        }

        private void HandleViewerChanged(ulong previous, ulong current)
        {
            TerminalStateChanged?.Invoke();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_activeViewerClientId.Value == clientId)
            {
                _activeViewerClientId.Value = NoViewerClientId;
            }
        }

        private void HandleLogChanged(
            NetworkListEvent<SecurityLogEntry> changeEvent)
        {
            TerminalStateChanged?.Invoke();
        }
    }
}
