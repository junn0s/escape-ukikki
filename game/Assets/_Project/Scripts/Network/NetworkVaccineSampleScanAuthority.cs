using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 백신 샘플 스캔 미션의 서버 권위 판정이다(GDD §10.2). 다음 순서가
    /// 아닌 샘플을 스캔하면 서버가 거부한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(VaccineSampleScanStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkVaccineSampleScanAuthority : NetworkBehaviour
    {
        [SerializeField] private VaccineSampleScanStation _station;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<int> _scannedCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            VaccineSampleScanStation station,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority =
                GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _interactionConfig == null ||
                _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] Vaccine sample scan authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestScan);
            _scannedCount.OnValueChanged += HandleScannedCountChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _scannedCount.OnValueChanged -= HandleScannedCountChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestScan(GameObject interactor, int sampleIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestScanRpc(sampleIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestScanRpc(
            int sampleIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                sampleIndex < 0 || sampleIndex >= _station.Rules.SampleCount)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (_missionAuthority == null ||
                !_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

            if (!NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var playerObject = client.PlayerObject;
            var squaredDistance = (
                (Vector2)playerObject.transform.position -
                (Vector2)_station.transform.position).sqrMagnitude;
            var range = _interactionConfig.GeneralInteractionRangeMeters;
            if (squaredDistance > range * range)
            {
                return;
            }

            if (_station.Rules.TryScan(sampleIndex))
            {
                if (_station.Rules.IsCompleted &&
                    !_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _station.Rules.Reset();
                    _scannedCount.Value = 0;
                    return;
                }

                _scannedCount.Value = _station.Rules.ScannedCount;
            }
        }

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(_scannedCount.Value);
        }

        private void HandleScannedCountChanged(int previous, int current)
        {
            ApplyReplicatedState();
        }
    }
}
