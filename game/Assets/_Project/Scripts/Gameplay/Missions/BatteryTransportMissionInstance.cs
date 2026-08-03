using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    public enum BatteryTransportPhase : byte
    {
        Secured = 0,
        Carrying = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// 배터리를 거치대에서 분리하고 다른 단말기까지 운반한 뒤 장착하는 미션 상태다.
    /// 월드 위치와 소유권은 네트워크 권위 계층이 검증한다.
    /// </summary>
    public sealed class BatteryTransportMissionInstance
    {
        public MissionState State { get; private set; } =
            MissionState.Assigned;
        public BatteryTransportPhase Phase { get; private set; } =
            BatteryTransportPhase.Secured;

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
                Phase = BatteryTransportPhase.Secured;
            }
        }

        public FuseMissionInputResult Detach()
        {
            if (State != MissionState.InProgress ||
                Phase != BatteryTransportPhase.Secured)
            {
                return FuseMissionInputResult.Ignored;
            }

            Phase = BatteryTransportPhase.Carrying;
            return FuseMissionInputResult.Accepted;
        }

        public FuseMissionInputResult Insert()
        {
            if (State != MissionState.InProgress ||
                Phase != BatteryTransportPhase.Carrying)
            {
                return FuseMissionInputResult.Ignored;
            }

            Phase = BatteryTransportPhase.Completed;
            State = MissionState.Completed;
            return FuseMissionInputResult.Completed;
        }

        public FuseMissionInputResult Drop()
        {
            if (State != MissionState.InProgress ||
                Phase != BatteryTransportPhase.Carrying)
            {
                return FuseMissionInputResult.Ignored;
            }

            Phase = BatteryTransportPhase.Failed;
            State = MissionState.Failed;
            return FuseMissionInputResult.Failed;
        }

        public void Cancel()
        {
            if (State != MissionState.InProgress)
            {
                return;
            }

            Phase = BatteryTransportPhase.Cancelled;
            State = MissionState.Cancelled;
        }
    }
}
