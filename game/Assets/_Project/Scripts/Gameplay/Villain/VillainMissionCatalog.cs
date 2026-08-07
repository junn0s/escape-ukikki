using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Villain
{
    public readonly struct VillainMissionDefinition
    {
        public VillainMissionDefinition(
            VillainMissionKind kind,
            string roomId,
            string roomDisplayName,
            string displayName)
        {
            Kind = kind;
            RoomId = roomId;
            RoomDisplayName = roomDisplayName;
            DisplayName = displayName;
        }

        public VillainMissionKind Kind { get; }
        public string RoomId { get; }
        public string RoomDisplayName { get; }
        public string DisplayName { get; }
    }

    /// <summary>GDD §13.2의 빌런 전용 미션 6종 카탈로그다.</summary>
    public static class VillainMissionCatalog
    {
        private static readonly VillainMissionDefinition[] Definitions =
        {
            new(VillainMissionKind.CultureContamination, "LabA", "실험실 A", "배양액 오염시키기"),
            new(VillainMissionKind.VentBackflow, "QuarantineB", "격리실 B", "환풍구 역류 조작"),
            new(VillainMissionKind.MedicationRecordWipe, "Ward", "입원실", "투약 기록 삭제"),
            new(VillainMissionKind.ValvePressureRelease, "Storage", "액체 보관실", "밸브 압력 풀기"),
            new(VillainMissionKind.SecurityWireTangle, "Security", "중앙 보안 광장", "보안 카메라 선 꼬기"),
            new(VillainMissionKind.MainPowerLineCut, "Power", "전력 복구실", "메인 전력선 절단")
        };

        public static IReadOnlyList<VillainMissionDefinition> All =>
            Definitions;

        public static VillainMissionDefinition GetDefinition(
            VillainMissionKind kind)
        {
            var index = (int)kind;
            if (index < 0 || index >= Definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return Definitions[index];
        }
    }
}
