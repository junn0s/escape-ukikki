namespace MonkeyLab.Network
{
    public enum GameSessionFailureKind
    {
        Unknown,
        InvalidJoinCode,
        SessionUnavailable,
        NotAuthenticated,
        RelayConnection,
        RateLimited
    }
}
