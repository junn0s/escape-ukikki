using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 슬라이드 글라스 닦기 미션의 서버 권위 판정이다(GDD §10.2).
    /// 얼룩 하나를 문지를 때마다 서버가 횟수를 확정한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SlideGlassStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkSlideGlassAuthority : NetworkBehaviour
    {
        private const int MaxStainCount = 8;

        [SerializeField] private SlideGlassStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        private NetworkSurvivorMissionAuthority _missionAuthority;

        // 얼룩당 문지름 횟수를 4비트씩 packing한다(최대 15회, 밸런스 표는 5회).
        private readonly NetworkVariable<ulong> _scrubCounts = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            SlideGlassStation station,
            SurvivorMissionBalanceConfig config,
            InteractionBalanceConfig interactionConfig)
        {
            _station = station;
            _config = config;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority =
                GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _config == null ||
                _interactionConfig == null || _missionAuthority == null ||
                _config.SlideGlassStainCount > MaxStainCount ||
                _config.SlideGlassScrubsPerStain > 15)
            {
                Debug.LogError(
                    "[Mission] Slide glass authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestScrub);
            _scrubCounts.OnValueChanged += HandleScrubCountsChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _scrubCounts.OnValueChanged -= HandleScrubCountsChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestScrub(GameObject interactor, int stainIndex)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestScrubRpc(stainIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestScrubRpc(
            int stainIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                stainIndex < 0 || stainIndex >= _config.SlideGlassStainCount)
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

            var shift = stainIndex * 4;
            var current = (int)((_scrubCounts.Value >> shift) & 0xF);
            if (current >= _config.SlideGlassScrubsPerStain)
            {
                return;
            }

            var mask = ~(0xFUL << shift);
            var nextCounts =
                (_scrubCounts.Value & mask) |
                ((ulong)(current + 1) << shift);
            var isCompleted = true;
            for (var index = 0;
                 index < _config.SlideGlassStainCount;
                 index++)
            {
                if (((nextCounts >> (index * 4)) & 0xF) <
                    (ulong)_config.SlideGlassScrubsPerStain)
                {
                    isCompleted = false;
                    break;
                }
            }

            if (isCompleted &&
                !_missionAuthority.ServerTryComplete(senderClientId))
            {
                return;
            }

            _scrubCounts.Value = nextCounts;
        }

        private void ApplyReplicatedState()
        {
            if (IsServer || _station == null || _config == null)
            {
                return;
            }

            var counts = new int[_config.SlideGlassStainCount];
            for (var index = 0; index < counts.Length; index++)
            {
                counts[index] =
                    (int)((_scrubCounts.Value >> (index * 4)) & 0xF);
            }

            _station.ApplyAuthoritativeState(counts);
        }

        private void HandleScrubCountsChanged(ulong previous, ulong current)
        {
            ApplyReplicatedState();
        }
    }
}
