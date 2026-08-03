using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerMissionJournal : NetworkBehaviour
    {
        private readonly NetworkList<ulong> _assignedMissionIds = new(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<ulong> _completedMissionIds = new(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isPerformingMission = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action Changed;

        public int AssignedCount => _assignedMissionIds.Count;
        public int CompletedCount => _completedMissionIds.Count;
        public bool IsPerformingMission => _isPerformingMission.Value;
        public NetworkVariableReadPermission ReadPermission =>
            _assignedMissionIds.ReadPerm;
        public NetworkVariableWritePermission WritePermission =>
            _assignedMissionIds.WritePerm;
        public NetworkVariableReadPermission ActivityReadPermission =>
            _isPerformingMission.ReadPerm;

        public override void OnNetworkSpawn()
        {
            _assignedMissionIds.OnListChanged += HandleListChanged;
            _completedMissionIds.OnListChanged += HandleListChanged;
            _isPerformingMission.OnValueChanged +=
                HandleMissionActivityChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _assignedMissionIds.OnListChanged -= HandleListChanged;
            _completedMissionIds.OnListChanged -= HandleListChanged;
            _isPerformingMission.OnValueChanged -=
                HandleMissionActivityChanged;
        }

        public bool IsAssigned(ulong missionId)
        {
            return _assignedMissionIds.Contains(missionId);
        }

        /// <summary>
        /// 미션 목록 화면이 배정된 미션을 순서대로 훑을 때 쓴다(GDD §7.2).
        /// 소유자와 서버만 실제 값을 읽는다.
        /// </summary>
        public ulong GetAssignedMissionId(int index)
        {
            return index >= 0 && index < _assignedMissionIds.Count
                ? _assignedMissionIds[index]
                : 0UL;
        }

        public bool IsCompleted(ulong missionId)
        {
            return _completedMissionIds.Contains(missionId);
        }

        public bool ServerAssignMissions(
            IReadOnlyList<ulong> missionIds)
        {
            if (!IsServer || missionIds == null ||
                missionIds.Count == 0)
            {
                return false;
            }

            _assignedMissionIds.Clear();
            _completedMissionIds.Clear();
            for (var index = 0; index < missionIds.Count; index++)
            {
                var missionId = missionIds[index];
                if (!_assignedMissionIds.Contains(missionId))
                {
                    _assignedMissionIds.Add(missionId);
                }
            }

            return _assignedMissionIds.Count > 0;
        }

        public bool ServerMarkCompleted(ulong missionId)
        {
            if (!IsServer || !IsAssigned(missionId) ||
                IsCompleted(missionId))
            {
                return false;
            }

            _completedMissionIds.Add(missionId);
            return true;
        }

        /// <summary>
        /// 재접속 스냅샷에 개인 미션을 복사한다. 이 값은 서버 메모리에만 남고
        /// 다른 클라이언트에 공개하지 않는다.
        /// </summary>
        public bool ServerCreateReconnectSnapshot(
            out ulong[] assignedMissionIds,
            out ulong[] completedMissionIds)
        {
            assignedMissionIds = Array.Empty<ulong>();
            completedMissionIds = Array.Empty<ulong>();
            if (!IsServer)
            {
                return false;
            }

            assignedMissionIds = new ulong[_assignedMissionIds.Count];
            for (var index = 0; index < _assignedMissionIds.Count; index++)
            {
                assignedMissionIds[index] = _assignedMissionIds[index];
            }

            completedMissionIds = new ulong[_completedMissionIds.Count];
            for (var index = 0; index < _completedMissionIds.Count; index++)
            {
                completedMissionIds[index] = _completedMissionIds[index];
            }

            return true;
        }

        /// <summary>30초 내 재접속한 소유자의 개인 미션 상태를 복원한다.</summary>
        public bool ServerRestoreReconnectSnapshot(
            IReadOnlyList<ulong> assignedMissionIds,
            IReadOnlyList<ulong> completedMissionIds)
        {
            if (!IsServer || assignedMissionIds == null ||
                assignedMissionIds.Count == 0 ||
                completedMissionIds == null ||
                !ServerAssignMissions(assignedMissionIds))
            {
                return false;
            }

            for (var index = 0; index < completedMissionIds.Count; index++)
            {
                var missionId = completedMissionIds[index];
                if (IsAssigned(missionId) && !IsCompleted(missionId))
                {
                    _completedMissionIds.Add(missionId);
                }
            }

            _isPerformingMission.Value = false;
            return true;
        }

        /// <summary>
        /// 다음 라운드를 위해 배정·완료 목록과 수행 중 표시를 모두 비운다.
        /// 미션 재배정이 목록을 덮어쓰긴 하지만, 수행 중 표시가 남으면
        /// 새 판 시작 순간 다른 플레이어에게 미션 연출이 보인다.
        /// </summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer)
            {
                return;
            }

            _assignedMissionIds.Clear();
            _completedMissionIds.Clear();
            _isPerformingMission.Value = false;
        }

        public bool ServerSetMissionActivity(bool isPerforming)
        {
            if (!IsServer)
            {
                return false;
            }

            _isPerformingMission.Value = isPerforming;
            return true;
        }

        private void HandleListChanged(
            NetworkListEvent<ulong> changeEvent)
        {
            Changed?.Invoke();
        }

        private void HandleMissionActivityChanged(
            bool previousValue,
            bool currentValue)
        {
            Changed?.Invoke();
        }
    }
}
