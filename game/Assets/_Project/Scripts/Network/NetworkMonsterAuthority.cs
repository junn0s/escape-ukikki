using System.Collections.Generic;
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
        public const ulong NoTargetClientId = ulong.MaxValue;

        private static readonly HashSet<NetworkMonsterAuthority>
            ActiveAuthoritySet = new();

        [SerializeField] private MonsterBrain _brain;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private NetworkTransform _networkTransform;

        private readonly NetworkVariable<MonsterState> _state = new(
            MonsterState.Patrol,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _targetClientId = new(
            NoTargetClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MonsterTarget _cachedServerTarget;
        private ulong _cachedServerTargetClientId = NoTargetClientId;

        public MonsterBrain Brain => _brain;
        public Rigidbody2D Body => _body;
        public NetworkTransform NetworkTransform => _networkTransform;
        public MonsterState ReplicatedState => _state.Value;
        public ulong TargetClientId => _targetClientId.Value;
        public static IEnumerable<NetworkMonsterAuthority>
            ActiveAuthorities => ActiveAuthoritySet;

        public bool IsThreateningClient(ulong clientId)
        {
            return IsSpawned && isActiveAndEnabled &&
                   ReplicatedState is MonsterState.Chase or MonsterState.Bite &&
                   TargetClientId == clientId;
        }

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
            ActiveAuthoritySet.Add(this);
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
                RefreshReplicatedTarget();
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
            ActiveAuthoritySet.Remove(this);
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

            _cachedServerTarget = null;
            _cachedServerTargetClientId = NoTargetClientId;
        }

        private void OnEnable()
        {
            if (IsSpawned)
            {
                ActiveAuthoritySet.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveAuthoritySet.Remove(this);
        }

        private void Update()
        {
            if (IsServer)
            {
                RefreshReplicatedTarget();
            }
        }

        private void HandleServerBrainStateChanged(
            MonsterBrain brain,
            MonsterState state)
        {
            if (IsServer)
            {
                _state.Value = state;
                RefreshReplicatedTarget();
            }
        }

        private void RefreshReplicatedTarget()
        {
            if (!IsServer || _brain == null)
            {
                return;
            }

            var target = _brain.State is MonsterState.Chase or MonsterState.Bite
                ? _brain.Senses?.Target
                : null;
            if (target != _cachedServerTarget)
            {
                _cachedServerTarget = target;
                _cachedServerTargetClientId = ResolveTargetClientId(target);
            }

            if (_targetClientId.Value != _cachedServerTargetClientId)
            {
                _targetClientId.Value = _cachedServerTargetClientId;
            }
        }

        private static ulong ResolveTargetClientId(MonsterTarget target)
        {
            if (target == null)
            {
                return NoTargetClientId;
            }

            var playerNetworkObject =
                target.GetComponentInParent<NetworkObject>();
            return playerNetworkObject != null && playerNetworkObject.IsSpawned
                ? playerNetworkObject.OwnerClientId
                : NoTargetClientId;
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

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveAuthoritySet.Clear();
        }
    }
}
