namespace MonkeyLab.Network
{
    public sealed class GameSessionCreateRequest
    {
        public GameSessionCreateRequest(int maxPlayers, bool isPrivate)
        {
            MaxPlayers = maxPlayers;
            IsPrivate = isPrivate;
        }

        public int MaxPlayers { get; }
        public bool IsPrivate { get; }
    }
}
