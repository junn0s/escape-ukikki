using MonkeyLab.Gameplay.Infection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 플레이어 한 명의 해독제 소지와 배합 코드 보유 여부를 서버 권위로 관리한다.
    /// 소지 한도는 1개이며(GDD §14.3), 코드는 본인에게만 복제한다(GDD §14.2).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkAntidoteInventoryAuthority : NetworkBehaviour
    {
        [SerializeField] private AntidoteService _antidoteService;
        [SerializeField] private NetworkInfectionAuthority _infectionAuthority;
        [SerializeField] private AntidoteBalanceConfig _config;

        private readonly AntidoteCodeSession _codeSession = new();

        private readonly NetworkVariable<int> _carriedCount = new(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _hasValidCode = new(
            false,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString32Bytes> _issuedCode = new(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public AntidoteService AntidoteService => _antidoteService;

        /// <summary>서버와 소유자만 실제 값을 본다.</summary>
        public int CarriedCount => _carriedCount.Value;
        public bool HasValidCode => _hasValidCode.Value;
        public int MaxCarryCount => _config != null ? _config.MaxCarryCount : 1;
        public bool HasFreeCarrySlot => CarriedCount < MaxCarryCount;

        /// <summary>서버에서만 실제 코드 문자열에 접근한다(RPC 판정용).</summary>
        public AntidoteCodeSession ServerCodeSession => _codeSession;

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
            _hasValidCode.OnValueChanged += HandleCodeStateChanged;
            _issuedCode.OnValueChanged += HandleCodeStateChanged;
            if (IsOwner)
            {
                _antidoteService.UseCompleted += HandleLocalUseCompleted;
            }

            MirrorInventoryState();
        }

        public override void OnNetworkDespawn()
        {
            _carriedCount.OnValueChanged -= HandleCarriedCountChanged;
            _hasValidCode.OnValueChanged -= HandleCodeStateChanged;
            _issuedCode.OnValueChanged -= HandleCodeStateChanged;
            if (_antidoteService != null)
            {
                _antidoteService.UseCompleted -= HandleLocalUseCompleted;
                _antidoteService.SetExternallyDriven(false);
            }
        }

        /// <summary>완성품 획득에서 호출한다.</summary>
        public bool ServerTryAddAntidote()
        {
            if (!IsServer || !HasFreeCarrySlot)
            {
                return false;
            }

            _carriedCount.Value++;
            return true;
        }

        /// <summary>해독제 사용에서 호출한다.</summary>
        public bool ServerTryConsumeAntidote()
        {
            if (!IsServer || _carriedCount.Value <= 0)
            {
                return false;
            }

            _carriedCount.Value--;
            return true;
        }

        /// <summary>다음 라운드를 위해 소지품과 배합 코드를 비운다.</summary>
        public void ServerResetForNewRound()
        {
            if (!IsServer)
            {
                return;
            }

            _carriedCount.Value = 0;
            ServerInvalidateCode();
        }

        /// <summary>
        /// 중앙 제어 PC가 새 코드를 발급했을 때 서버가 확정한다(GDD §14.2).
        /// 이전 코드와 오입 횟수를 덮어쓴다.
        /// </summary>
        public void ServerIssueCode(string code)
        {
            if (!IsServer)
            {
                return;
            }

            _codeSession.IssueCode(code);
            _hasValidCode.Value = true;
            _issuedCode.Value = code;
        }

        /// <summary>
        /// 제작대에서 입력한 코드를 판정한다. 정답이면 참을 반환하고 코드는 유지된다.
        /// 오답이 누적 최대치에 도달하면 코드를 무효화한다(SDD §12.4).
        /// </summary>
        public bool ServerTrySubmitCode(string attempt)
        {
            if (!IsServer)
            {
                return false;
            }

            var isCorrect = _codeSession.TrySubmit(
                attempt,
                _config != null ? _config.MaxCodeAttempts : 3);
            if (!_codeSession.HasValidCode)
            {
                ServerInvalidateCode();
            }

            return isCorrect;
        }

        public void ServerInvalidateCode()
        {
            if (!IsServer)
            {
                return;
            }

            _codeSession.Invalidate();
            _hasValidCode.Value = false;
            _issuedCode.Value = default;
        }

        /// <summary>30초 내 재접속한 플레이어의 개인 해독제 상태를 복원한다.</summary>
        public bool ServerRestoreReconnectSnapshot(int carriedCount)
        {
            if (!IsServer || _config == null || carriedCount < 0 ||
                carriedCount > _config.MaxCarryCount)
            {
                return false;
            }

            _carriedCount.Value = carriedCount;
            // 배합 코드는 저장하지 않는 개인 기억 정보이므로 재접속 시 복원하지 않는다.
            ServerInvalidateCode();
            MirrorInventoryState();
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

        private void HandleCodeStateChanged(
            bool previousValue,
            bool currentValue)
        {
            MirrorInventoryState();
        }

        private void HandleCodeStateChanged(
            FixedString32Bytes previousValue,
            FixedString32Bytes currentValue)
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
            _antidoteService.ApplyAuthoritativeCodeState(
                _hasValidCode.Value,
                _issuedCode.Value.ToString());
        }
    }
}
