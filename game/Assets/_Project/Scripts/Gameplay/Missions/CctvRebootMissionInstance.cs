using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>왼쪽 신호 노드를 같은 채널의 오른쪽 포트에 연결하는 미션이다.</summary>
    public sealed class CctvRebootMissionInstance
    {
        private readonly int[] _targetChannelOrder;
        private readonly bool[] _connectedChannels;

        public CctvRebootMissionInstance(
            IReadOnlyList<int> targetChannelOrder)
        {
            if (targetChannelOrder == null)
            {
                throw new ArgumentNullException(nameof(targetChannelOrder));
            }

            var channelCount = targetChannelOrder.Count;
            if (channelCount < FuseMissionInstance.MinimumFuseCount ||
                channelCount > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelCount));
            }

            _targetChannelOrder = new int[channelCount];
            _connectedChannels = new bool[channelCount];
            var seen = new bool[channelCount + 1];
            for (var index = 0; index < channelCount; index++)
            {
                var channelId = targetChannelOrder[index];
                if (channelId < 1 || channelId > channelCount ||
                    seen[channelId])
                {
                    throw new ArgumentException(
                        "CCTV target order must contain every channel once.",
                        nameof(targetChannelOrder));
                }

                seen[channelId] = true;
                _targetChannelOrder[index] = channelId;
            }
        }

        public MissionState State { get; private set; } = MissionState.Assigned;
        public int ChannelCount => _targetChannelOrder.Length;
        public int CompletedChannelCount { get; private set; }

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
            }
        }

        public int GetTargetChannelAtSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _targetChannelOrder.Length
                ? _targetChannelOrder[slotIndex]
                : 0;
        }

        public bool IsChannelConnected(int channelId)
        {
            return channelId >= 1 && channelId <= _connectedChannels.Length &&
                   _connectedChannels[channelId - 1];
        }

        public FuseMissionInputResult SubmitConnection(
            int sourceChannelId,
            int targetChannelId)
        {
            if (State != MissionState.InProgress || sourceChannelId < 1 ||
                sourceChannelId > _connectedChannels.Length ||
                targetChannelId < 1 ||
                targetChannelId > _connectedChannels.Length ||
                _connectedChannels[sourceChannelId - 1])
            {
                return FuseMissionInputResult.Ignored;
            }

            // CCTV 배선 실수는 큰 소음을 내지 않고 연결만 되돌린다(UI/UX §9.4).
            if (sourceChannelId != targetChannelId)
            {
                return FuseMissionInputResult.Ignored;
            }

            _connectedChannels[sourceChannelId - 1] = true;
            CompletedChannelCount++;

            if (CompletedChannelCount < _connectedChannels.Length)
            {
                return FuseMissionInputResult.Accepted;
            }

            State = MissionState.Completed;
            return FuseMissionInputResult.Completed;
        }

        public void Cancel()
        {
            if (State == MissionState.InProgress)
            {
                State = MissionState.Cancelled;
            }
        }
    }
}
