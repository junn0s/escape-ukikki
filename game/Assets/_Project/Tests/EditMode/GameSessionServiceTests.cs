using System.Threading.Tasks;
using MonkeyLab.Network;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class GameSessionServiceTests
    {
        [Test]
        public async Task CreateUsesSixPlayerPrivateSessionAndExposesJoinCode()
        {
            var gateway = new FakeGameSessionGateway();
            var service = new GameSessionService(gateway);

            await service.CreateSessionAsync();

            Assert.That(gateway.CreateCalls, Is.EqualTo(1));
            Assert.That(gateway.LastCreateRequest.MaxPlayers, Is.EqualTo(6));
            Assert.That(gateway.LastCreateRequest.IsPrivate, Is.True);
            Assert.That(service.State, Is.EqualTo(GameSessionState.Connected));
            Assert.That(service.CurrentSession.JoinCode, Is.EqualTo("ABC123"));
            Assert.That(service.CurrentSession.IsHost, Is.True);
        }

        [Test]
        public async Task JoinNormalizesCodeBeforeCallingGateway()
        {
            var gateway = new FakeGameSessionGateway();
            var service = new GameSessionService(gateway);

            await service.JoinSessionAsync("  ab12cd  ");

            Assert.That(gateway.LastJoinCode, Is.EqualTo("AB12CD"));
            Assert.That(service.State, Is.EqualTo(GameSessionState.Connected));
            Assert.That(service.CurrentSession.IsHost, Is.False);
        }

        [Test]
        public async Task EmptyJoinCodeFailsWithoutGatewayCall()
        {
            var gateway = new FakeGameSessionGateway();
            var service = new GameSessionService(gateway);

            await service.JoinSessionAsync("   ");

            Assert.That(gateway.JoinCalls, Is.Zero);
            Assert.That(service.State, Is.EqualTo(GameSessionState.Failed));
            StringAssert.Contains("참가 코드", service.FailureMessage);
        }

        [Test]
        public async Task RepeatedCreateWhileBusySharesSingleOperation()
        {
            var gateway = new FakeGameSessionGateway
            {
                CreateCompletion = new TaskCompletionSource<GameSessionInfo>()
            };
            var service = new GameSessionService(gateway);

            var first = service.CreateSessionAsync();
            var second = service.CreateSessionAsync();

            Assert.That(second, Is.SameAs(first));
            Assert.That(gateway.CreateCalls, Is.EqualTo(1));

            gateway.CreateCompletion.SetResult(FakeGameSessionGateway.HostSession);
            await first;
            Assert.That(service.State, Is.EqualTo(GameSessionState.Connected));
        }

        [Test]
        public async Task InvalidJoinCodeProvidesActionableMessage()
        {
            var gateway = new FakeGameSessionGateway
            {
                JoinFailure = new GameSessionGatewayException(
                    GameSessionFailureKind.InvalidJoinCode,
                    "not found")
            };
            var service = new GameSessionService(gateway);

            await service.JoinSessionAsync("ABC123");

            Assert.That(service.State, Is.EqualTo(GameSessionState.Failed));
            StringAssert.Contains("만료", service.FailureMessage);
        }

        [Test]
        public async Task LeaveClearsConnectedSession()
        {
            var gateway = new FakeGameSessionGateway();
            var service = new GameSessionService(gateway);
            await service.CreateSessionAsync();

            await service.LeaveSessionAsync();

            Assert.That(gateway.LeaveCalls, Is.EqualTo(1));
            Assert.That(service.State, Is.EqualTo(GameSessionState.Idle));
            Assert.That(service.CurrentSession, Is.Null);
        }

        private sealed class FakeGameSessionGateway : IGameSessionGateway
        {
            public static readonly GameSessionInfo HostSession =
                new("session-host", "ABC123", true, 1, 6);

            private static readonly GameSessionInfo ClientSession =
                new("session-client", "ABC123", false, 2, 6);

            public int CreateCalls { get; private set; }
            public int JoinCalls { get; private set; }
            public int LeaveCalls { get; private set; }
            public GameSessionCreateRequest LastCreateRequest { get; private set; }
            public string LastJoinCode { get; private set; }
            public TaskCompletionSource<GameSessionInfo> CreateCompletion { get; set; }
            public GameSessionGatewayException JoinFailure { get; set; }

            public Task<GameSessionInfo> CreateSessionAsync(GameSessionCreateRequest request)
            {
                CreateCalls++;
                LastCreateRequest = request;
                return CreateCompletion?.Task ?? Task.FromResult(HostSession);
            }

            public Task<GameSessionInfo> JoinSessionByCodeAsync(string joinCode)
            {
                JoinCalls++;
                LastJoinCode = joinCode;
                return JoinFailure == null
                    ? Task.FromResult(ClientSession)
                    : Task.FromException<GameSessionInfo>(JoinFailure);
            }

            public Task LeaveSessionAsync()
            {
                LeaveCalls++;
                return Task.CompletedTask;
            }
        }
    }
}
