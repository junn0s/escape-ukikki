using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace MonkeyLab.Network
{
    internal sealed class UnityGameSessionGateway : IGameSessionGateway
    {
        private const string SessionName = "Escape Ukikki";
        private const string SessionType = "escape-ukikki";

        private ISession _session;

        public async Task<GameSessionInfo> CreateSessionAsync(GameSessionCreateRequest request)
        {
            var options = new SessionOptions
            {
                Name = SessionName,
                Type = SessionType,
                MaxPlayers = request.MaxPlayers,
                IsPrivate = request.IsPrivate,
                IsLocked = false
            }.WithRelayNetwork();

            try
            {
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                return CreateInfo(_session);
            }
            catch (SessionException exception)
            {
                throw Translate(exception);
            }
        }

        public async Task<GameSessionInfo> JoinSessionByCodeAsync(string joinCode)
        {
            try
            {
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                return CreateInfo(_session);
            }
            catch (SessionException exception)
            {
                throw Translate(exception);
            }
        }

        public async Task<GameSessionInfo> ReconnectSessionAsync()
        {
            if (_session == null || _session.IsHost ||
                string.IsNullOrWhiteSpace(_session.Code))
            {
                throw new GameSessionGatewayException(
                    GameSessionFailureKind.SessionUnavailable,
                    "There is no client session to reconnect.");
            }

            var joinCode = _session.Code;
            try
            {
                // 외부 전송 끊김 뒤 MPS의 기존 NGO 네트워크 핸들러를 먼저
                // 정리해야 새 Relay 할당으로 NetworkManager를 다시 시작할 수 있다.
                await _session.LeaveAsync();
                _session = await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(joinCode);
                return CreateInfo(_session);
            }
            catch (SessionException exception)
            {
                throw Translate(exception);
            }
        }

        public async Task LeaveSessionAsync()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                if (_session.IsHost)
                {
                    await _session.AsHost().DeleteAsync();
                }
                else
                {
                    await _session.LeaveAsync();
                }

                _session = null;
            }
            catch (SessionException exception)
            {
                throw Translate(exception);
            }
        }

        private static GameSessionInfo CreateInfo(ISession session)
        {
            return new GameSessionInfo(
                session.Id,
                session.Code,
                session.IsHost,
                session.PlayerCount,
                session.MaxPlayers);
        }

        private static GameSessionGatewayException Translate(SessionException exception)
        {
            var failureKind = exception.Error switch
            {
                SessionError.SessionNotFound or
                SessionError.SessionDeleted or
                SessionError.InvalidSessionIdentifier or
                SessionError.InvalidParameter =>
                    GameSessionFailureKind.InvalidJoinCode,
                SessionError.InvalidOperation or
                SessionError.Forbidden =>
                    GameSessionFailureKind.SessionUnavailable,
                SessionError.NotAuthorized =>
                    GameSessionFailureKind.NotAuthenticated,
                SessionError.NetworkManagerNotInitialized or
                SessionError.NetworkManagerStartFailed or
                SessionError.NetworkSetupFailed or
                SessionError.InvalidNetworkConfig or
                SessionError.TransportComponentMissing or
                SessionError.TransportInvalid or
                SessionError.QoSMeasurementFailed =>
                    GameSessionFailureKind.RelayConnection,
                SessionError.RateLimitExceeded =>
                    GameSessionFailureKind.RateLimited,
                _ => GameSessionFailureKind.Unknown
            };

            return new GameSessionGatewayException(
                failureKind,
                exception.Message,
                exception);
        }
    }
}
