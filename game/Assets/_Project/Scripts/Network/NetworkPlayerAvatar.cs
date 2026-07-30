using System;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Network
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        public const string LaboratorySceneName = "10_Laboratory";
        public const byte UnassignedSlot = byte.MaxValue;

        [SerializeField] private NetworkTransform _networkTransform;

        private readonly NetworkVariable<byte> _slotIndex = new(
            UnassignedSlot,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<LobbyPlayerColor> _color = new(
            LobbyPlayerColor.Blue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlayerRole> _role = new(
            PlayerRole.Unassigned,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public event Action StateChanged;

        public byte SlotIndex => _slotIndex.Value;
        public LobbyPlayerColor Color => _color.Value;
        public PlayerRole Role => _role.Value;
        public NetworkVariableReadPermission RoleReadPermission =>
            _role.ReadPerm;
        public NetworkVariableWritePermission RoleWritePermission =>
            _role.WritePerm;
        public bool IsConfigured => SlotIndex != UnassignedSlot;
        public bool HasAssignedRole => Role != PlayerRole.Unassigned;
        public NetworkTransform NetworkTransform => _networkTransform;

        public void Configure(NetworkTransform networkTransform)
        {
            _networkTransform = networkTransform;
        }

        public override void OnNetworkSpawn()
        {
            _slotIndex.OnValueChanged += HandleSlotChanged;
            _color.OnValueChanged += HandleColorChanged;
            _role.OnValueChanged += HandleRoleChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            TryPlaceOwnerAtLaboratorySpawn();
            StateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _slotIndex.OnValueChanged -= HandleSlotChanged;
            _color.OnValueChanged -= HandleColorChanged;
            _role.OnValueChanged -= HandleRoleChanged;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        public bool ServerApplyLobbyState(LobbyPlayerState player)
        {
            if (!IsServer || player.ClientId != OwnerClientId ||
                player.SlotIndex < 0 ||
                player.SlotIndex >= NetworkPlayerSpawnLayout.SlotCount)
            {
                return false;
            }

            _slotIndex.Value = (byte)player.SlotIndex;
            _color.Value = player.Color;
            return true;
        }

        public bool ServerAssignRole(PlayerRole role)
        {
            if (!IsServer ||
                (role != PlayerRole.Survivor &&
                 role != PlayerRole.Villain))
            {
                return false;
            }

            _role.Value = role;
            return true;
        }

        private void HandleSlotChanged(byte previousValue, byte currentValue)
        {
            TryPlaceOwnerAtLaboratorySpawn();
            StateChanged?.Invoke();
        }

        private void HandleColorChanged(
            LobbyPlayerColor previousValue,
            LobbyPlayerColor currentValue)
        {
            StateChanged?.Invoke();
        }

        private void HandleRoleChanged(
            PlayerRole previousValue,
            PlayerRole currentValue)
        {
            StateChanged?.Invoke();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            TryPlaceOwnerAtLaboratorySpawn();
            StateChanged?.Invoke();
        }

        private void TryPlaceOwnerAtLaboratorySpawn()
        {
            if (!IsSpawned || !IsOwner || _networkTransform == null ||
                SceneManager.GetActiveScene().name != LaboratorySceneName ||
                !NetworkPlayerSpawnLayout.TryGetLaboratoryPosition(
                    SlotIndex,
                    out var spawnPosition))
            {
                return;
            }

            _networkTransform.Teleport(
                spawnPosition,
                Quaternion.identity,
                Vector3.one);

            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = spawnPosition;
                body.rotation = 0f;
            }
        }
    }
}
