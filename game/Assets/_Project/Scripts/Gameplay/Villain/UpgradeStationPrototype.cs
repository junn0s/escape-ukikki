using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 빌런 전용 강화 스테이션이다. 축마다 서로 다른 직접 조작 퍼즐을 제공하며
    /// 중단 시 진행 상황을 즉시 초기화한다.
    /// </summary>
    public sealed class UpgradeStationPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer _stationRenderer;
        [SerializeField] private UpgradeBalanceConfig _config;
        [SerializeField] private UpgradeAxis _axis;
        [SerializeField] private string _roomId;
        [SerializeField]
        private Color _idleColor = new(0.65f, 0.2f, 0.85f, 1f);
        [SerializeField]
        private Color _channelingColor = new(1f, 0.45f, 0.1f, 1f);
        [SerializeField]
        private Color _maxedColor = new(0.35f, 0.35f, 0.4f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private GameObject _activeInteractor;
        private PlayerInputReader _activeInput;
        private PlayerMotor _activeMotor;
        private PlayerAimController _activeAim;
        private VillainUpgradeMissionSession _mission;
        private bool _isChanneling;
        private bool _isAwaitingServerCompletion;
        private bool _isAxisMaxed;
        private bool _isPubliclyOccupied;
        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;

        public static UpgradeStationPrototype ActiveLocalStation
        {
            get;
            private set;
        }

        public event Action<UpgradeStationPrototype> ChannelStarted;
        public event Action<UpgradeStationPrototype> ProgressChanged;
        public event Action<UpgradeStationPrototype> ChannelCancelled;
        public event Action<UpgradeStationPrototype> ChannelCompleted;
        public event Action<
            UpgradeStationPrototype,
            VillainUpgradeMissionInputCommand> ChallengeInputSubmitted;

        public string Prompt => _axis switch
        {
            UpgradeAxis.Scent => "후각 혼합비 조정하기",
            UpgradeAxis.Population => "격리 회로 우회하기",
            UpgradeAxis.Toxicity => "독성 약품 주입하기",
            _ => "강화하기"
        };

        public Transform InteractionTransform => transform;
        public UpgradeAxis Axis => _axis;
        public string RoomId => _roomId;
        public UpgradeBalanceConfig Config => _config;
        public bool IsChanneling => _isChanneling;
        public bool IsAwaitingServerCompletion =>
            _isAwaitingServerCompletion;
        public bool IsAxisMaxed => _isAxisMaxed;
        public float RequiredSeconds =>
            _config != null
                ? _config.GetUpgradeMissionSeconds(_axis)
                : 0f;
        public MissionState ChallengeState =>
            _mission?.State ?? MissionState.Assigned;

        public float NormalizedProgress => _axis switch
        {
            UpgradeAxis.Scent => ScentStabilityProgress,
            UpgradeAxis.Population when PopulationNodeCount > 0 =>
                (float)PopulationAlignedNodeCount / PopulationNodeCount,
            UpgradeAxis.Toxicity when ToxicityStepCount > 0 =>
                (float)ToxicityProgressIndex / ToxicityStepCount,
            _ => 0f
        };

        public float ScentTargetNormalized =>
            _mission?.ScentTargetNormalized ?? 0f;
        public float ScentToleranceNormalized =>
            _mission?.ScentToleranceNormalized ?? 0f;
        public float ScentPressureNormalized =>
            _mission?.ScentPressureNormalized ?? 0f;
        public float ScentStabilityProgress =>
            _mission?.GetScentStabilityProgress(
                Time.unscaledTimeAsDouble) ?? 0f;
        public int PopulationNodeCount =>
            _mission?.PopulationNodeCount ?? 0;
        public int PopulationAlignedNodeCount =>
            _mission?.PopulationAlignedNodeCount ?? 0;
        public int ToxicityProgressIndex =>
            _mission?.ToxicityProgressIndex ?? 0;
        public int ToxicityStepCount =>
            _mission?.ToxicityStepCount ?? 0;
        public float ToxicityMarkerNormalized =>
            _mission?.GetToxicityMarkerNormalized(
                Time.unscaledTimeAsDouble) ?? 0f;
        public float ToxicityTargetNormalized =>
            _mission?.ToxicityTargetNormalized ?? 0f;
        public float ToxicitySuccessToleranceNormalized =>
            _mission?.ToxicitySuccessToleranceNormalized ?? 0f;

        public void Configure(
            Renderer stationRenderer,
            UpgradeBalanceConfig config,
            UpgradeAxis axis,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _config = config;
            _axis = axis;
            _roomId = roomId;
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

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally =
                !_isAxisMaxed &&
                !_isChanneling &&
                _config != null &&
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

            BeginApprovedInteraction(
                interactor,
                UnityEngine.Random.Range(1, int.MaxValue),
                Time.unscaledTimeAsDouble);
        }

        public void BeginApprovedInteraction(
            GameObject interactor,
            int challengeSeed,
            double challengeStartedAt)
        {
            if (_isAxisMaxed || _isChanneling || _config == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            _activeInput = interactor.GetComponent<PlayerInputReader>();
            _activeMotor = interactor.GetComponent<PlayerMotor>();
            _activeAim = interactor.GetComponent<PlayerAimController>();
            if (_activeInput == null || _activeMotor == null ||
                _activeAim == null)
            {
                Debug.LogError(
                    "[Upgrade] Station requires player input, motor and aim components.",
                    this);
                ClearActivePlayer();
                return;
            }

            _mission = CreateMission(
                challengeSeed,
                challengeStartedAt,
                useServerTolerance: false);
            _activeInteractor = interactor;
            _isChanneling = true;
            _isAwaitingServerCompletion = false;
            ActiveLocalStation = this;
            _activeInput.CancelPressed += CancelChannel;
            SetPlayerControlEnabled(false);
            ApplyVisuals();
            ChannelStarted?.Invoke(this);
            Debug.Log(
                $"[Upgrade] {_axis} interactive challenge started by {interactor.name}.",
                this);
        }

        public VillainUpgradeMissionSession CreateServerMission(
            int challengeSeed,
            double challengeStartedAt)
        {
            return CreateMission(
                challengeSeed,
                challengeStartedAt,
                useServerTolerance: true);
        }

        public float GetScentValveOpening(int valveIndex)
        {
            return _mission?.GetScentValveOpening(valveIndex) ?? 0f;
        }

        public int GetPopulationCurrentRotation(int nodeIndex)
        {
            return _mission?.GetPopulationCurrentRotation(nodeIndex) ?? 0;
        }

        public int GetPopulationTargetRotation(int nodeIndex)
        {
            return _mission?.GetPopulationTargetRotation(nodeIndex) ?? 0;
        }

        public void SetScentValveOpening(
            int valveIndex,
            float openingNormalized)
        {
            SubmitChallengeInput(
                new VillainUpgradeMissionInputCommand(
                    VillainUpgradeInputAction.ScentValveAdjusted,
                    valveIndex,
                    Mathf.RoundToInt(
                        Mathf.Clamp01(openingNormalized) * 1000f)));
        }

        public void SealScentMixture()
        {
            SubmitChallengeInput(
                new VillainUpgradeMissionInputCommand(
                    VillainUpgradeInputAction.ScentMixtureSealed,
                    0,
                    0));
        }

        public void RotatePopulationCircuit(int nodeIndex)
        {
            SubmitChallengeInput(
                new VillainUpgradeMissionInputCommand(
                    VillainUpgradeInputAction.PopulationCircuitRotated,
                    nodeIndex,
                    1));
        }

        public void TestPopulationCircuit()
        {
            SubmitChallengeInput(
                new VillainUpgradeMissionInputCommand(
                    VillainUpgradeInputAction.PopulationCircuitTested,
                    0,
                    0));
        }

        public void InjectToxicityDose()
        {
            SubmitChallengeInput(
                new VillainUpgradeMissionInputCommand(
                    VillainUpgradeInputAction.ToxicityDoseInjected,
                    0,
                    0));
        }

        public void CancelChannel()
        {
            if (!_isChanneling || _isAwaitingServerCompletion)
            {
                return;
            }

            _mission?.Cancel();
            ResetChallengeAndReleasePlayer();
            ApplyVisuals();
            ChannelCancelled?.Invoke(this);
            Debug.Log(
                $"[Upgrade] {_axis} challenge cancelled and reset.",
                this);
        }

        public void ApplyAxisMaxed()
        {
            if (_isChanneling)
            {
                ResetChallengeAndReleasePlayer();
            }

            _isAxisMaxed = true;
            ApplyVisuals();
        }

        public void ApplyAuthoritativeCompletion()
        {
            ResetChallengeAndReleasePlayer();
            ApplyVisuals();
        }

        public void ApplyAuthoritativeFailure()
        {
            ResetChallengeAndReleasePlayer();
            ApplyVisuals();
        }

        /// <summary>다른 플레이어에게 퍼즐 내용 없이 정상 작업 중 외형만 보인다.</summary>
        public void SetPublicActivity(bool isActive)
        {
            _isPubliclyOccupied = isActive;
            ApplyVisuals();
        }

        public void ApplyAuthoritativeToxicityProgress(
            int progressIndex,
            double localStepStartedAt)
        {
            if (!_isChanneling || _mission == null ||
                !_mission.ApplyAuthoritativeToxicityProgress(
                    progressIndex,
                    localStepStartedAt))
            {
                return;
            }

            _isAwaitingServerCompletion = false;
            ProgressChanged?.Invoke(this);
        }

        private void Update()
        {
            if (!_isChanneling)
            {
                return;
            }

            if (_activeInteractor == null)
            {
                ResetChallengeAndReleasePlayer();
                ApplyVisuals();
                return;
            }

            ProgressChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            ResetChallengeAndReleasePlayer();
        }

        private VillainUpgradeMissionSession CreateMission(
            int challengeSeed,
            double challengeStartedAt,
            bool useServerTolerance)
        {
            return new VillainUpgradeMissionSession(
                _axis,
                _config.ChallengeItemCount,
                _config.ScentTargetMinimumNormalized,
                _config.ScentTargetMaximumNormalized,
                _config.ScentToleranceNormalized,
                useServerTolerance
                    ? _config.ScentServerStabilizeSeconds
                    : _config.ScentStabilizeSeconds,
                _config.ToxicityCycleSeconds,
                useServerTolerance
                    ? _config.ToxicityServerToleranceNormalized
                    : _config.ToxicitySuccessToleranceNormalized,
                challengeSeed,
                challengeStartedAt);
        }

        private void SubmitChallengeInput(
            VillainUpgradeMissionInputCommand command)
        {
            if (!_isChanneling || _isAwaitingServerCompletion ||
                _mission == null)
            {
                return;
            }

            var result = _mission.Validate(
                command,
                Time.unscaledTimeAsDouble);
            if (result == FuseMissionInputResult.Ignored)
            {
                return;
            }

            ChallengeInputSubmitted?.Invoke(this, command);
            ProgressChanged?.Invoke(this);
            if (command.Action ==
                VillainUpgradeInputAction.ToxicityDoseInjected)
            {
                _isAwaitingServerCompletion = true;
                return;
            }

            if (result != FuseMissionInputResult.Failed &&
                result != FuseMissionInputResult.Completed)
            {
                return;
            }

            if (_externalInteractionRequest != null)
            {
                _isAwaitingServerCompletion = true;
                return;
            }

            if (result == FuseMissionInputResult.Completed)
            {
                ChannelCompleted?.Invoke(this);
            }

            ResetChallengeAndReleasePlayer();
            ApplyVisuals();
        }

        private void ResetChallengeAndReleasePlayer()
        {
            _isChanneling = false;
            _isAwaitingServerCompletion = false;
            _mission = null;
            ReleasePlayer();
        }

        private void ApplyVisuals()
        {
            if (_stationRenderer == null)
            {
                return;
            }

            var color = _isAxisMaxed
                ? _maxedColor
                : _isChanneling || _isPubliclyOccupied
                    ? _channelingColor
                    : _idleColor;
            if (_stationRenderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = color;
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _stationRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _stationRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ReleasePlayer()
        {
            if (_activeInput != null)
            {
                _activeInput.CancelPressed -= CancelChannel;
            }

            if (ActiveLocalStation == this)
            {
                ActiveLocalStation = null;
            }

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
    }
}
