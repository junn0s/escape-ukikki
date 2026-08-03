using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 결과 화면에서 공개하는 플레이어 한 명의 요약이다(GDD §20).
    /// 라운드 중에는 절대 채우지 않는다. 역할과 개인 미션 수는 라운드가 끝난
    /// 뒤에야 전원에게 공개되는 정보이기 때문이다(GDD §16.4).
    /// </summary>
    public struct RoundSummaryEntry :
        INetworkSerializable,
        IEquatable<RoundSummaryEntry>
    {
        public ulong ClientId;
        public byte SlotIndex;
        public LobbyPlayerColor Color;
        public PlayerRole Role;
        public PlayerLifeState LifeState;
        public byte CompletedMissionCount;
        public byte AssignedMissionCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref LifeState);
            serializer.SerializeValue(ref CompletedMissionCount);
            serializer.SerializeValue(ref AssignedMissionCount);
        }

        public bool Equals(RoundSummaryEntry other)
        {
            return ClientId == other.ClientId &&
                   SlotIndex == other.SlotIndex &&
                   Color == other.Color &&
                   Role == other.Role &&
                   LifeState == other.LifeState &&
                   CompletedMissionCount == other.CompletedMissionCount &&
                   AssignedMissionCount == other.AssignedMissionCount;
        }

        public override bool Equals(object obj)
        {
            return obj is RoundSummaryEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ClientId,
                SlotIndex,
                Color,
                Role,
                LifeState,
                CompletedMissionCount,
                AssignedMissionCount);
        }
    }
}
