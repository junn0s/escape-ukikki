namespace MonkeyLab.Network
{
    public readonly struct LobbyRosterResult
    {
        private LobbyRosterResult(bool succeeded, LobbyRosterFailureKind failureKind)
        {
            Succeeded = succeeded;
            FailureKind = failureKind;
        }

        public bool Succeeded { get; }
        public LobbyRosterFailureKind FailureKind { get; }

        public static LobbyRosterResult Success()
        {
            return new LobbyRosterResult(true, LobbyRosterFailureKind.None);
        }

        public static LobbyRosterResult Failure(LobbyRosterFailureKind failureKind)
        {
            return new LobbyRosterResult(false, failureKind);
        }
    }
}
