using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 환자 바이탈 기록 미션의 서버 권위 판정이다(GDD §10.2).
    /// 라운드 시작 시 서버가 4자리 숫자를 결정적으로 생성해 전원에게 공개한다
    /// (모니터 화면은 위장이 아니라 공용 정보라 숨길 이유가 없다).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PatientVitalsStation))]
    [RequireComponent(typeof(NetworkSurvivorMissionAuthority))]
    public sealed class NetworkPatientVitalsAuthority : NetworkBehaviour
    {
        [SerializeField] private PatientVitalsStation _station;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;
        [SerializeField] private int _seed = 20260808;

        private NetworkSurvivorMissionAuthority _missionAuthority;

        private readonly NetworkVariable<FixedString32Bytes> _code = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCompleted = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void Configure(
            PatientVitalsStation station,
            SurvivorMissionBalanceConfig config,
            InteractionBalanceConfig interactionConfig,
            int seed)
        {
            _station = station;
            _config = config;
            _interactionConfig = interactionConfig;
            _seed = seed;
        }

        public override void OnNetworkSpawn()
        {
            _missionAuthority = GetComponent<NetworkSurvivorMissionAuthority>();
            if (_station == null || _config == null ||
                _interactionConfig == null || _missionAuthority == null)
            {
                Debug.LogError(
                    "[Mission] Patient vitals authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            if (IsServer)
            {
                _code.Value = GenerateNumericCode(
                    _config.PatientVitalsCodeLength,
                    _seed);
            }

            _station.SetInteractionAuthority(
                this,
                CanLocalPlayerRequestInteraction,
                RequestSubmit);
            _code.OnValueChanged += HandleCodeChanged;
            _isCompleted.OnValueChanged += HandleCompletedChanged;
            ApplyReplicatedState();
        }

        public override void OnNetworkDespawn()
        {
            if (_station != null)
            {
                _station.ClearInteractionAuthority(this);
            }

            _code.OnValueChanged -= HandleCodeChanged;
            _isCompleted.OnValueChanged -= HandleCompletedChanged;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            return _missionAuthority != null &&
                   _missionAuthority.CanLocalPlayerRequestInteraction(
                       interactor);
        }

        private void RequestSubmit(GameObject interactor, string attempt)
        {
            if (CanLocalPlayerRequestInteraction(interactor))
            {
                RequestSubmitRpc(attempt);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestSubmitRpc(
            string attempt,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || _station == null ||
                _isCompleted.Value)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!_missionAuthority.ServerCanProcess(senderClientId))
            {
                return;
            }

            if (_code.Value.ToString() == attempt)
            {
                if (_missionAuthority.ServerTryComplete(senderClientId))
                {
                    _isCompleted.Value = true;
                }
            }
        }

        private static FixedString32Bytes GenerateNumericCode(
            int length,
            int seed)
        {
            var random = (uint)(seed == 0 ? 1 : seed);
            var code = new FixedString32Bytes();
            for (var index = 0; index < length; index++)
            {
                random = NextRandom(random);
                code.Append((char)('0' + (random % 10)));
            }

            return code;
        }

        private static uint NextRandom(uint state)
        {
            // Xorshift32. Unity 난수를 쓰지 않아 서버·테스트 결과가 일치한다.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private void ApplyReplicatedState()
        {
            // 호스트도 자기 화면에는 복제 상태를 반영해야 한다. 서버라고
            // 건너뛰면 진행 표시가 멈춘 채 완료만 처리된다.
            if (_station == null)
            {
                return;
            }

            _station.ApplyAuthoritativeState(
                _code.Value.ToString(),
                _isCompleted.Value);
        }

        private void HandleCodeChanged(
            FixedString32Bytes previous,
            FixedString32Bytes current)
        {
            ApplyReplicatedState();
        }

        private void HandleCompletedChanged(bool previous, bool current)
        {
            ApplyReplicatedState();
        }
    }
}
