using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 백신실 중앙 제어 PC 한 대의 서버 권위 판정이다.
    /// 살아 있는 플레이어라면 역할과 무관하게 배합 코드를 발급받을 수 있다(GDD §14.2~14.3).
    /// 코드는 서버에서 생성하고 요청한 클라이언트에게만 전송한다(docs/system-design-document.md §12.1).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AntidoteTerminalPrototype))]
    public sealed class NetworkAntidoteTerminalAuthority : NetworkBehaviour
    {
        [SerializeField] private AntidoteTerminalPrototype _terminal;
        [SerializeField] private AntidoteBalanceConfig _antidoteConfig;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        // 분석 진행 여부는 공용 연출이라 전원에게 공개하지만, 발급된 코드 자체는
        // 요청자 전용 RPC로만 전달한다.
        private readonly NetworkVariable<bool> _isAnalyzing = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _analyzingUntilTime = -1f;
        private ulong _analyzingForClientId = ulong.MaxValue;

        public AntidoteTerminalPrototype Terminal => _terminal;

        public void Configure(
            AntidoteTerminalPrototype terminal,
            AntidoteBalanceConfig antidoteConfig,
            InteractionBalanceConfig interactionConfig)
        {
            _terminal = terminal;
            _antidoteConfig = antidoteConfig;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_terminal == null || _antidoteConfig == null ||
                _interactionConfig == null)
            {
                Debug.LogError(
                    "[Antidote] Terminal authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _terminal.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestInteraction);
            _isAnalyzing.OnValueChanged += HandleAnalyzingChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_terminal != null)
            {
                _terminal.ClearInteractionAuthority(this);
            }

            _isAnalyzing.OnValueChanged -= HandleAnalyzingChanged;
        }

        private void Update()
        {
            if (!IsServer || !_isAnalyzing.Value ||
                Time.unscaledTime < _analyzingUntilTime)
            {
                return;
            }

            ServerFinishAnalysis();
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
                RequestCodeIssueRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestCodeIssueRpc(RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _isAnalyzing.Value)
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
            var squaredDistance = (
                (Vector2)playerObject.transform.position -
                (Vector2)_terminal.transform.position).sqrMagnitude;
            var range = _interactionConfig.GeneralInteractionRangeMeters;

            var rejection = AntidoteCraftRules.ValidateCodeIssue(
                lifeState,
                allowsInteraction,
                squaredDistance <= range * range);
            if (rejection != AntidoteRejectionReason.None)
            {
                PublishRejectionRpc(senderClientId, rejection);
                return;
            }

            _analyzingForClientId = senderClientId;
            _analyzingUntilTime =
                Time.unscaledTime + _antidoteConfig.CodeAnalysisSeconds;
            _isAnalyzing.Value = true;
        }

        private void ServerFinishAnalysis()
        {
            _isAnalyzing.Value = false;
            if (_analyzingForClientId == ulong.MaxValue ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    _analyzingForClientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<
                    NetworkAntidoteInventoryAuthority>(out var inventory))
            {
                _analyzingForClientId = ulong.MaxValue;
                return;
            }

            var code = AntidoteCodeGenerator.Generate(
                _antidoteConfig.CodeLength,
                Random.Range(int.MinValue, int.MaxValue));
            inventory.ServerIssueCode(code);
            _analyzingForClientId = ulong.MaxValue;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _terminal == null)
            {
                return;
            }

            _terminal.ApplyAuthoritativeAnalyzingState(_isAnalyzing.Value);
        }

        private void HandleAnalyzingChanged(bool previousValue, bool currentValue)
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
                _terminal?.ApplyInteractionFeedback(rejectionReason);
                Debug.LogWarning(
                    $"[Antidote] Terminal request rejected: {rejectionReason}.",
                    this);
            }
        }
    }
}
