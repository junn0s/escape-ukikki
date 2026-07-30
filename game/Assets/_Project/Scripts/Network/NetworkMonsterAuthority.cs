using MonkeyLab.Gameplay.Monsters;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(MonsterBrain))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class NetworkMonsterAuthority : NetworkBehaviour
    {
        [SerializeField] private MonsterBrain _brain;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private NetworkTransform _networkTransform;

        private readonly NetworkVariable<MonsterState> _state = new(
            MonsterState.Patrol,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public MonsterBrain Brain => _brain;
        public Rigidbody2D Body => _body;
        public NetworkTransform NetworkTransform => _networkTransform;
        public MonsterState ReplicatedState => _state.Value;

        public void Configure(
            MonsterBrain brain,
            Rigidbody2D body,
            NetworkTransform networkTransform)
        {
            _brain = brain;
            _body = body;
            _networkTransform = networkTransform;
        }

        public override void OnNetworkSpawn()
        {
            if (_brain == null || _body == null ||
                _networkTransform == null)
            {
                Debug.LogError(
                    "[Monster] Network authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _state.OnValueChanged += HandleStateChanged;
            if (IsServer)
            {
                _body.simulated = true;
                _brain.enabled = true;
                _brain.StateChanged += HandleServerBrainStateChanged;
                _state.Value = _brain.State;
            }
            else
            {
                _brain.enabled = false;
                _body.simulated = false;
                _brain.ApplyReplicatedStateForPresentation(_state.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            if (_brain != null)
            {
                _brain.StateChanged -= HandleServerBrainStateChanged;
                _brain.enabled = true;
            }

            if (_body != null)
            {
                _body.simulated = true;
            }
        }

        private void HandleServerBrainStateChanged(
            MonsterBrain brain,
            MonsterState state)
        {
            if (IsServer)
            {
                _state.Value = state;
            }
        }

        private void HandleStateChanged(
            MonsterState previousValue,
            MonsterState currentValue)
        {
            if (!IsServer)
            {
                _brain?.ApplyReplicatedStateForPresentation(currentValue);
            }
        }
    }
}
