namespace MonkeyLab.Network
{
    public enum GameSessionState
    {
        Idle,
        Creating,
        Joining,
        Reconnecting,
        Connected,
        Leaving,
        Failed
    }
}
