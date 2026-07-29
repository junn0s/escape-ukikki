using System.Threading.Tasks;

namespace MonkeyLab.Network
{
    public interface IOnlineServicesGateway
    {
        bool IsProjectLinked { get; }
        bool AreServicesInitialized { get; }
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task InitializeServicesAsync();
        Task SignInAnonymouslyAsync();
    }
}
