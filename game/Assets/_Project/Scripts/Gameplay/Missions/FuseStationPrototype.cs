using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    public sealed class FuseStationPrototype : MonoBehaviour, IInteractable
    {
        private const float RestoredIndicatorIntensity = 4f;

        [SerializeField] private Renderer _stationRenderer;
        [SerializeField] private Light _indicatorLight;
        [SerializeField] private FuseMissionConfig _config;
        [SerializeField] private MissionPrototypeKind _kind;
        [SerializeField] private BatteryReceiverPrototype _batteryReceiver;
        [SerializeField] private Color _restoredColor = new(0.15f, 1f, 0.35f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private FuseMissionInstance _mission;
        private BreakerTimingMissionInstance _breakerMission;
        private CctvRebootMissionInstance _cctvMission;
        private SampleSortingMissionInstance _sampleMission;
        private BatteryTransportMissionInstance _batteryMission;
        private PressureValveMissionInstance _pressureMission;
        private SecurityCircuitMissionInstance _securityCircuitMission;
        private AntennaAlignmentMissionInstance _antennaMission;
        private ServerLogRecoveryMissionInstance _serverLogMission;
        private GameObject _activeInteractor;
        private PlayerInputReader _activeInput;
        private PlayerMotor _activeMotor;
        private PlayerAimController _activeAim;
        private bool _isRestored;
        private bool _isBatteryInsertionOpen;
        private Color _defaultStationColor = Color.white;
        private Color _defaultIndicatorColor = Color.white;
        private float _defaultIndicatorIntensity;
        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;

        public event Action<FuseStationPrototype> MissionStarted;
        public event Action<FuseStationPrototype> ProgressChanged;
        public event Action<FuseStationPrototype, int, int> MissionFailed;
        public event Action<FuseStationPrototype> MissionCancelled;
        public event Action<FuseStationPrototype> MissionCompleted;
        public event Action<FuseStationPrototype, MissionInputCommand>
            MissionInputSubmitted;

        public string Prompt => _kind switch
        {
            MissionPrototypeKind.FuseSequence => "퓨즈 순서 맞추기",
            MissionPrototypeKind.BreakerSequence => "차단기 기동하기",
            MissionPrototypeKind.CctvReboot => "CCTV 재부팅하기",
            MissionPrototypeKind.SampleSorting => "시료 분류하기",
            MissionPrototypeKind.BatteryTransport => "비상 배터리 분리하기",
            MissionPrototypeKind.PressureValves => "압력 밸브 조정하기",
            MissionPrototypeKind.SecurityCircuit => "출입문 보안 회로 복구하기",
            MissionPrototypeKind.AntennaAlignment => "구조 안테나 조율하기",
            MissionPrototypeKind.ServerLogRecovery => "손상 서버 로그 복구하기",
            _ => "미션 수행하기"
        };
        public Transform InteractionTransform => transform;
        public MissionState State => _kind switch
        {
            MissionPrototypeKind.FuseSequence =>
                _mission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.BreakerSequence =>
                _breakerMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.CctvReboot =>
                _cctvMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.SampleSorting =>
                _sampleMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.BatteryTransport =>
                _batteryMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.PressureValves =>
                _pressureMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.SecurityCircuit =>
                _securityCircuitMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.AntennaAlignment =>
                _antennaMission?.State ?? MissionState.Assigned,
            MissionPrototypeKind.ServerLogRecovery =>
                _serverLogMission?.State ?? MissionState.Assigned,
            _ => MissionState.Assigned
        };
        public IReadOnlyList<int> RequiredOrder => _mission?.RequiredOrder ?? Array.Empty<int>();
        public int ProgressIndex => _kind switch
        {
            MissionPrototypeKind.FuseSequence => _mission?.ProgressIndex ?? 0,
            MissionPrototypeKind.BreakerSequence =>
                _breakerMission?.ProgressIndex ?? 0,
            MissionPrototypeKind.CctvReboot =>
                _cctvMission?.CompletedChannelCount ?? 0,
            MissionPrototypeKind.SampleSorting =>
                _sampleMission?.SortedSampleCount ?? 0,
            MissionPrototypeKind.BatteryTransport =>
                _batteryMission?.Phase == BatteryTransportPhase.Carrying
                    ? 1
                    : 0,
            MissionPrototypeKind.PressureValves =>
                Mathf.RoundToInt(PressureStabilityProgress * 100f),
            MissionPrototypeKind.SecurityCircuit =>
                _securityCircuitMission?.AlignedNodeCount ?? 0,
            MissionPrototypeKind.AntennaAlignment =>
                _antennaMission?.AlignedAxisCount ?? 0,
            MissionPrototypeKind.ServerLogRecovery =>
                _serverLogMission?.ProgressIndex ?? 0,
            _ => 0
        };
        public int FuseCount => _config != null ? _config.FuseCount : 0;
        public bool IsMissionActive => State == MissionState.InProgress;
        public bool IsRestored => _isRestored;
        public FuseMissionConfig Config => _config;
        public MissionPrototypeKind Kind => _kind;
        public GameObject ActiveInteractor => _activeInteractor;
        public bool IsMissionConfigured =>
            _config != null &&
            (_kind != MissionPrototypeKind.BatteryTransport ||
             (_batteryReceiver != null &&
              _batteryReceiver.SourceStation == this));
        public float BreakerMarkerNormalized =>
            _breakerMission?.GetMarkerNormalized(
                Time.unscaledTimeAsDouble) ?? 0f;
        public float BreakerTargetNormalized =>
            _breakerMission?.TargetNormalized ?? 0f;
        public float BreakerSuccessToleranceNormalized =>
            _breakerMission?.SuccessToleranceNormalized ?? 0f;
        public int CctvChannelCount => _cctvMission?.ChannelCount ?? FuseCount;
        public int SampleCount => _sampleMission?.SampleCount ?? FuseCount;
        public int SampleCategoryCount =>
            _sampleMission?.CategoryCount ??
            (_config != null ? _config.SampleCategoryCount : 0);
        public int SelectedSampleId =>
            _sampleMission?.SelectedSampleId ?? 0;
        public BatteryTransportPhase BatteryPhase =>
            _batteryMission?.Phase ?? BatteryTransportPhase.Secured;
        public bool IsBatteryCarrying =>
            _batteryMission?.Phase == BatteryTransportPhase.Carrying;
        public bool IsBatteryInsertionOpen => _isBatteryInsertionOpen;
        public Transform BatteryReceiverTransform =>
            _batteryReceiver != null ? _batteryReceiver.transform : null;
        public float PressureNormalized =>
            _pressureMission?.PressureNormalized ?? 0f;
        public float PressureTargetNormalized =>
            _pressureMission?.TargetPressureNormalized ??
            (_config != null ? _config.PressureTargetNormalized : 0f);
        public float PressureToleranceNormalized =>
            _pressureMission?.ToleranceNormalized ??
            (_config != null ? _config.PressureToleranceNormalized : 0f);
        public float PressureStabilityProgress =>
            _pressureMission?.GetStabilityProgress(
                Time.unscaledTimeAsDouble) ?? 0f;
        public int SecurityCircuitNodeCount =>
            _securityCircuitMission?.NodeCount ?? FuseCount;
        public int AntennaGridSize =>
            _antennaMission?.GridSize ?? FuseCount;
        public int AntennaAzimuth =>
            _antennaMission?.CurrentAzimuth ?? 0;
        public int AntennaFrequency =>
            _antennaMission?.CurrentFrequency ?? 0;
        public int AntennaTargetAzimuth =>
            _antennaMission?.TargetAzimuth ?? 0;
        public int AntennaTargetFrequency =>
            _antennaMission?.TargetFrequency ?? 0;
        public int ServerLogTokenCount =>
            _serverLogMission?.TokenCount ?? FuseCount;

        public void Configure(
            Renderer stationRenderer,
            Light indicatorLight,
            FuseMissionConfig config)
        {
            Configure(
                stationRenderer,
                indicatorLight,
                config,
                MissionPrototypeKind.FuseSequence);
        }

        public void Configure(
            Renderer stationRenderer,
            Light indicatorLight,
            FuseMissionConfig config,
            MissionPrototypeKind kind)
        {
            _stationRenderer = stationRenderer;
            _indicatorLight = indicatorLight;
            _config = config;
            _kind = kind;
        }

        public void ConfigureBatteryReceiver(
            BatteryReceiverPrototype receiver)
        {
            _batteryReceiver = receiver;
        }

        public void SetInteractionAuthority(
            Func<GameObject, bool> canInteract,
            Action<GameObject> requestInteraction)
        {
            _externalCanInteract = canInteract;
            _externalInteractionRequest = requestInteraction;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_externalInteractionRequest?.Target != authorityOwner)
            {
                return;
            }

            _externalCanInteract = null;
            _externalInteractionRequest = null;
        }

        private void Awake()
        {
            if (_stationRenderer is SpriteRenderer spriteRenderer)
            {
                _defaultStationColor = spriteRenderer.color;
            }

            if (_indicatorLight != null)
            {
                _defaultIndicatorColor = _indicatorLight.color;
                _defaultIndicatorIntensity = _indicatorLight.intensity;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally =
                (!_isRestored || _externalCanInteract != null) &&
                !IsMissionActive &&
                IsMissionConfigured &&
                isActiveAndEnabled;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (_externalInteractionRequest != null)
            {
                _externalInteractionRequest.Invoke(interactor);
                return;
            }

            BeginApprovedInteraction(interactor);
        }

        public void BeginApprovedInteraction(GameObject interactor)
        {
            BeginApprovedInteraction(
                interactor,
                UnityEngine.Random.Range(1, int.MaxValue));
        }

        public void BeginApprovedInteraction(
            GameObject interactor,
            int challengeSeed)
        {
            BeginApprovedInteraction(
                interactor,
                challengeSeed,
                Time.unscaledTimeAsDouble);
        }

        public void BeginApprovedInteraction(
            GameObject interactor,
            int challengeSeed,
            double breakerStartedAt)
        {
            if (_isRestored || IsMissionActive || _config == null ||
                !isActiveAndEnabled || !IsMissionConfigured)
            {
                return;
            }

            _activeInput = interactor.GetComponent<PlayerInputReader>();
            _activeMotor = interactor.GetComponent<PlayerMotor>();
            _activeAim = interactor.GetComponent<PlayerAimController>();
            if (_activeInput == null || _activeMotor == null || _activeAim == null)
            {
                Debug.LogError(
                    $"[Mission] {_kind} requires player input, motor and aim components.",
                    this);
                ClearActivePlayer();
                return;
            }

            _activeInteractor = interactor;
            CreateMissionInstance(challengeSeed, breakerStartedAt);
            _activeInput.CancelPressed += CancelMission;
            SetPlayerControlEnabled(false);
            MissionStarted?.Invoke(this);
            Debug.Log(
                $"[Mission] {_kind} started by {interactor.name}.",
                this);
        }

        /// <summary>
        /// 서버가 승인한 공용 복구 미션은 이 클라이언트가 같은 스테이션의
        /// 개인 미션을 이미 끝냈어도 다시 조작할 수 있다.
        /// </summary>
        public void BeginApprovedNetworkInteraction(
            GameObject interactor,
            int challengeSeed,
            double breakerStartedAt)
        {
            // 서버가 승인했다면 개인 완료 뒤 공용 복구를 시작하는 경우와
            // 승인 응답 유실 뒤 재시도하는 경우 모두 로컬 잠금을 해제한다.
            if (_isRestored)
            {
                _isRestored = false;
                RestoreDefaultVisuals();
            }

            BeginApprovedInteraction(
                interactor,
                challengeSeed,
                breakerStartedAt);
        }

        public void ApplyAuthoritativeCompletion()
        {
            if (_isRestored)
            {
                return;
            }

            CancelActiveMission();
            ReleasePlayer();
            ClearMissionInstances();

            _isRestored = true;
            ApplyRestoredVisuals();
        }

        /// <summary>
        /// 공용 복구 완료는 개인 스테이션 완료 여부와 별개다. 개인 완료가 없는
        /// 플레이어에게는 다시 기본 상태로 돌려 중복 복구 건을 막지 않는다.
        /// </summary>
        public void ApplyAuthoritativeRecoveryCompletion(
            bool preservePersonalCompletion)
        {
            CancelActiveMission();
            ReleasePlayer();
            ClearMissionInstances();
            _isRestored = preservePersonalCompletion;
            if (_isRestored)
            {
                ApplyRestoredVisuals();
            }
            else
            {
                RestoreDefaultVisuals();
            }
        }

        public void SubmitFuse(int fuseId)
        {
            if (_kind != MissionPrototypeKind.FuseSequence ||
                _mission == null)
            {
                return;
            }

            var expectedFuseId = ProgressIndex < RequiredOrder.Count
                ? RequiredOrder[ProgressIndex]
                : 0;
            var result = _mission.SubmitFuse(fuseId);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.FuseDrop,
                    fuseId,
                    ProgressIndex));
            switch (result)
            {
                case FuseMissionInputResult.Accepted:
                    ProgressChanged?.Invoke(this);
                    break;
                case FuseMissionInputResult.Failed:
                    HandleFailure(fuseId, expectedFuseId);
                    break;
                case FuseMissionInputResult.Completed:
                    HandleCompletion();
                    break;
            }
        }

        public void SubmitBreakerTiming()
        {
            if (_kind != MissionPrototypeKind.BreakerSequence ||
                _breakerMission == null)
            {
                return;
            }

            var marker = BreakerMarkerNormalized;
            var target = BreakerTargetNormalized;
            var result = _breakerMission.Submit(Time.unscaledTimeAsDouble);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.BreakerLever,
                    0,
                    0));
            HandleInputResult(
                result,
                Mathf.RoundToInt(marker * 100f),
                Mathf.RoundToInt(target * 100f));
        }

        public void SubmitCctvConnection(
            int sourceChannelId,
            int targetChannelId)
        {
            if (_kind != MissionPrototypeKind.CctvReboot ||
                _cctvMission == null)
            {
                return;
            }

            var result = _cctvMission.SubmitConnection(
                    sourceChannelId,
                    targetChannelId);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.CctvConnection,
                    sourceChannelId,
                    targetChannelId));
            HandleInputResult(
                result,
                sourceChannelId,
                targetChannelId);
        }

        public int GetCctvTargetChannelAtSlot(int slotIndex)
        {
            return _cctvMission?.GetTargetChannelAtSlot(slotIndex) ?? 0;
        }

        public bool IsCctvChannelConnected(int channelId)
        {
            return _cctvMission?.IsChannelConnected(channelId) ?? false;
        }

        public void SelectSample(int sampleId)
        {
            if (_kind == MissionPrototypeKind.SampleSorting &&
                _sampleMission != null &&
                _sampleMission.SelectSample(sampleId))
            {
                ProgressChanged?.Invoke(this);
            }
        }

        public void SubmitSampleCategory(int categoryId)
        {
            if (_kind != MissionPrototypeKind.SampleSorting ||
                _sampleMission == null)
            {
                return;
            }

            var sampleId = _sampleMission.SelectedSampleId;
            var expectedCategory =
                _sampleMission.GetRequiredCategory(sampleId);
            var result = _sampleMission.SubmitCategory(categoryId);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.SamplePlacement,
                    sampleId,
                    categoryId));
            HandleInputResult(
                result,
                categoryId,
                expectedCategory);
        }

        public int GetSampleRequiredCategory(int sampleId)
        {
            return _sampleMission?.GetRequiredCategory(sampleId) ?? 0;
        }

        public bool IsSampleSorted(int sampleId)
        {
            return _sampleMission?.IsSorted(sampleId) ?? false;
        }

        public void SubmitBatteryDetach()
        {
            if (_kind != MissionPrototypeKind.BatteryTransport ||
                _batteryMission == null)
            {
                return;
            }

            var result = _batteryMission.Detach();
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.BatteryDetach,
                    0,
                    0));
            if (result == FuseMissionInputResult.Accepted)
            {
                _isBatteryInsertionOpen = false;
                _activeMotor?.SetBatteryCarrying(true);
                SetPlayerControlEnabled(true);
                ProgressChanged?.Invoke(this);
            }
        }

        public bool CanPresentBatteryInsertion(GameObject interactor)
        {
            return _kind == MissionPrototypeKind.BatteryTransport &&
                   IsBatteryCarrying && !_isBatteryInsertionOpen &&
                   interactor != null && interactor == _activeInteractor;
        }

        public void BeginBatteryInsertion(GameObject interactor)
        {
            if (!CanPresentBatteryInsertion(interactor))
            {
                return;
            }

            _isBatteryInsertionOpen = true;
            SetPlayerControlEnabled(false);
            ProgressChanged?.Invoke(this);
        }

        public void SubmitBatteryInsert()
        {
            if (_kind != MissionPrototypeKind.BatteryTransport ||
                _batteryMission == null || !_isBatteryInsertionOpen)
            {
                return;
            }

            var result = _batteryMission.Insert();
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.BatteryInsert,
                    0,
                    0));
            HandleInputResult(result, 1, 1);
        }

        public void SubmitBatteryDrop()
        {
            if (_kind != MissionPrototypeKind.BatteryTransport ||
                _batteryMission == null)
            {
                return;
            }

            var result = _batteryMission.Drop();
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.BatteryDrop,
                    0,
                    0));
            HandleInputResult(result, 0, 1);
        }

        public float GetPressureValveOpening(int valveIndex)
        {
            return _pressureMission?.GetValveOpening(valveIndex) ?? 0f;
        }

        public void SubmitPressureValve(
            int valveIndex,
            float openingNormalized)
        {
            if (_kind != MissionPrototypeKind.PressureValves ||
                _pressureMission == null)
            {
                return;
            }

            var clampedOpening = Mathf.Clamp01(openingNormalized);
            var result = _pressureMission.SetValveOpening(
                valveIndex,
                clampedOpening,
                Time.unscaledTimeAsDouble);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.PressureValveAdjusted,
                    valveIndex,
                    QuantizeNormalized(clampedOpening)));
            HandleInputResult(
                result,
                QuantizeNormalized(PressureNormalized),
                QuantizeNormalized(PressureTargetNormalized));
        }

        public void SubmitPressureLock()
        {
            if (_kind != MissionPrototypeKind.PressureValves ||
                _pressureMission == null)
            {
                return;
            }

            var pressure = PressureNormalized;
            var result = _pressureMission.LockPressure(
                Time.unscaledTimeAsDouble);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.PressureLock,
                    0,
                    0));
            HandleInputResult(
                result,
                QuantizeNormalized(pressure),
                QuantizeNormalized(PressureTargetNormalized));
        }

        public int GetSecurityCircuitRotation(int nodeIndex)
        {
            return _securityCircuitMission?.GetCurrentRotation(nodeIndex) ??
                   0;
        }

        public int GetSecurityCircuitTargetRotation(int nodeIndex)
        {
            return _securityCircuitMission?.GetTargetRotation(nodeIndex) ??
                   0;
        }

        public void RotateSecurityCircuitNode(
            int nodeIndex,
            int direction)
        {
            if (_kind != MissionPrototypeKind.SecurityCircuit ||
                _securityCircuitMission == null)
            {
                return;
            }

            var result = _securityCircuitMission.RotateNode(
                nodeIndex,
                direction);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.SecurityCircuitRotate,
                    nodeIndex,
                    direction));
            HandleInputResult(
                result,
                _securityCircuitMission.GetCurrentRotation(nodeIndex),
                _securityCircuitMission.GetTargetRotation(nodeIndex));
        }

        public void TestSecurityCircuit()
        {
            if (_kind != MissionPrototypeKind.SecurityCircuit ||
                _securityCircuitMission == null)
            {
                return;
            }

            var alignedCount = _securityCircuitMission.AlignedNodeCount;
            var result = _securityCircuitMission.TestCircuit();
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.SecurityCircuitTest,
                    0,
                    0));
            HandleInputResult(
                result,
                alignedCount,
                _securityCircuitMission.NodeCount);
        }

        public void AdjustAntenna(int axis, int direction)
        {
            if (_kind != MissionPrototypeKind.AntennaAlignment ||
                _antennaMission == null)
            {
                return;
            }

            var result = _antennaMission.AdjustAxis(axis, direction);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.AntennaAdjust,
                    axis,
                    direction));
            HandleInputResult(result, direction, axis);
        }

        public void LockAntennaSignal()
        {
            if (_kind != MissionPrototypeKind.AntennaAlignment ||
                _antennaMission == null)
            {
                return;
            }

            var alignedCount = _antennaMission.AlignedAxisCount;
            var result = _antennaMission.LockSignal();
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.AntennaLock,
                    0,
                    0));
            HandleInputResult(result, alignedCount, 2);
        }

        public int GetServerLogRequiredToken(int index)
        {
            return _serverLogMission?.GetRequiredToken(index) ?? 0;
        }

        public void SubmitServerLogToken(int token)
        {
            if (_kind != MissionPrototypeKind.ServerLogRecovery ||
                _serverLogMission == null)
            {
                return;
            }

            var expectedToken =
                _serverLogMission.GetRequiredToken(
                    _serverLogMission.ProgressIndex);
            var result = _serverLogMission.SubmitToken(token);
            MissionInputSubmitted?.Invoke(
                this,
                new MissionInputCommand(
                    MissionInputAction.ServerLogKey,
                    token,
                    0));
            HandleInputResult(result, token, expectedToken);
        }

        public bool IsFuseInserted(int fuseId)
        {
            if (_mission == null)
            {
                return false;
            }

            for (var index = 0; index < ProgressIndex; index++)
            {
                if (RequiredOrder[index] == fuseId)
                {
                    return true;
                }
            }

            return false;
        }

        public void CancelMission()
        {
            if (!IsMissionActive)
            {
                return;
            }

            if (_kind == MissionPrototypeKind.BatteryTransport &&
                IsBatteryCarrying)
            {
                if (_isBatteryInsertionOpen)
                {
                    _isBatteryInsertionOpen = false;
                    SetPlayerControlEnabled(true);
                    ProgressChanged?.Invoke(this);
                }
                else
                {
                    SubmitBatteryDrop();
                }

                return;
            }

            CancelActiveMission();
            MissionCancelled?.Invoke(this);
            Debug.Log($"[Mission] {_kind} cancelled and reset.", this);
            ReleasePlayer();
            ClearMissionInstances();
        }

        public void ApplyAuthoritativeInterruption(bool batteryDropped)
        {
            if (!IsMissionActive)
            {
                return;
            }

            if (batteryDropped &&
                _kind == MissionPrototypeKind.BatteryTransport &&
                IsBatteryCarrying)
            {
                var result = _batteryMission.Drop();
                HandleInputResult(result, 0, 1);
                return;
            }

            CancelActiveMission();
            MissionCancelled?.Invoke(this);
            ReleasePlayer();
            ClearMissionInstances();
        }

        private void OnDisable()
        {
            if (IsMissionActive)
            {
                CancelActiveMission();
            }

            ReleasePlayer();
            if (!_isRestored)
            {
                ClearMissionInstances();
            }
        }

        private void CreateMissionInstance(
            int challengeSeed,
            double breakerStartedAt)
        {
            ClearMissionInstances();
            switch (_kind)
            {
                case MissionPrototypeKind.FuseSequence:
                    _mission = new FuseMissionInstance(
                        MissionChallengeGenerator.CreateShuffledOrder(
                            _config.FuseCount,
                            challengeSeed));
                    _mission.Begin();
                    break;
                case MissionPrototypeKind.BreakerSequence:
                    _breakerMission = new BreakerTimingMissionInstance(
                        _config.FuseCount,
                        _config.BreakerCycleSeconds,
                        _config.BreakerSuccessToleranceNormalized);
                    _breakerMission.Begin(breakerStartedAt);
                    break;
                case MissionPrototypeKind.CctvReboot:
                    _cctvMission = new CctvRebootMissionInstance(
                        MissionChallengeGenerator.CreateShuffledOrder(
                            _config.FuseCount,
                            challengeSeed));
                    _cctvMission.Begin();
                    break;
                case MissionPrototypeKind.SampleSorting:
                    _sampleMission = new SampleSortingMissionInstance(
                        MissionChallengeGenerator.CreateSampleCategories(
                            _config.FuseCount,
                            _config.SampleCategoryCount,
                            challengeSeed),
                        _config.SampleCategoryCount);
                    _sampleMission.Begin();
                    break;
                case MissionPrototypeKind.BatteryTransport:
                    _batteryMission =
                        new BatteryTransportMissionInstance();
                    _batteryMission.Begin();
                    break;
                case MissionPrototypeKind.PressureValves:
                    _pressureMission = new PressureValveMissionInstance(
                        _config.PressureTargetNormalized,
                        _config.PressureToleranceNormalized,
                        _config.PressureStabilizeSeconds);
                    _pressureMission.Begin();
                    break;
                case MissionPrototypeKind.SecurityCircuit:
                    _securityCircuitMission =
                        new SecurityCircuitMissionInstance(
                            _config.FuseCount,
                            challengeSeed);
                    _securityCircuitMission.Begin();
                    break;
                case MissionPrototypeKind.AntennaAlignment:
                    _antennaMission =
                        new AntennaAlignmentMissionInstance(
                            _config.FuseCount,
                            challengeSeed);
                    _antennaMission.Begin();
                    break;
                case MissionPrototypeKind.ServerLogRecovery:
                    _serverLogMission =
                        new ServerLogRecoveryMissionInstance(
                            MissionChallengeGenerator.CreateShuffledOrder(
                                _config.FuseCount,
                                challengeSeed));
                    _serverLogMission.Begin();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleInputResult(
            FuseMissionInputResult result,
            int submittedValue,
            int expectedValue)
        {
            switch (result)
            {
                case FuseMissionInputResult.Accepted:
                    ProgressChanged?.Invoke(this);
                    break;
                case FuseMissionInputResult.Failed:
                    HandleFailure(submittedValue, expectedValue);
                    break;
                case FuseMissionInputResult.Completed:
                    HandleCompletion();
                    break;
            }
        }

        private void HandleFailure(int submittedFuseId, int expectedFuseId)
        {
            var interactorName = _activeInteractor != null ? _activeInteractor.name : "Unknown";
            MissionFailed?.Invoke(this, submittedFuseId, expectedFuseId);
            Debug.Log(
                $"[Mission] {_kind} failed by {interactorName}: expected {expectedFuseId}, received {submittedFuseId}.",
                this);
            ReleasePlayer();
            ClearMissionInstances();
        }

        private void HandleCompletion()
        {
            _isRestored = true;
            ApplyRestoredVisuals();
            MissionCompleted?.Invoke(this);
            var interactorName = _activeInteractor != null ? _activeInteractor.name : "Unknown";
            Debug.Log(
                $"[Mission] {_kind} completed by {interactorName}.",
                this);
            ReleasePlayer();
            ClearMissionInstances();
        }

        private void ApplyRestoredVisuals()
        {
            if (_stationRenderer != null)
            {
                if (_stationRenderer is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.color = _restoredColor;
                }
                else
                {
                    _propertyBlock ??= new MaterialPropertyBlock();
                    _stationRenderer.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor("_BaseColor", _restoredColor);
                    _stationRenderer.SetPropertyBlock(_propertyBlock);
                }
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = _restoredColor;
                _indicatorLight.intensity = RestoredIndicatorIntensity;
            }
        }

        private void RestoreDefaultVisuals()
        {
            if (_stationRenderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = _defaultStationColor;
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = _defaultIndicatorColor;
                _indicatorLight.intensity = _defaultIndicatorIntensity;
            }
        }

        private void ReleasePlayer()
        {
            if (_activeInput != null)
            {
                _activeInput.CancelPressed -= CancelMission;
            }

            _activeMotor?.SetBatteryCarrying(false);
            SetPlayerControlEnabled(true);
            ClearActivePlayer();
        }

        private void SetPlayerControlEnabled(bool isEnabled)
        {
            _activeMotor?.SetMovementEnabled(isEnabled);
            _activeAim?.SetAimingEnabled(isEnabled);
        }

        private void ClearActivePlayer()
        {
            _activeInteractor = null;
            _activeInput = null;
            _activeMotor = null;
            _activeAim = null;
        }

        private void CancelActiveMission()
        {
            _mission?.Cancel();
            _breakerMission?.Cancel();
            _cctvMission?.Cancel();
            _sampleMission?.Cancel();
            _batteryMission?.Cancel();
            _pressureMission?.Cancel();
            _securityCircuitMission?.Cancel();
            _antennaMission?.Cancel();
            _serverLogMission?.Cancel();
        }

        private void ClearMissionInstances()
        {
            _mission = null;
            _breakerMission = null;
            _cctvMission = null;
            _sampleMission = null;
            _batteryMission = null;
            _pressureMission = null;
            _securityCircuitMission = null;
            _antennaMission = null;
            _serverLogMission = null;
            _isBatteryInsertionOpen = false;
        }

        private static int QuantizeNormalized(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 1000f);
        }

    }
}
