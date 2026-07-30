namespace MonkeyLab.Network
{
    public enum LobbyRosterFailureKind : byte
    {
        None = 0,
        LobbyFull = 1,
        PlayerNotFound = 2,
        InvalidColor = 3,
        ColorAlreadyTaken = 4,
        NotHost = 5,
        NotEnoughPlayers = 6,
        PlayersNotReady = 7,
        StartAlreadyInProgress = 8,
        PlayerObjectUnavailable = 9,
        SceneTransitionFailed = 10
    }
}
