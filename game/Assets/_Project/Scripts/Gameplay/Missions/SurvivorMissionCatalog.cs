using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Missions
{
    public readonly struct SurvivorMissionDefinition
    {
        public SurvivorMissionDefinition(
            SurvivorMissionKind kind,
            string roomId,
            string roomDisplayName,
            string displayName,
            MissionDifficulty difficulty,
            MissionPrototypeKind prototypeKind)
        {
            Kind = kind;
            RoomId = roomId;
            RoomDisplayName = roomDisplayName;
            DisplayName = displayName;
            Difficulty = difficulty;
            PrototypeKind = prototypeKind;
        }

        public SurvivorMissionKind Kind { get; }
        public ulong MissionId => SurvivorMissionCatalog.GetMissionId(Kind);
        public string RoomId { get; }
        public string RoomDisplayName { get; }
        public string DisplayName { get; }
        public MissionDifficulty Difficulty { get; }
        public MissionPrototypeKind PrototypeKind { get; }
    }

    /// <summary>
    /// 생존자 미션 22종의 단일 카탈로그다. ID는 NetworkObjectId와 충돌하지 않는
    /// 고정 영역을 사용해 재접속·HUD·서버 배정에서 같은 값을 공유한다.
    /// </summary>
    public static class SurvivorMissionCatalog
    {
        private const ulong MissionIdPrefix = 0x4D4C530000000000UL;

        private static readonly SurvivorMissionDefinition[] Definitions =
        {
            Define(SurvivorMissionKind.VaccineDataDownload, "VaccineA", "백신실 A", "백신 데이터 다운로드", MissionDifficulty.Easy, MissionPrototypeKind.ServerLogRecovery),
            Define(SurvivorMissionKind.ContaminatedSyringeDisposal, "VaccineA", "백신실 A", "오염된 주사기 폐기", MissionDifficulty.Easy, MissionPrototypeKind.SampleSorting),
            Define(SurvivorMissionKind.FreezerTemperatureAdjustment, "VaccineB", "백신실 B", "냉동고 온도 조절", MissionDifficulty.Standard, MissionPrototypeKind.PressureValves),
            Define(SurvivorMissionKind.VaccineSampleScan, "VaccineB", "백신실 B", "백신 샘플 스캔", MissionDifficulty.Easy, MissionPrototypeKind.SampleSorting),
            Define(SurvivorMissionKind.SlideGlassCleaning, "LabA", "실험실 A", "슬라이드 글라스 닦기", MissionDifficulty.Easy, MissionPrototypeKind.CctvReboot),
            Define(SurvivorMissionKind.ReagentSorting, "LabA", "실험실 A", "시약병 분류", MissionDifficulty.Standard, MissionPrototypeKind.SampleSorting),
            Define(SurvivorMissionKind.MicroscopeFocus, "LabB", "실험실 B", "현미경 렌즈 초점", MissionDifficulty.Standard, MissionPrototypeKind.AntennaAlignment),
            Define(SurvivorMissionKind.FlaskFill, "LabB", "실험실 B", "플라스크 용액 채우기", MissionDifficulty.Hard, MissionPrototypeKind.BreakerSequence),
            Define(SurvivorMissionKind.RatCageLock, "LabB", "실험실 B", "실험용 쥐 케이지 잠그기", MissionDifficulty.Easy, MissionPrototypeKind.SecurityCircuit),
            Define(SurvivorMissionKind.QuarantineAWireConnect, "QuarantineA", "격리실 A", "배선 복구", MissionDifficulty.Standard, MissionPrototypeKind.SecurityCircuit),
            Define(SurvivorMissionKind.AirlockPressureAdjustment, "QuarantineA", "격리실 A", "에어록 압력 조절", MissionDifficulty.Easy, MissionPrototypeKind.PressureValves),
            Define(SurvivorMissionKind.HazmatDecontamination, "QuarantineA", "격리실 A", "방호복 소독", MissionDifficulty.Easy, MissionPrototypeKind.ServerLogRecovery),
            Define(SurvivorMissionKind.QuarantineBWireConnect, "QuarantineB", "격리실 B", "배선 복구", MissionDifficulty.Standard, MissionPrototypeKind.SecurityCircuit),
            Define(SurvivorMissionKind.AirFilterReplacement, "QuarantineB", "격리실 B", "공기 필터 교체", MissionDifficulty.Easy, MissionPrototypeKind.FuseSequence),
            Define(SurvivorMissionKind.IvDripAdjustment, "Ward", "입원실", "수액 속도 조절", MissionDifficulty.Standard, MissionPrototypeKind.BreakerSequence),
            Define(SurvivorMissionKind.PatientVitalsEntry, "Ward", "입원실", "환자 바이탈 기록", MissionDifficulty.Standard, MissionPrototypeKind.ServerLogRecovery),
            Define(SurvivorMissionKind.StorageValveLock, "Storage", "액체 보관실", "밸브 잠그기", MissionDifficulty.Easy, MissionPrototypeKind.PressureValves),
            Define(SurvivorMissionKind.WasteCompactor, "Storage", "액체 보관실", "폐기물 통 압축", MissionDifficulty.Easy, MissionPrototypeKind.ServerLogRecovery),
            Define(SurvivorMissionKind.IdCardSwipe, "Security", "중앙 보안 광장", "ID 카드 긁기", MissionDifficulty.Hard, MissionPrototypeKind.BreakerSequence),
            Define(SurvivorMissionKind.CctvScreenCleaning, "Security", "중앙 보안 광장", "CCTV 화면 닦기", MissionDifficulty.Easy, MissionPrototypeKind.CctvReboot),
            Define(SurvivorMissionKind.CircuitBreakerReset, "Power", "전력 복구실", "차단기 올리기", MissionDifficulty.Easy, MissionPrototypeKind.BreakerSequence),
            Define(SurvivorMissionKind.FuseReplacement, "Power", "전력 복구실", "퓨즈 교체", MissionDifficulty.Standard, MissionPrototypeKind.FuseSequence)
        };

        public static IReadOnlyList<SurvivorMissionDefinition> All =>
            Definitions;

        public static ulong GetMissionId(SurvivorMissionKind kind)
        {
            return MissionIdPrefix | ((ulong)kind + 1UL);
        }

        public static bool TryGetDefinition(
            ulong missionId,
            out SurvivorMissionDefinition definition)
        {
            var encoded = missionId & 0xFFUL;
            if ((missionId & 0xFFFFFFFFFFFFFF00UL) != MissionIdPrefix ||
                encoded == 0UL || encoded > (ulong)Definitions.Length)
            {
                definition = default;
                return false;
            }

            definition = Definitions[(int)encoded - 1];
            return true;
        }

        public static SurvivorMissionDefinition GetDefinition(
            SurvivorMissionKind kind)
        {
            var index = (int)kind;
            if (index < 0 || index >= Definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return Definitions[index];
        }

        private static SurvivorMissionDefinition Define(
            SurvivorMissionKind kind,
            string roomId,
            string roomDisplayName,
            string displayName,
            MissionDifficulty difficulty,
            MissionPrototypeKind prototypeKind)
        {
            return new SurvivorMissionDefinition(
                kind,
                roomId,
                roomDisplayName,
                displayName,
                difficulty,
                prototypeKind);
        }
    }
}
