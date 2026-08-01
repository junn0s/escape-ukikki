using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 스피커 리모컨의 서버 권위 상태다.
    /// 빌런이 방을 고르면 서버가 검증한 뒤 그 방에서 Large 소음을 발행하고
    /// 붉은 LED 흔적을 남긴다(GDD §13.1).
    ///
    /// 남은 쿨타임은 빌런 본인에게만 보낸다. 생존자가 쿨타임을 보면
    /// 스피커가 언제 눌렸는지 역산할 수 있어 단서 설계가 무너진다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkSpeakerAuthority : NetworkBehaviour
    {
        [SerializeField] private SpeakerBalanceConfig _config;
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private SpeakerPlacement[] _speakers =
            Array.Empty<SpeakerPlacement>();

        private readonly SpeakerCooldownState _cooldown = new();
        private readonly Dictionary<ulong, uint> _lastProcessedSequences =
            new();

        private uint _localSequence;
        private float _localRemainingCooldownSeconds;

        public static NetworkSpeakerAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        /// <summary>빌런 본인 화면에서만 의미 있는 값이다.</summary>
        public event Action LocalCooldownChanged;

        /// <summary>스피커가 울린 방을 전원에게 알린다(소리는 공개 정보다).</summary>
        public event Action<string, float> SpeakerActivated;

        public SpeakerBalanceConfig Config => _config;
        public int SpeakerCount => _speakers?.Length ?? 0;
        public float LocalRemainingCooldownSeconds =>
            _localRemainingCooldownSeconds;
        public bool IsLocallyReady => _localRemainingCooldownSeconds <= 0f;

        public IReadOnlyList<SpeakerPlacement> Speakers =>
            _speakers ?? Array.Empty<SpeakerPlacement>();

        public void Configure(
            SpeakerBalanceConfig config,
            NoiseService noiseService,
            SpeakerPlacement[] speakers)
        {
            _config = config;
            _noiseService = noiseService;
            _speakers = speakers ?? Array.Empty<SpeakerPlacement>();
        }

        public override void OnNetworkSpawn()
        {
            if (_config == null || _noiseService == null)
            {
                Debug.LogError(
                    "[Speaker] Speaker authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentChanged?.Invoke();

            if (IsServer)
            {
                _cooldown.Reset();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            _lastProcessedSequences.Clear();
            _localRemainingCooldownSeconds = 0f;
        }

        private void Update()
        {
            if (_localRemainingCooldownSeconds <= 0f)
            {
                return;
            }

            _localRemainingCooldownSeconds = Mathf.Max(
                0f,
                _localRemainingCooldownSeconds - Time.deltaTime);
            LocalCooldownChanged?.Invoke();
        }

        /// <summary>빌런 지도 UI가 호출한다.</summary>
        public void RequestSpeaker(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) || !IsSpawned)
            {
                return;
            }

            RequestSpeakerRpc(roomId, NextLocalSequence());
        }

        [Rpc(SendTo.Server)]
        private void RequestSpeakerRpc(
            string roomId,
            uint clientSequence,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _config == null ||
                _noiseService == null)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!IsNewSequence(senderClientId, clientSequence))
            {
                return;
            }

            _lastProcessedSequences[senderClientId] = clientSequence;

            var hasPlayer =
                NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) &&
                client.PlayerObject != null;
            var playerObject = hasPlayer ? client.PlayerObject : null;
            var avatar = playerObject != null
                ? playerObject.GetComponent<NetworkPlayerAvatar>()
                : null;
            var infection = playerObject != null
                ? playerObject.GetComponent<NetworkInfectionAuthority>()
                : null;
            var roundState = NetworkRoundState.Current;
            var placement = FindSpeaker(roomId);
            var serverTime = NetworkManager.ServerTime.Time;

            var rejectionReason = SpeakerRules.Validate(
                avatar != null ? avatar.Role : PlayerRole.Unassigned,
                infection == null ||
                infection.LifeState != PlayerLifeState.DeadGhost,
                roundState == null || roundState.AllowsMissionInteraction,
                placement != null,
                _cooldown.IsReady(serverTime));

            if (rejectionReason != SpeakerRejectionReason.None)
            {
                PublishRejectionRpc(
                    senderClientId,
                    rejectionReason,
                    _cooldown.GetRemainingSeconds(serverTime));
                return;
            }

            _cooldown.StartCooldown(
                serverTime,
                _config.SpeakerCooldownSeconds);

            // 소음 사건은 하나만 발행한다. 3초 재생은 연출이며
            // 같은 noiseId를 공유한다(balance-and-telemetry.md §5.1).
            _noiseService.EmitNoise(
                NoiseSourceType.Speaker,
                placement.transform.position,
                placement.RoomId,
                NoiseIntensity.Large);

            // 사용한 방의 스피커에 붉은 LED 흔적이 남는다(GDD §13.1).
            NetworkClueAuthority.Current?.ServerActivateClue(
                ClueKind.SpeakerRedLed,
                placement.RoomId);

            // 누가 눌렀는지는 기록하지 않는다(GDD §13.1).
            Debug.Log(
                $"[Speaker] Activated in room '{placement.RoomId}'.",
                this);

            PublishCooldownRpc(
                _config.SpeakerCooldownSeconds,
                RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
            PublishActivationRpc(
                placement.RoomId,
                _config.SpeakerPlaybackSeconds);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PublishCooldownRpc(
            float cooldownSeconds,
            RpcParams rpcParams = default)
        {
            _localRemainingCooldownSeconds = cooldownSeconds;
            LocalCooldownChanged?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishActivationRpc(
            string roomId,
            float playbackSeconds)
        {
            SpeakerActivated?.Invoke(roomId, playbackSeconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PublishRejectionRpc(
            ulong targetClientId,
            SpeakerRejectionReason rejectionReason,
            float remainingCooldownSeconds)
        {
            if (NetworkManager == null ||
                NetworkManager.LocalClientId != targetClientId)
            {
                return;
            }

            if (rejectionReason == SpeakerRejectionReason.OnCooldown)
            {
                _localRemainingCooldownSeconds = remainingCooldownSeconds;
                LocalCooldownChanged?.Invoke();
            }

            Debug.LogWarning(
                $"[Speaker] Request rejected: {rejectionReason}.",
                this);
        }

        private SpeakerPlacement FindSpeaker(string roomId)
        {
            if (_speakers == null || string.IsNullOrEmpty(roomId))
            {
                return null;
            }

            for (var index = 0; index < _speakers.Length; index++)
            {
                if (_speakers[index] != null &&
                    _speakers[index].RoomId == roomId)
                {
                    return _speakers[index];
                }
            }

            return null;
        }

        private bool IsNewSequence(ulong clientId, uint clientSequence)
        {
            return !_lastProcessedSequences.TryGetValue(
                       clientId,
                       out var previousSequence) ||
                   clientSequence > previousSequence;
        }

        private uint NextLocalSequence()
        {
            _localSequence++;
            if (_localSequence == 0)
            {
                _localSequence = 1;
            }

            return _localSequence;
        }
    }
}
