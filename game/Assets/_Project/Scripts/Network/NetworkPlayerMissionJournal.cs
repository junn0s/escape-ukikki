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
