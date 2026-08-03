using MonkeyLab.Gameplay.Infection;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(InfectionService))]
    public sealed class NetworkInfectionAuthority : NetworkBehaviour
    {
        [SerializeField] private InfectionService _infectionService;

        private readonly NetworkVariable<PlayerLifeState> _lifeState = new(
            PlayerLifeState.AliveHealthy,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _durationAtBiteSeconds = new(
            0f,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _remainingSeconds = new(
            0f,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _toxicityTierAtBite = new(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        private float _nextServerPublishTime;

        public InfectionService InfectionService => _infectionService;
        public PlayerLifeState LifeState => _lifeState.Value;
        public float RemainingSeconds => _remainingSeconds.Value;

        public void Configure(InfectionService infectionService)
        {
            _infectionService = infectionService;
        }

        public override void OnNetworkSpawn()
        {
            if (_infectionService == null)
            {
                Debug.LogError(
                    "[Infection] Network authority reference is missing.",
                    this);
                enabled = false;
                return;
            }

            _lifeState.OnValueChanged += HandleLifeStateChanged;
            _durationAtBiteSeconds.OnValueChanged += HandleFloatChanged;
            _remainingSeconds.OnValueChanged += HandleFloatChanged;
            _toxicityTierAtBite.OnValueChanged += HandleTierChanged;
            _infectionService.SetExternallyDriven(!IsServer);
            if (IsServer)
            {
                _infectionService.StateChanged +=
                    HandleServerInfectionStateChanged;
                PublishServerState();
            }
            else
            {
                ApplyReplicatedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            _lifeState.OnValueChanged -= HandleLifeStateChanged;
            _durationAtBiteSeconds.OnValueChanged -= HandleFloatChanged;
            _remainingSeconds.OnValueChanged -= HandleFloatChanged;
            _toxicityTierAtBite.OnValueChanged -= HandleTierChanged;
            if (_infectionService != null)
            {
                _infectionService.StateChanged -=
                    HandleServerInfectionStateChanged;
                _infectionService.SetExternallyDriven(false);
            }
        }

        private void Update()
        {
            if (!IsServer || _infectionService == null)
            {
                return;
            }

            // 회의 중에는 감염 타이머가 정지한다(SDD §4 상태표).
            // 남은 값은 보존하고 회의가 끝나면 그대로 이어간다.
            var roundState = NetworkRoundState.Current;
            _infectionService.SetPaused(
                roundState != null && roundState.IsMeetingActive);

            if (Time.unscaledTime < _nextServerPublishTime)
            {
                return;
            }

            _nextServerPublishTime = Time.unscaledTime + 0.1f;
            PublishServerState();
        }

        /// <summary>다음 라운드를 위해 생명 상태를 초기화한다.</summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer || _infectionService == null)
            {
                return;
            }

            _infectionService.ResetForNewRound();
            PublishServerState();
        }

        /// <summary>회의 퇴출로 유령 상태를 확정한다(GDD §16.4).</summary>
        public bool ServerForceGhost()
        {
            if (!IsServer || _infectionService == null ||
                !_infectionService.TryExile())
            {
                return false;
            }

            PublishServerState();
            return true;
        }

        private void PublishServerState()
        {
            if (!IsServer || _infectionService == null)
            {
                return;
            }

            _lifeState.Value = _infectionService.State;
            _durationAtBiteSeconds.Value =
                _infectionService.DurationAtBiteSeconds;
            _remainingSeconds.Value =
                _infectionService.RemainingSeconds;
            _toxicityTierAtBite.Value =
                _infectionService.ToxicityTierAtBite;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _infectionService == null)
            {
                return;
            }

            _infectionService.ApplyAuthoritativeSnapshot(
                _lifeState.Value,
                IsOwner ? _durationAtBiteSeconds.Value : 0f,
                IsOwner ? _remainingSeconds.Value : 0f,
                IsOwner ? _toxicityTierAtBite.Value : 0);
        }

        private void HandleServerInfectionStateChanged(
            InfectionService service,
            PlayerLifeState state)
        {
            PublishServerState();
        }

        private void HandleLifeStateChanged(
            PlayerLifeState previousValue,
            PlayerLifeState currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleFloatChanged(
            float previousValue,
            float currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleTierChanged(int previousValue, int currentValue)
        {
            ApplyReplicatedState();
        }
    }
}
