using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MonkeyLab.Network;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class OnlineServicesStartupTests
    {
        [Test]
        public async Task InitializesServicesBeforeAnonymousSignIn()
        {
            var gateway = new FakeOnlineServicesGateway();
            var startup = new OnlineServicesStartup(gateway);

            await startup.InitializeAndSignInAsync();

            CollectionAssert.AreEqual(
                new[] { "InitializeServices", "SignInAnonymously" },
                gateway.Calls);
            Assert.That(startup.State, Is.EqualTo(OnlineServicesState.Ready));
            Assert.That(startup.PlayerId, Is.EqualTo(FakeOnlineServicesGateway.ValidPlayerId));
        }

        [Test]
        public async Task ExistingAuthorizedSessionSkipsDuplicateSdkCalls()
        {
            var gateway = new FakeOnlineServicesGateway
            {
                AreServicesInitialized = true,
                IsSignedIn = true
            };
            var startup = new OnlineServicesStartup(gateway);

            await startup.InitializeAndSignInAsync();

            Assert.That(gateway.Calls, Is.Empty);
            Assert.That(startup.State, Is.EqualTo(OnlineServicesState.Ready));
        }

        [Test]
        public void InitializationFailureStopsBootstrapWithUserMessage()
        {
            var gateway = new FakeOnlineServicesGateway
            {
                InitializationFailure = new InvalidOperationException("service unavailable")
            };
            var startup = new OnlineServicesStartup(gateway);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await startup.InitializeAndSignInAsync());
            Assert.That(startup.State, Is.EqualTo(OnlineServicesState.Failed));
            Assert.That(startup.FailureMessage, Is.Not.Empty);
            Assert.That(gateway.Calls, Is.EqualTo(new[] { "InitializeServices" }));
        }

        [Test]
        public void UnlinkedCloudProjectFailsBeforeCallingSdk()
        {
            var gateway = new FakeOnlineServicesGateway { IsProjectLinked = false };
            var startup = new OnlineServicesStartup(gateway);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await startup.InitializeAndSignInAsync());
            Assert.That(startup.State, Is.EqualTo(OnlineServicesState.Failed));
            StringAssert.Contains("Unity Cloud", startup.FailureMessage);
            Assert.That(gateway.Calls, Is.Empty);
        }

        private sealed class FakeOnlineServicesGateway : IOnlineServicesGateway
        {
            public const string ValidPlayerId = "test-player-id";

            public List<string> Calls { get; } = new();
            public bool IsProjectLinked { get; set; } = true;
            public bool AreServicesInitialized { get; set; }
            public bool IsSignedIn { get; set; }
            public string PlayerId => IsSignedIn ? ValidPlayerId : string.Empty;
            public Exception InitializationFailure { get; set; }

            public Task InitializeServicesAsync()
            {
                Calls.Add("InitializeServices");
                if (InitializationFailure != null)
                {
                    return Task.FromException(InitializationFailure);
                }

                AreServicesInitialized = true;
                return Task.CompletedTask;
            }

            public Task SignInAnonymouslyAsync()
            {
                Calls.Add("SignInAnonymously");
                IsSignedIn = true;
                return Task.CompletedTask;
            }
        }
    }
}
