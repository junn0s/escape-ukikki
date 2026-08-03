using MonkeyLab.Gameplay.Infection;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 플레이어 한 명의 해독제 소지와 레시피 발견 여부를 서버 권위로 관리한다.
    /// 소지 한도는 1개이며(GDD §14.3), 레시피는 개인 정보라 소유자에게만 복제한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkAntidoteInventoryAuthority : NetworkBehaviour
    {
        [SerializeField] private AntidoteService _antidoteService;
        [SerializeField] private NetworkInfectionAuthority _infectionAuthority;
        [SerializeField] private AntidoteBalanceConfig _config;

        private readonly NetworkVariable<int> _carriedCount = new(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _hasRecipe = new(
            false,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public AntidoteService AntidoteService => _antidoteService;

        /// <summary>서버와 소유자만 실제 값을 본다.</summary>
        public int CarriedCount => _carriedCount.Value;
        public bool HasRecipe => _hasRecipe.Value;
        public int MaxCarryCount => _config != null ? _config.MaxCarryCount : 1;
        public bool HasFreeCarrySlot => CarriedCount < MaxCarryCount;

        public void Configure(
            AntidoteService antidoteService,
            NetworkInfectionAuthority infectionAuthority,
            AntidoteBalanceConfig config)
        {
            _antidoteService = antidoteService;
            _infectionAuthority = infectionAuthority;
            _config = config;
        }

        public override void OnNetworkSpawn()
        {
            if (_antidoteService == null || _config == null)
            {
                Debug.LogError(
                    "[Antidote] Inventory authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _antidoteService.SetExternallyDriven(true);
            _carriedCount.OnValueChanged += HandleCarriedCountChanged;
            _hasRecipe.OnValueChanged += HandleRecipeStateChanged;
            if (IsOwner)
            {
                _antidoteService.UseCompleted += HandleLocalUseCompleted;
            }

            if (IsServer && _infectionAuthority?.InfectionService != null)
            {
                _infectionAuthority.InfectionService.InfectionExpired +=
                    HandleServerInfectionExpired;
            }

            MirrorInventoryState();
        }

        public override void OnNetworkDespawn()
        {
            _carriedCount.OnValueChanged -= HandleCarriedCountChanged;
            _hasRecipe.OnValueChanged -= HandleRecipeStateChanged;
            if (_antidoteService != null)
            {
                _antidoteService.UseCompleted -= HandleLocalUseCompleted;
                _antidoteService.SetExternallyDriven(false);
            }

            if (_infectionAuthority?.InfectionService != null)
            {
                _infectionAuthority.InfectionService.InfectionExpired -=
                    HandleServerInfectionExpired;
            }
        }

        /// <summary>
        /// 감염 타이머가 0이 되면 소지 해독제를 지정 드롭 지점에 놓는다(SDD §13.3의 2단계).
        /// 바닥 자유 드롭이 없으므로 가장 가까운 보관 칸이 지정 드롭 지점이다.
        /// 모든 보관 칸이 가득 차면 해독제를 없애지 않고 그대로 남긴다.
        /// </summary>
        private void HandleServerInfectionExpired(InfectionService service)
        {
            if (!IsServer || _carriedCount.Value <= 0)
            {
                return;
            }

            if (!NetworkStorageLockerAuthority.ServerDepositAtNearestLocker(
                    transform.position))
            {
                Debug.LogWarning(
                    "[Antidote] Every storage locker is full. " +
                    "The carried antidote stays with the ghost.",
                    this);
                return;
            }

            _carriedCount.Value--;
        }

        /// <summary>완성품 획득과 보관함 인출에서 호출한다.</summary>
        public bool ServerTryAddAntidote()
        {
            if (!IsServer || !HasFreeCarrySlot)
            {
                return false;
            }

            _carriedCount.Value++;
            return true;
        }

        /// <summary>해독제 사용과 보관함 보관에서 호출한다.</summary>
        public bool ServerTryConsumeAntidote()
        {
            if (!IsServer || _carriedCount.Value <= 0)
            {
                return false;
            }

            _carriedCount.Value--;
            return true;
        }

        /// <summary>
        /// 다음 라운드를 위해 소지품과 레시피 발견 상태를 비운다.
        /// 레시피는 라운드마다 다시 배치되므로 반드시 함께 초기화한다.
        /// </summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer)
            {
                return;
            }

            _carriedCount.Value = 0;
            _hasRecipe.Value = false;
        }

        /// <summary>배정된 후보에서 레시피를 발견했을 때 서버가 확정한다.</summary>
        public bool ServerGrantRecipe()
        {
            if (!IsServer || _hasRecipe.Value)
            {
                return false;
            }

            _hasRecipe.Value = true;
            return true;
        }

        private void HandleLocalUseCompleted(AntidoteService service)
        {
            RequestUseAntidoteRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestUseAntidoteRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId ||
                _infectionAuthority == null)
            {
                return;
            }

            // 서버가 감염 상태와 소지 여부를 다시 검사한 뒤에만 치료를 확정한다.
            var infectionService = _infectionAuthority.InfectionService;
            if (infectionService == null || !infectionService.IsInfected ||
                _carriedCount.Value <= 0)
            {
                return;
            }

            if (!infectionService.TryCure())
            {
                return;
            }

            _carriedCount.Value--;
        }

        private void HandleCarriedCountChanged(
            int previousValue,
            int currentValue)
        {
            MirrorInventoryState();
        }

        private void HandleRecipeStateChanged(
            bool previousValue,
            bool currentValue)
        {
            MirrorInventoryState();
        }

        private void MirrorInventoryState()
        {
            if (_antidoteService == null || (!IsOwner && !IsServer))
            {
                return;
            }

            _antidoteService.ApplyAuthoritativeCarriedCount(
                _carriedCount.Value);
            _antidoteService.ApplyAuthoritativeRecipeState(_hasRecipe.Value);
        }
    }
}
