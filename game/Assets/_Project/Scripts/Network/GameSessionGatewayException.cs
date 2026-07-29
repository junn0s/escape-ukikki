using System;

namespace MonkeyLab.Network
{
    public sealed class GameSessionGatewayException : Exception
    {
        public GameSessionGatewayException(
            GameSessionFailureKind failureKind,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            FailureKind = failureKind;
        }

        public GameSessionFailureKind FailureKind { get; }
    }
}
