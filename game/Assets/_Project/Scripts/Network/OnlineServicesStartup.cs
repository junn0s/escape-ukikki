using System;
using System.Threading.Tasks;
using Unity.Services.Core;

namespace MonkeyLab.Network
{
    public sealed class OnlineServicesStartup
    {
        private readonly IOnlineServicesGateway _gateway;
        private Task _initializationTask;

        public OnlineServicesStartup(IOnlineServicesGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public OnlineServicesState State { get; private set; } = OnlineServicesState.NotStarted;
        public string PlayerId { get; private set; } = string.Empty;
        public string FailureMessage { get; private set; } = string.Empty;

        public Task InitializeAndSignInAsync()
        {
            if (State == OnlineServicesState.Ready)
            {
                return Task.CompletedTask;
            }

            if (_initializationTask == null || _initializationTask.IsCompleted)
            {
                _initializationTask = RunInitializationAsync();
            }

            return _initializationTask;
        }

        private async Task RunInitializationAsync()
        {
            FailureMessage = string.Empty;
            try
            {
                if (!_gateway.IsProjectLinked)
                {
                    throw new InvalidOperationException("Unity Cloud project is not linked.");
                }

                State = OnlineServicesState.InitializingServices;
                if (!_gateway.AreServicesInitialized)
                {
                    await _gateway.InitializeServicesAsync();
                }

                State = OnlineServicesState.SigningIn;
                if (!_gateway.IsSignedIn)
                {
                    await _gateway.SignInAnonymouslyAsync();
                }

                if (string.IsNullOrWhiteSpace(_gateway.PlayerId))
                {
                    throw new InvalidOperationException(
                        "Anonymous authentication completed without a player ID.");
                }

                PlayerId = _gateway.PlayerId;
                State = OnlineServicesState.Ready;
            }
            catch (Exception exception)
            {
                PlayerId = string.Empty;
                FailureMessage = CreateUserMessage(exception);
                State = OnlineServicesState.Failed;
                throw;
            }
        }

        private string CreateUserMessage(Exception exception)
        {
            if (!_gateway.IsProjectLinked)
            {
                return "Unity Cloud 프로젝트 연결이 필요합니다. Project Settings > Services에서 연결해 주세요.";
            }

            if (exception is RequestFailedException)
            {
                return "온라인 서비스에 연결하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.";
            }

            return "온라인 서비스 초기화에 실패했습니다. 잠시 후 다시 시도해 주세요.";
        }
    }
}
