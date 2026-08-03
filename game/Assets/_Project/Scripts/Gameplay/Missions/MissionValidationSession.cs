using System;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 서버가 클라이언트와 같은 시드로 퍼즐을 만들고 조작 입력만 검증한다.
    /// UI나 NetworkBehaviour에 의존하지 않는다.
    /// </summary>
    public sealed class MissionValidationSession
    {
        private readonly MissionPrototypeKind _kind;
        private readonly FuseMissionInstance _fuse;
        private readonly BreakerTimingMissionInstance _breaker;
        private readonly CctvRebootMissionInstance _cctv;
        private readonly SampleSortingMissionInstance _sample;
        private readonly BatteryTransportMissionInstance _battery;
        private readonly PressureValveMissionInstance _pressure;
        private readonly SecurityCircuitMissionInstance _securityCircuit;
        private readonly AntennaAlignmentMissionInstance _antenna;
        private readonly ServerLogRecoveryMissionInstance _serverLog;

        public MissionValidationSession(
            MissionPrototypeKind kind,
            int itemCount,
            int sampleCategoryCount,
            float breakerCycleSeconds,
            float breakerSuccessToleranceNormalized,
            float pressureTargetNormalized,
            float pressureToleranceNormalized,
            float pressureStabilizeSeconds,
            int seed,
            double startedAt)
        {
            _kind = kind;
            switch (kind)
            {
                case MissionPrototypeKind.FuseSequence:
                    _fuse = new FuseMissionInstance(
                        MissionChallengeGenerator.CreateShuffledOrder(
                            itemCount,
                            seed));
                    _fuse.Begin();
                    break;
                case MissionPrototypeKind.BreakerSequence:
                    _breaker = new BreakerTimingMissionInstance(
                        itemCount,
                        breakerCycleSeconds,
                        breakerSuccessToleranceNormalized);
                    _breaker.Begin(startedAt);
                    break;
                case MissionPrototypeKind.CctvReboot:
                    _cctv = new CctvRebootMissionInstance(
                        MissionChallengeGenerator.CreateShuffledOrder(
                            itemCount,
                            seed));
                    _cctv.Begin();
                    break;
                case MissionPrototypeKind.SampleSorting:
                    _sample = new SampleSortingMissionInstance(
                        MissionChallengeGenerator.CreateSampleCategories(
                            itemCount,
                            sampleCategoryCount,
                            seed),
                        sampleCategoryCount);
                    _sample.Begin();
                    break;
                case MissionPrototypeKind.BatteryTransport:
                    _battery = new BatteryTransportMissionInstance();
                    _battery.Begin();
                    break;
                case MissionPrototypeKind.PressureValves:
                    _pressure = new PressureValveMissionInstance(
                        pressureTargetNormalized,
                        pressureToleranceNormalized,
                        pressureStabilizeSeconds);
                    _pressure.Begin();
                    break;
                case MissionPrototypeKind.SecurityCircuit:
                    _securityCircuit = new SecurityCircuitMissionInstance(
                        itemCount,
                        seed);
                    _securityCircuit.Begin();
                    break;
                case MissionPrototypeKind.AntennaAlignment:
                    _antenna = new AntennaAlignmentMissionInstance(
                        itemCount,
                        seed);
                    _antenna.Begin();
                    break;
                case MissionPrototypeKind.ServerLogRecovery:
                    _serverLog = new ServerLogRecoveryMissionInstance(
                        MissionChallengeGenerator.CreateShuffledOrder(
                            itemCount,
                            seed));
                    _serverLog.Begin();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public bool IsCarryingBattery =>
            _kind == MissionPrototypeKind.BatteryTransport &&
            _battery?.Phase == BatteryTransportPhase.Carrying;

        public FuseMissionInputResult Validate(
            MissionInputCommand command,
            double currentTime)
        {
            return _kind switch
            {
                MissionPrototypeKind.FuseSequence
                    when command.Action == MissionInputAction.FuseDrop =>
                    _fuse.SubmitFuse(command.PrimaryValue),
                MissionPrototypeKind.BreakerSequence
                    when command.Action == MissionInputAction.BreakerLever =>
                    _breaker.Submit(currentTime),
                MissionPrototypeKind.CctvReboot
                    when command.Action == MissionInputAction.CctvConnection =>
                    _cctv.SubmitConnection(
                        command.PrimaryValue,
                        command.SecondaryValue),
                MissionPrototypeKind.SampleSorting
                    when command.Action == MissionInputAction.SamplePlacement =>
                    ValidateSamplePlacement(command),
                MissionPrototypeKind.BatteryTransport
                    when command.Action == MissionInputAction.BatteryDetach =>
                    _battery.Detach(),
                MissionPrototypeKind.BatteryTransport
                    when command.Action == MissionInputAction.BatteryInsert =>
                    _battery.Insert(),
                MissionPrototypeKind.BatteryTransport
                    when command.Action == MissionInputAction.BatteryDrop =>
                    _battery.Drop(),
                MissionPrototypeKind.PressureValves
                    when command.Action ==
                         MissionInputAction.PressureValveAdjusted =>
                    _pressure.SetValveOpening(
                        command.PrimaryValue,
                        DecodeNormalized(command.SecondaryValue),
                        currentTime),
                MissionPrototypeKind.PressureValves
                    when command.Action == MissionInputAction.PressureLock =>
                    _pressure.LockPressure(currentTime),
                MissionPrototypeKind.SecurityCircuit
                    when command.Action ==
                         MissionInputAction.SecurityCircuitRotate =>
                    _securityCircuit.RotateNode(
                        command.PrimaryValue,
                        command.SecondaryValue),
                MissionPrototypeKind.SecurityCircuit
                    when command.Action ==
                         MissionInputAction.SecurityCircuitTest =>
                    _securityCircuit.TestCircuit(),
                MissionPrototypeKind.AntennaAlignment
                    when command.Action == MissionInputAction.AntennaAdjust =>
                    _antenna.AdjustAxis(
                        command.PrimaryValue,
                        command.SecondaryValue),
                MissionPrototypeKind.AntennaAlignment
                    when command.Action == MissionInputAction.AntennaLock =>
                    _antenna.LockSignal(),
                MissionPrototypeKind.ServerLogRecovery
                    when command.Action == MissionInputAction.ServerLogKey =>
                    _serverLog.SubmitToken(command.PrimaryValue),
                _ => FuseMissionInputResult.Ignored
            };
        }

        private static float DecodeNormalized(int quantizedValue)
        {
            return quantizedValue / 1000f;
        }

        private FuseMissionInputResult ValidateSamplePlacement(
            MissionInputCommand command)
        {
            return _sample.SelectSample(command.PrimaryValue)
                ? _sample.SubmitCategory(command.SecondaryValue)
                : FuseMissionInputResult.Ignored;
        }
    }
}
