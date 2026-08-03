using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 현장 단서의 서버 권위 상태다.
    /// 단서는 생존자가 봐야 하는 공개 증거이므로 전원에게 복제한다.
    /// 다만 "누가 남겼는지"는 절대 보내지 않는다(GDD §13.1: 사용자의 신원은 기록하지 않는다).
    ///
    /// 상태를 NetworkList로 복제하는 이유는 중간 참가·재접속 클라이언트도
    /// 이미 생성된 단서를 그대로 봐야 하기 때문이다. Rpc만 쓰면 그 시점에
    /// 접속하지 않은 클라이언트는 단서를 영영 못 본다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkClueAuthority : NetworkBehaviour
    {
        [SerializeField] private ClueMarker[] _markers =
            Array.Empty<ClueMarker>();

        private readonly NetworkList<ClueStateEntry> _clueStates =
            new(
                new List<ClueStateEntry>(),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly ClueRegistry _serverRegistry = new();

        public static NetworkClueAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        public event Action ClueStatesChanged;

        public int MarkerCount => _markers?.Length ?? 0;
        public int ActiveClueCount => _clueStates.Count;

        public void Configure(ClueMarker[] markers)
        {
            _markers = markers ?? Array.Empty<ClueMarker>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            CurrentChanged?.Invoke();
            _clueStates.OnListChanged += HandleClueListChanged;

            if (IsServer)
            {
                _serverRegistry.ResetForNewRound();
                _clueStates.Clear();
            }

            ApplyReplicatedStates();
        }

        public override void OnNetworkDespawn()
        {
            _clueStates.OnListChanged -= HandleClueListChanged;
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        /// <summary>
        /// 강화 성공 등으로 단서를 남긴다. 서버에서만 호출한다.
        /// 이미 활성인 단서는 다시 활성화하지 않는다.
        /// </summary>
        public bool ServerActivateClue(ClueKind kind, string roomId)
        {
            if (!IsServer)
            {
                return false;
            }

            var marker = FindInactiveMarker(kind, roomId);
            if (marker == null)
            {
                Debug.LogWarning(
                    $"[Clue] No inactive marker for {kind} in room '{roomId}'.",
                    this);
                return false;
            }

            if (!_serverRegistry.TryActivate(marker.ClueId, kind))
            {
                return false;
            }

            _clueStates.Add(
                new ClueStateEntry(
                    marker.ClueId,
                    (byte)kind,
                    (byte)ClueState.ActiveUninspected));
            Debug.Log(
                $"[Clue] {kind} left in room '{roomId}' (id {marker.ClueId}).",
                this);
            return true;
        }

        /// <summary>조사 표시를 남긴다. 단서를 사라지게 하지 않는다.</summary>
        /// <summary>
        /// 조사된 단서 수다. 결과 화면의 "발견한 단서와 놓친 것"에 쓴다(GDD §20).
        /// 라운드 중에는 공개하지 않는다.
        /// </summary>
        public int ServerCountInspectedClues()
        {
            return IsServer
                ? _serverRegistry.CountByState(ClueState.ActiveInspected)
                : 0;
        }

        public bool ServerMarkInspected(int clueId)
        {
            if (!IsServer)
            {
                return false;
            }

            var marker = FindMarkerById(clueId);
            if (marker == null ||
                !_serverRegistry.TryMarkInspected(clueId, marker.Kind))
            {
                return false;
            }

            for (var index = 0; index < _clueStates.Count; index++)
            {
                var entry = _clueStates[index];
                if (entry.ClueId != clueId)
                {
                    continue;
                }

                _clueStates[index] = new ClueStateEntry(
                    entry.ClueId,
                    entry.Kind,
                    (byte)ClueState.ActiveInspected);
                return true;
            }

            return false;
        }

        public ClueState GetReplicatedState(int clueId)
        {
            for (var index = 0; index < _clueStates.Count; index++)
            {
                var entry = _clueStates[index];
                if (entry.ClueId == clueId)
                {
                    return (ClueState)entry.State;
                }
            }

            return ClueState.Inactive;
        }

        private void HandleClueListChanged(
            NetworkListEvent<ClueStateEntry> changeEvent)
        {
            ApplyReplicatedStates();
            ClueStatesChanged?.Invoke();
        }

        private void ApplyReplicatedStates()
        {
            if (_markers == null)
            {
                return;
            }

            for (var index = 0; index < _markers.Length; index++)
            {
                var marker = _markers[index];
                if (marker == null)
                {
                    continue;
                }

                marker.ApplyState(GetReplicatedState(marker.ClueId));
            }
        }

        private ClueMarker FindInactiveMarker(ClueKind kind, string roomId)
        {
            if (_markers == null)
            {
                return null;
            }

            for (var index = 0; index < _markers.Length; index++)
            {
                var marker = _markers[index];
                if (marker != null &&
                    marker.Kind == kind &&
                    !_serverRegistry.IsActive(marker.ClueId) &&
                    (string.IsNullOrEmpty(roomId) ||
                     marker.RoomId == roomId))
                {
                    return marker;
                }
            }

            return null;
        }

        private ClueMarker FindMarkerById(int clueId)
        {
            if (_markers == null)
            {
                return null;
            }

            for (var index = 0; index < _markers.Length; index++)
            {
                if (_markers[index] != null &&
                    _markers[index].ClueId == clueId)
                {
                    return _markers[index];
                }
            }

            return null;
        }
    }
}
