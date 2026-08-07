using System;
using MonkeyLab.Gameplay.Monsters;
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
        [SerializeField] private MonsterTarget _monsterTarget;
        [SerializeField] private MonsterBalanceConfig _monsterConfig;

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
        private readonly NetworkVariable<bool> _isFlashlightEnabled = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);


        public event Action StateChanged;

        public byte SlotIndex => _slotIndex.Value;
        public LobbyPlayerColor Color => _color.Value;

        /// <summary>비어 있으면 슬롯 번호로 대체해 표시한다.</summary>
        public string Nickname => _nickname.Value.ToString();
        public PlayerRole Role => _role.Value;
        public bool IsFlashlightEnabled => _isFlashlightEnabled.Value;
        public NetworkVariableReadPermission RoleReadPermission =>
            _role.ReadPerm;
        public NetworkVariableWritePermission RoleWritePermission =>
            _role.WritePerm;
        public bool IsConfigured => SlotIndex != UnassignedSlot;
        public bool HasAssignedRole => Role != PlayerRole.Unassigned;
        public NetworkTransform NetworkTransform => _networkTransform;
        public MonsterTarget MonsterTarget => _monsterTarget;

        public void Configure(
            NetworkTransform networkTransform,
            MonsterTarget monsterTarget = null,
            MonsterBalanceConfig monsterConfig = null)
        {
            _networkTransform = networkTransform;
            _monsterTarget = monsterTarget;
            _monsterConfig = monsterConfig;
        }

        public override void OnNetworkSpawn()
        {
            _slotIndex.OnValueChanged += HandleSlotChanged;
            _color.OnValueChanged += HandleColorChanged;
            _role.OnValueChanged += HandleRoleChanged;
            _isFlashlightEnabled.OnValueChanged +=
                HandleFlashlightStateChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            ApplyMonsterRoleRules(_role.Value);
            TryPlaceOwnerAtLaboratorySpawn();
            StateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _slotIndex.OnValueChanged -= HandleSlotChanged;
            _color.OnValueChanged -= HandleColorChanged;
            _role.OnValueChanged -= HandleRoleChanged;
            _isFlashlightEnabled.OnValueChanged -=
                HandleFlashlightStateChanged;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        public override void OnNetworkPreDespawn()
        {
            if (IsServer)
            {
                GameSessionController.Current?.ServerCaptureBeforeDisconnect(
                    OwnerClientId,
                    gameObject);
            }
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
            ApplyMonsterRoleRules(role);
            return true;
        }

        public void RequestSetFlashlight(bool isEnabled)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (IsServer)
            {
                ServerSetFlashlight(isEnabled);
                return;
            }

            SetFlashlightStateRpc(isEnabled, default);
        }

        [Rpc(SendTo.Server)]
        private void SetFlashlightStateRpc(
            bool isEnabled,
            RpcParams rpcParams)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ServerSetFlashlight(isEnabled);
        }

        private void ServerSetFlashlight(bool isEnabled)
        {
            if (!IsServer || _isFlashlightEnabled.Value == isEnabled)
            {
                return;
            }

            // 복제만 한다. 손전등은 감지 조건이 아니므로 서버가 MonsterTarget을
            // 갱신할 필요가 없다(GDD 1.6). OnValueChanged가 연출을 알린다.
            _isFlashlightEnabled.Value = isEnabled;
        }

        /// <summary>
        /// 같은 인증 PlayerId가 30초 안에 재접속했을 때 공개 로비 상태와
        /// 비공개 역할을 새 PlayerObject에 함께 복원한다(GDD §19.2).
        /// </summary>
        public bool ServerRestoreReconnectSnapshot(
            byte slotIndex,
            LobbyPlayerColor color,
            string nickname,
            PlayerRole role,
            Vector3 position)
        {
            if (!IsServer || slotIndex >= NetworkPlayerSpawnLayout.SlotCount ||
                (role != PlayerRole.Survivor &&
                 role != PlayerRole.Villain))
            {
                return false;
            }

            _slotIndex.Value = slotIndex;
            _color.Value = color;
            _nickname.Value = new FixedString64Bytes(
                TrimNickname(nickname));
            _role.Value = role;
            ApplyMonsterRoleRules(role);

            RestoreOwnerPositionRpc(
                position,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
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
            ApplyMonsterRoleRules(currentValue);
            StateChanged?.Invoke();
        }

        private void HandleFlashlightStateChanged(
            bool previousValue,
            bool currentValue)
        {
            // 손전등은 감지에 관여하지 않는다(GDD 1.6). 복제 값은 원격
            // 플레이어의 손전등 연출에만 쓰이며 NetworkPlayerPresentation이 읽는다.
            StateChanged?.Invoke();
        }

        private void ApplyMonsterRoleRules(PlayerRole role)
        {
            if (_monsterTarget == null)
            {
                return;
            }

            // 빌런은 추격 대상에는 남지만 물기 동작 자체를 받지 않는다(GDD §5.2).
            // Unassigned는 역할 유출 없이 기존 생존자 기본값을 유지한다.
            var isVillain = role == PlayerRole.Villain;
            _monsterTarget.SetCanBeBitten(!isVillain);
            _monsterTarget.SetCanBeInfected(!isVillain);
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

        [Rpc(SendTo.SpecifiedInParams)]
        private void RestoreOwnerPositionRpc(
            Vector3 position,
            RpcParams rpcParams = default)
        {
            if (!IsOwner || _networkTransform == null)
            {
                return;
            }

            _networkTransform.Teleport(
                position,
                Quaternion.identity,
                Vector3.one);
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.rotation = 0f;
            }
        }
    }
}
