using System;
using System.Threading.Tasks;

namespace MonkeyLab.Network
{
    public sealed class GameSessionService
    {
        public const int RequiredPlayerCount = 6;

        private readonly IGameSessionGateway _gateway;
        private Task _operationTask;

        public GameSessionService(IGameSessionGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public event Action<GameSessionService, GameSessionState> StateChanged;

        public GameSessionState State { get; private set; } = GameSessionState.Idle;
        public GameSessionInfo CurrentSession { get; private set; }
        public string FailureMessage { get; private set; } = string.Empty;
        public bool IsBusy => _operationTask != null && !_operationTask.IsCompleted;

        public Task CreateSessionAsync()
        {
            if (State == GameSessionState.Connected)
            {
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _operationTask;
            }

            _operationTask = RunCreateSessionAsync();
            return _operationTask;
        }

        public Task JoinSessionAsync(string joinCode)
        {
            if (State == GameSessionState.Connected)
            {
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _operationTask;
            }

            var normalizedCode = NormalizeJoinCode(joinCode);
            if (string.IsNullOrEmpty(normalizedCode))
            {
                Fail("참가 코드를 입력해 주세요.");
                return Task.CompletedTask;
            }

            _operationTask = RunJoinSessionAsync(normalizedCode);
            return _operationTask;
        }

        public Task LeaveSessionAsync()
        {
            if (State != GameSessionState.Connected || CurrentSession == null)
            {
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _operationTask;
            }

            _operationTask = RunLeaveSessionAsync();
            return _operationTask;
        }

        private async Task RunCreateSessionAsync()
        {
            BeginOperation(GameSessionState.Creating);
            try
            {
                var request = new GameSessionCreateRequest(
                    RequiredPlayerCount,
                    isPrivate: true);
                CompleteConnection(await _gateway.CreateSessionAsync(request));
            }
            catch (GameSessionGatewayException exception)
            {
                Fail(CreateUserMessage(exception.FailureKind));
            }
            catch (Exception)
            {
                Fail(CreateUserMessage(GameSessionFailureKind.Unknown));
            }
        }

        private async Task RunJoinSessionAsync(string joinCode)
        {
            BeginOperation(GameSessionState.Joining);
            try
            {
                CompleteConnection(await _gateway.JoinSessionByCodeAsync(joinCode));
            }
            catch (GameSessionGatewayException exception)
            {
                Fail(CreateUserMessage(exception.FailureKind));
            }
            catch (Exception)
            {
                Fail(CreateUserMessage(GameSessionFailureKind.Unknown));
            }
        }

        private async Task RunLeaveSessionAsync()
        {
            BeginOperation(GameSessionState.Leaving);
            try
            {
                await _gateway.LeaveSessionAsync();
                CurrentSession = null;
                SetState(GameSessionState.Idle);
            }
            catch (GameSessionGatewayException exception)
            {
                Fail(CreateUserMessage(exception.FailureKind));
            }
            catch (Exception)
            {
                Fail(CreateUserMessage(GameSessionFailureKind.Unknown));
            }
        }

        private void BeginOperation(GameSessionState state)
        {
            FailureMessage = string.Empty;
            SetState(state);
        }

        private void CompleteConnection(GameSessionInfo session)
        {
            if (session == null ||
                string.IsNullOrWhiteSpace(session.SessionId) ||
                string.IsNullOrWhiteSpace(session.JoinCode))
            {
                Fail("세션 정보를 확인하지 못했습니다. 다시 시도해 주세요.");
                return;
            }

            CurrentSession = session;
            FailureMessage = string.Empty;
            SetState(GameSessionState.Connected);
        }

        private void Fail(string message)
        {
            CurrentSession = null;
            FailureMessage = message;
            SetState(GameSessionState.Failed);
        }

        private void SetState(GameSessionState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }

        private static string NormalizeJoinCode(string joinCode)
        {
            return string.IsNullOrWhiteSpace(joinCode)
                ? string.Empty
                : joinCode.Trim().ToUpperInvariant();
        }

        private static string CreateUserMessage(GameSessionFailureKind failureKind)
        {
            return failureKind switch
            {
                GameSessionFailureKind.InvalidJoinCode =>
                    "참가 코드가 올바르지 않거나 만료되었습니다.",
                GameSessionFailureKind.SessionUnavailable =>
                    "세션에 참가할 수 없습니다. 인원이 가득 찼거나 잠겼을 수 있습니다.",
                GameSessionFailureKind.NotAuthenticated =>
                    "온라인 인증이 만료되었습니다. 게임을 다시 시작해 주세요.",
                GameSessionFailureKind.RelayConnection =>
                    "Relay 연결에 실패했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.",
                GameSessionFailureKind.RateLimited =>
                    "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.",
                _ => "세션 연결에 실패했습니다. 잠시 후 다시 시도해 주세요."
            };
        }
    }
}
