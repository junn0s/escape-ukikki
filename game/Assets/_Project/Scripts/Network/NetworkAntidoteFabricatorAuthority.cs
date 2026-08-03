using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 백신실 제작기 한 대의 서버 권위 판정이다.
    /// 제작 시작은 레시피를 발견한 생존자만 가능하고, 완성품은 선착순으로 누구나 가져간다
    /// (docs/system-design-document.md §12.2~12.3).
    /// 회의 중에는 제작 타이머가 정지한다(GDD §16.2).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AntidoteFabricatorPrototype))]
    public sealed class NetworkAntidoteFabricatorAuthority : NetworkBehaviour
    {
        private const float ReplicationIntervalSeconds = 0.5f;

        [SerializeField] private AntidoteFabricatorPrototype _fabricator;
        [SerializeField] private AntidoteBalanceConfig _antidoteConfig;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        // 제작기는 공용 설비라 상태와 남은 시간을 전원에게 공개한다(UI·UX §10.2).
        private readonly NetworkVariable<FabricatorState> _state = new(
            FabricatorState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _remainingSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _totalDurationSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _nextReplicationTime;

        public AntidoteFabricatorPrototype Fabricator => _fabricator;
        public FabricatorState State => _state.Value;
        public float RemainingSeconds => _remainingSeconds.Value;

        public void Configure(
            AntidoteFabricatorPrototype fabricator,
            AntidoteBalanceConfig antidoteConfig,
            InteractionBalanceConfig interactionConfig)
        {
            _fabricator = fabricator;
            _antidoteConfig = antidoteConfig;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_fabricator == null || _antidoteConfig == null ||
                _interactionConfig == null)
            {
                Debug.LogError(
                    "[Antidote] Fabricator authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _fabricator.SetInteractionAuthority(
                CanLocalPlayerRequestInteraction,
                RequestInteraction);
            _state.OnValueChanged += HandleStateChanged;
            _remainingSeconds.OnValueChanged += HandleRemainingChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_fabricator != null)
            {
                _fabricator.ClearInteractionAuthority(this);
            }

            _state.OnValueChanged -= HandleStateChanged;
            _remainingSeconds.OnValueChanged -= HandleRemainingChanged;
        }

        private void Update()
        {
            if (!IsServer || _fabricator == null)
            {
                return;
            }

            var roundState = NetworkRoundState.Current;
            _fabricator.Fabricator.SetPaused(
                roundState != null && roundState.IsMeetingActive);
            _fabricator.Fabricator.Tick(Time.deltaTime);

            var stateChanged = _state.Value != _fabricator.Fabricator.State;
            if (!stateChanged && Time.unscaledTime < _nextReplicationTime)
            {
                return;
            }

            _nextReplicationTime =
                Time.unscaledTime + ReplicationIntervalSeconds;
            PublishServerState();
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner)
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                return false;
            }

            // 유령은 해독제를 조작할 수 없다(GDD §17).
            return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private void RequestInteraction(GameObject interactor)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestFabricatorActionRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestFabricatorActionRpc(RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _fabricator == null)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var playerObject = client.PlayerObject;
            var roundState = NetworkRoundState.Current;
            var allowsInteraction =
                roundState == null || roundState.AllowsMissionInteraction;
            var lifeState =
                playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection)
                    ? infection.LifeState
                    : PlayerLifeState.AliveHealthy;
            var role =
                playerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar)
                    ? avatar.Role
                    : PlayerRole.Unassigned;
            var inventory = playerObject
                .GetComponent<NetworkAntidoteInventoryAuthority>();
            var squaredDistance = (
                (Vector2)playerObject.transform.position -
                (Vector2)_fabricator.transform.position).sqrMagnitude;

            if (_fabricator.Fabricator.State == FabricatorState.Ready)
            {
                ServerHandleCollect(
                    senderClientId,
                    lifeState,
                    inventory,
                    allowsInteraction,
                    squaredDistance);
                return;
            }

            ServerHandleCraftStart(
                senderClientId,
                role,
                lifeState,
                inventory,
                allowsInteraction,
                squaredDistance);
        }

        private void ServerHandleCraftStart(
            ulong senderClientId,
            PlayerRole role,
            PlayerLifeState lifeState,
            NetworkAntidoteInventoryAuthority inventory,
            bool allowsInteraction,
            float squaredDistance)
        {
            var range = _interactionConfig.GeneralInteractionRangeMeters;
            var rejection = AntidoteCraftRules.ValidateCraftStart(
                role,
                lifeState,
                inventory != null && inventory.HasRecipe,
                _fabricator.Fabricator.State,
                allowsInteraction,
                squaredDistance <= range * range);
            if (rejection != AntidoteRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, rejection);
                return;
            }

            if (_fabricator.Fabricator.TryBeginCraft(
                    senderClientId,
                    _antidoteConfig.CraftDurationSeconds))
            {
                PublishServerState();
            }
        }

        private void ServerHandleCollect(
            ulong senderClientId,
            PlayerLifeState lifeState,
            NetworkAntidoteInventoryAuthority inventory,
            bool allowsInteraction,
            float squaredDistance)
        {
            var range = _interactionConfig.ItemPickupRangeMeters;
            var rejection = AntidoteCraftRules.ValidateCollect(
                lifeState,
                _fabricator.Fabricator.State,
                inventory != null ? inventory.CarriedCount : int.MaxValue,
                inventory != null ? inventory.MaxCarryCount : 0,
                allowsInteraction,
                squaredDistance <= range * range);
            if (rejection != AntidoteRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, rejection);
                return;
            }

            // 소지 칸을 먼저 확보한 뒤 제작기를 비운다.
            // 순서를 뒤집으면 소지 실패 시 완성품이 사라진다.
            if (!inventory.ServerTryAddAntidote())
            {
                PublishRejectionRpc(
                    senderClientId,
                    AntidoteRejectionReason.CarryLimitReached);
                return;
            }

            // 선착순 판정이다. 먼저 도착한 요청만 TryCollect에 성공한다.
            if (!_fabricator.Fabricator.TryCollect())
            {
                inventory.ServerTryConsumeAntidote();
                PublishRejectionRpc(
                    senderClientId,
                    AntidoteRejectionReason.NothingToCollect);
                return;
            }

            PublishServerState();
        }

        private void PublishServerState()
        {
            if (!IsServer || _fabricator == null)
            {
                return;
            }

            _state.Value = _fabricator.Fabricator.State;
            _remainingSeconds.Value = _fabricator.Fabricator.RemainingSeconds;
            _totalDurationSeconds.Value =
                _fabricator.Fabricator.TotalDurationSeconds;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _fabricator == null)
            {
                return;
            }

            _fabricator.ApplyAuthoritativeState(
                _state.Value,
                _remainingSeconds.Value,
                _totalDurationSeconds.Value);
        }

        private void HandleStateChanged(
            FabricatorState previousValue,
            FabricatorState currentValue)
        {
            ApplyReplicatedState();
        }

        private void HandleRemainingChanged(
            float previousValue,
            float currentValue)
        {
            ApplyReplicatedState();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            AntidoteRejectionReason rejectionReason)
        {
            if (NetworkManager != null &&
                NetworkManager.LocalClientId == targetClientId)
            {
                Debug.LogWarning(
                    $"[Antidote] Fabricator request rejected: {rejectionReason}.",
                    this);
            }
        }
    }
}
