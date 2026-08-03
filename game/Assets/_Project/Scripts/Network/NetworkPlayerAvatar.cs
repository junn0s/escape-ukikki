using System;
using MonkeyLab.Gameplay.Villain;
using Unity.Collections;
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

        /// <summary>결과 화면에서 돌아갈 로비 씬이다(mvp-scope §3.2).</summary>
        public const string MainMenuSceneName = "01_MainMenu";
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
        /// <summary>
        /// 라운드 화면 이름표용이다. 색상과 함께 공개 정보이므로 전원이 읽는다.
        /// 역할과 달리 숨길 필요가 없다.
        /// </summary>
        private readonly NetworkVariable<FixedString64Bytes> _nickname = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlayerRole> _role = new(
            PlayerRole.Unassigned,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public event Action StateChanged;

        public byte SlotIndex => _slotIndex.Value;
        public LobbyPlayerColor Color => _color.Value;

        /// <summary>비어 있으면 슬롯 번호로 대체해 표시한다.</summary>
        public string Nickname => _nickname.Value.ToString();
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
            // 닉네임은 라운드 화면의 이름표에도 필요하다(mvp-scope §3.3).
            _nickname.Value = new FixedString64Bytes(
                TrimNickname(player.Nickname));
            return true;
        }

        /// <summary>
        /// FixedString64Bytes 용량(61바이트)을 넘기면 예외가 나므로 미리 자른다.
        /// 한글은 글자당 3바이트라 20글자로 제한한다.
        /// </summary>
        private static string TrimNickname(string nickname)
        {
            const int maximumCharacters = 20;
            if (string.IsNullOrEmpty(nickname))
            {
                return string.Empty;
            }

            return nickname.Length <= maximumCharacters
                ? nickname
                : nickname.Substring(0, maximumCharacters);
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
