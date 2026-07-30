using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonkeyLab.Network
{
    public struct LobbyPlayerNetworkState :
        INetworkSerializable,
        IEquatable<LobbyPlayerNetworkState>
    {
        public LobbyPlayerNetworkState(LobbyPlayerState state)
        {
            ClientId = state.ClientId;
            SlotIndex = (byte)state.SlotIndex;
            Nickname = state.Nickname;
            Color = state.Color;
            IsReady = state.IsReady;
            IsHost = state.IsHost;
        }

        public ulong ClientId;
        public byte SlotIndex;
        public FixedString64Bytes Nickname;
        public LobbyPlayerColor Color;
        public bool IsReady;
        public bool IsHost;

        public LobbyPlayerState ToPlayerState()
        {
            return new LobbyPlayerState(
                ClientId,
                SlotIndex,
                Nickname.ToString(),
                Color,
                IsReady,
                IsHost);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref Nickname);
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsHost);
        }

        public bool Equals(LobbyPlayerNetworkState other)
        {
            return ClientId == other.ClientId &&
                   SlotIndex == other.SlotIndex &&
                   Nickname.Equals(other.Nickname) &&
                   Color == other.Color &&
                   IsReady == other.IsReady &&
                   IsHost == other.IsHost;
        }

        public override bool Equals(object obj)
        {
            return obj is LobbyPlayerNetworkState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ClientId,
                SlotIndex,
                Nickname,
                Color,
                IsReady,
                IsHost);
        }
    }
}
