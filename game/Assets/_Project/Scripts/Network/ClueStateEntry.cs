using System;
using Unity.Netcode;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 복제되는 단서 한 개의 상태다.
    /// 누가 남겼는지는 담지 않는다(GDD §13.1).
    /// </summary>
    public struct ClueStateEntry :
        INetworkSerializable,
        IEquatable<ClueStateEntry>
    {
        public int ClueId;
        public byte Kind;
        public byte State;

        public ClueStateEntry(int clueId, byte kind, byte state)
        {
            ClueId = clueId;
            Kind = kind;
            State = state;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClueId);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref State);
        }

        public bool Equals(ClueStateEntry other)
        {
            return ClueId == other.ClueId &&
                   Kind == other.Kind &&
                   State == other.State;
        }

        public override bool Equals(object obj)
        {
            return obj is ClueStateEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClueId, Kind, State);
        }
    }
}
