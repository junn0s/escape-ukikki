using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;

namespace MonkeyLab.Gameplay.Villain
{
    public enum VillainUpgradeInputAction : byte
    {
        ScentValveAdjusted = 0,
        ScentMixtureSealed = 1,
        PopulationCircuitRotated = 2,
        PopulationCircuitTested = 3,
        ToxicityDoseInjected = 4
    }

    /// <summary>강화 결과가 아니라 빌런이 수행한 조작 한 번을 표현한다.</summary>
    public readonly struct VillainUpgradeMissionInputCommand
    {
        public VillainUpgradeMissionInputCommand(
            VillainUpgradeInputAction action,
            int primaryValue,
            int secondaryValue)
        {
            Action = action;
            PrimaryValue = primaryValue;
            SecondaryValue = secondaryValue;
        }

        public VillainUpgradeInputAction Action { get; }
        public int PrimaryValue { get; }
        public int SecondaryValue { get; }
    }

    /// <summary>
    /// 빌런 강화 3종의 순수 규칙 세션이다. 클라이언트와 서버가 같은 시드로
    /// 만들고 서버가 개별 입력을 다시 적용한다.
    /// </summary>
    public sealed class VillainUpgradeMissionSession
    {
        private readonly UpgradeAxis _axis;
        private readonly PressureValveMissionInstance _scentMission;
        private readonly SecurityCircuitMissionInstance _populationMission;
        private readonly BreakerTimingMissionInstance _toxicityMission;

        public VillainUpgradeMissionSession(
            UpgradeAxis axis,
            int itemCount,
            float scentTargetMinimumNormalized,
            float scentTargetMaximumNormalized,
            float scentToleranceNormalized,
            float scentStabilizeSeconds,
            float toxicityCycleSeconds,
            float toxicitySuccessToleranceNormalized,
            int seed,
            double startedAt)
        {
            _axis = axis;
            switch (axis)
            {
                case UpgradeAxis.Scent:
                {
                    var random = new Random(seed);
                    var target =
                        scentTargetMinimumNormalized +
                        (scentTargetMaximumNormalized -
                         scentTargetMinimumNormalized) *
                        (float)random.NextDouble();
                    _scentMission = new PressureValveMissionInstance(
                        target,
                        scentToleranceNormalized,
                        scentStabilizeSeconds);
                    _scentMission.Begin();
                    break;
                }
                case UpgradeAxis.Population:
                    _populationMission =
                        new SecurityCircuitMissionInstance(itemCount, seed);
                    _populationMission.Begin();
                    break;
                case UpgradeAxis.Toxicity:
                    _toxicityMission = new BreakerTimingMissionInstance(
                        itemCount,
                        toxicityCycleSeconds,
                        toxicitySuccessToleranceNormalized);
                    _toxicityMission.Begin(startedAt);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public UpgradeAxis Axis => _axis;
        public MissionState State => _axis switch
        {
            UpgradeAxis.Scent => _scentMission.State,
            UpgradeAxis.Population => _populationMission.State,
            UpgradeAxis.Toxicity => _toxicityMission.State,
            _ => MissionState.Assigned
        };

        public float ScentTargetNormalized =>
            _scentMission?.TargetPressureNormalized ?? 0f;
        public float ScentToleranceNormalized =>
            _scentMission?.ToleranceNormalized ?? 0f;
        public float ScentPressureNormalized =>
            _scentMission?.PressureNormalized ?? 0f;
        public int PopulationNodeCount =>
            _populationMission?.NodeCount ?? 0;
        public int PopulationAlignedNodeCount =>
            _populationMission?.AlignedNodeCount ?? 0;
        public int ToxicityProgressIndex =>
            _toxicityMission?.ProgressIndex ?? 0;
        public int ToxicityStepCount =>
            _toxicityMission?.StepCount ?? 0;
        public float ToxicityTargetNormalized =>
            _toxicityMission?.TargetNormalized ?? 0f;
        public float ToxicitySuccessToleranceNormalized =>
            _toxicityMission?.SuccessToleranceNormalized ?? 0f;
        public double ToxicityStepStartedAt =>
            _toxicityMission?.StepStartedAt ?? 0d;

        public float GetScentValveOpening(int valveIndex)
        {
            return _scentMission?.GetValveOpening(valveIndex) ?? 0f;
        }

        public float GetScentStabilityProgress(double currentTime)
        {
            return _scentMission?.GetStabilityProgress(currentTime) ?? 0f;
        }

        public int GetPopulationCurrentRotation(int nodeIndex)
        {
            return _populationMission?.GetCurrentRotation(nodeIndex) ?? 0;
        }

        public int GetPopulationTargetRotation(int nodeIndex)
        {
            return _populationMission?.GetTargetRotation(nodeIndex) ?? 0;
        }

        public float GetToxicityMarkerNormalized(double currentTime)
        {
            return _toxicityMission?.GetMarkerNormalized(currentTime) ?? 0f;
        }

        public bool ApplyAuthoritativeToxicityProgress(
            int progressIndex,
            double stepStartedAt)
        {
            return _axis == UpgradeAxis.Toxicity &&
                   _toxicityMission != null &&
                   _toxicityMission.ApplyAuthoritativeProgress(
                       progressIndex,
                       stepStartedAt);
        }

        public FuseMissionInputResult Validate(
            VillainUpgradeMissionInputCommand command,
            double currentTime)
        {
            return _axis switch
            {
                UpgradeAxis.Scent
                    when command.Action ==
                         VillainUpgradeInputAction.ScentValveAdjusted =>
                    _scentMission.SetValveOpening(
                        command.PrimaryValue,
                        DecodeNormalized(command.SecondaryValue),
                        currentTime),
                UpgradeAxis.Scent
                    when command.Action ==
                         VillainUpgradeInputAction.ScentMixtureSealed =>
                    _scentMission.LockPressure(currentTime),
                UpgradeAxis.Population
                    when command.Action ==
                         VillainUpgradeInputAction.PopulationCircuitRotated =>
                    _populationMission.RotateNode(
                        command.PrimaryValue,
                        command.SecondaryValue),
                UpgradeAxis.Population
                    when command.Action ==
                         VillainUpgradeInputAction.PopulationCircuitTested =>
                    _populationMission.TestCircuit(),
                UpgradeAxis.Toxicity
                    when command.Action ==
                         VillainUpgradeInputAction.ToxicityDoseInjected =>
                    _toxicityMission.Submit(currentTime),
                _ => FuseMissionInputResult.Ignored
            };
        }

        public void Cancel()
        {
            switch (_axis)
            {
                case UpgradeAxis.Scent:
                    _scentMission?.Cancel();
                    break;
                case UpgradeAxis.Population:
                    _populationMission?.Cancel();
                    break;
                case UpgradeAxis.Toxicity:
                    _toxicityMission?.Cancel();
                    break;
            }
        }

        private static float DecodeNormalized(int quantizedValue)
        {
            return quantizedValue / 1000f;
        }
    }
}
