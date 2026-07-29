namespace MonkeyLab.Network
{
    public sealed class GameSessionInfo
    {
        public GameSessionInfo(
            string sessionId,
            string joinCode,
            bool isHost,
            int playerCount,
            int maxPlayers)
        {
            SessionId = sessionId;
            JoinCode = joinCode;
            IsHost = isHost;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
        }

        public string SessionId { get; }
        public string JoinCode { get; }
        public bool IsHost { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }
    }
}
