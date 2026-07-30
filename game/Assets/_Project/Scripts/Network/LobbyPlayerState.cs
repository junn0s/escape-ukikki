using System;

namespace MonkeyLab.Network
{
    public readonly struct LobbyPlayerState : IEquatable<LobbyPlayerState>
    {
        public LobbyPlayerState(
            ulong clientId,
            int slotIndex,
            string nickname,
            LobbyPlayerColor color,
            bool isReady,
            bool isHost)
        {
            ClientId = clientId;
            SlotIndex = slotIndex;
            Nickname = nickname ?? string.Empty;
            Color = color;
            IsReady = isReady;
            IsHost = isHost;
        }

        public ulong ClientId { get; }
        public int SlotIndex { get; }
        public string Nickname { get; }
        public LobbyPlayerColor Color { get; }
        public bool IsReady { get; }
        public bool IsHost { get; }

        public LobbyPlayerState WithColor(LobbyPlayerColor color)
        {
            return new LobbyPlayerState(
                ClientId,
                SlotIndex,
                Nickname,
                color,
                IsReady,
                IsHost);
        }

        public LobbyPlayerState WithReady(bool isReady)
        {
            return new LobbyPlayerState(
                ClientId,
                SlotIndex,
                Nickname,
                Color,
                isReady,
                IsHost);
        }

        public bool Equals(LobbyPlayerState other)
        {
            return ClientId == other.ClientId &&
                   SlotIndex == other.SlotIndex &&
                   Nickname == other.Nickname &&
                   Color == other.Color &&
                   IsReady == other.IsReady &&
                   IsHost == other.IsHost;
        }

        public override bool Equals(object obj)
        {
            return obj is LobbyPlayerState other && Equals(other);
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
