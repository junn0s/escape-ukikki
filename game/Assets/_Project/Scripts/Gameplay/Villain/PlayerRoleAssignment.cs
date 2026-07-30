using System;

namespace MonkeyLab.Gameplay.Villain
{
    public readonly struct PlayerRoleAssignment :
        IEquatable<PlayerRoleAssignment>
    {
        public PlayerRoleAssignment(ulong clientId, PlayerRole role)
        {
            ClientId = clientId;
            Role = role;
        }

        public ulong ClientId { get; }
        public PlayerRole Role { get; }

        public bool Equals(PlayerRoleAssignment other)
        {
            return ClientId == other.ClientId && Role == other.Role;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerRoleAssignment other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, Role);
        }
    }
}
