using System.Threading.Tasks;
using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Network
{
    public sealed class UnityServicesInitializer : MonoBehaviour, IBootstrapTask
    {
        private OnlineServicesStartup _startup;

        public bool IsReady => EnsureStartup().State == OnlineServicesState.Ready;
        public string FailureMessage => EnsureStartup().FailureMessage;
        public OnlineServicesState State => EnsureStartup().State;
        public string PlayerId => EnsureStartup().PlayerId;

        public Task InitializeAsync()
        {
            return EnsureStartup().InitializeAndSignInAsync();
        }

        private void Awake()
        {
            EnsureStartup();
        }

        private OnlineServicesStartup EnsureStartup()
        {
            return _startup ??= new OnlineServicesStartup(new UnityOnlineServicesGateway());
        }
    }
}
