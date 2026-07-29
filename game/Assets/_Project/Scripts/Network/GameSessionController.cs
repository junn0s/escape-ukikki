using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MonkeyLab.Network
{
    public sealed class GameSessionController : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;

        private GameSessionService _service;

        public GameSessionState State => EnsureService().State;
        public GameSessionInfo CurrentSession => EnsureService().CurrentSession;
        public string FailureMessage => EnsureService().FailureMessage;
        public bool IsBusy => EnsureService().IsBusy;
        public NetworkManager NetworkManager => _networkManager;
        public UnityTransport Transport => _transport;

        public void Configure(NetworkManager networkManager, UnityTransport transport)
        {
            _networkManager = networkManager;
            _transport = transport;
        }

        public Task CreateSessionAsync()
        {
            return EnsureService().CreateSessionAsync();
        }

        public Task JoinSessionAsync(string joinCode)
        {
            return EnsureService().JoinSessionAsync(joinCode);
        }

        public Task LeaveSessionAsync()
        {
            return EnsureService().LeaveSessionAsync();
        }

        private void Awake()
        {
            if (_networkManager == null || _transport == null)
            {
                Debug.LogError(
                    "[Session] NetworkManager and UnityTransport are required.",
                    this);
                enabled = false;
                return;
            }

            if (_networkManager.NetworkConfig.NetworkTransport != _transport)
            {
                Debug.LogError(
                    "[Session] NetworkManager must use the configured UnityTransport.",
                    this);
                enabled = false;
                return;
            }

            EnsureService();
            DontDestroyOnLoad(gameObject);
        }

        private GameSessionService EnsureService()
        {
            return _service ??= new GameSessionService(new UnityGameSessionGateway());
        }
    }
}
