using System;
using Unity.Netcode;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 보안실 로그 한 줄이다. 작동 시각과 방만 기록하고
    /// 사용자의 신원은 절대 기록하지 않는다(GDD §13.1).
    /// </summary>
    public struct SecurityLogEntry :
        INetworkSerializable,
        IEquatable<SecurityLogEntry>
    {
        /// <summary>라운드 시작 이후 경과한 탐색 시간(초)이다.</summary>
        public float ElapsedSeconds;

        /// <summary>방 인덱스다. RoomOrder 기준이다.</summary>
        public byte RoomIndex;

        public SecurityLogEntry(float elapsedSeconds, byte roomIndex)
        {
            ElapsedSeconds = elapsedSeconds;
            RoomIndex = roomIndex;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ElapsedSeconds);
            serializer.SerializeValue(ref RoomIndex);
        }

        public bool Equals(SecurityLogEntry other)
        {
            return ElapsedSeconds.Equals(other.ElapsedSeconds) &&
                   RoomIndex == other.RoomIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is SecurityLogEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ElapsedSeconds, RoomIndex);
        }
    }
}
