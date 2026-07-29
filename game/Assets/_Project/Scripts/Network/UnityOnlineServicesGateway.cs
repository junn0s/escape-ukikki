using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace MonkeyLab.Network
{
    internal sealed class UnityOnlineServicesGateway : IOnlineServicesGateway
    {
        public bool IsProjectLinked => !string.IsNullOrWhiteSpace(Application.cloudProjectId);

        public bool AreServicesInitialized =>
            UnityServices.State == ServicesInitializationState.Initialized;

        public bool IsSignedIn =>
            AreServicesInitialized && AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => IsSignedIn
            ? AuthenticationService.Instance.PlayerId
            : string.Empty;

        public Task InitializeServicesAsync()
        {
            return AreServicesInitialized
                ? Task.CompletedTask
                : UnityServices.InitializeAsync();
        }

        public Task SignInAnonymouslyAsync()
        {
            return IsSignedIn
                ? Task.CompletedTask
                : AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
