using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Noise;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Audio;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.Player;
using MonkeyLab.Presentation.Settings;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MonkeyLab.EditorTools
{
    public static class FirstPlayableBuilder
    {
        private const string LaboratoryScenePath =
            "Assets/_Project/Scenes/10_Laboratory.unity";
        private const string InputActionsPath =
            "Assets/_Project/Settings/PlayerControls.inputactions";
        private const string MovementConfigPath =
            "Assets/_Project/Data/Balance/SO_PlayerMovement_Default.asset";
        private const string FuseMissionConfigPath =
            "Assets/_Project/Data/Missions/SO_FuseMission_Default.asset";
        private const string NoiseBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_NoiseBalance_Default.asset";
        private const string MonsterBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_MonsterBalance_Default.asset";
        private const string MonsterTierConfigPath =
            "Assets/_Project/Data/Balance/SO_MonsterTier_Default.asset";
        private const string AntidoteBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_AntidoteBalance_Default.asset";
        private const string RoundBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_RoundBalance_Default.asset";
        private const string InteractionBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_InteractionBalance_Default.asset";
        private const string DoorBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_DoorBalance_Default.asset";
        private const string UpgradeBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_UpgradeBalance_Default.asset";
        private const string SpeakerBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_SpeakerBalance_Default.asset";
        private const string WorldLightingBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_WorldLightingBalance_Default.asset";
        private const string PresentationAssetCatalogPath =
            "Assets/_Project/Data/Catalogs/SO_PresentationAssetCatalog_Default.asset";
        private const string SpriteRoot =
            "Assets/_Project/Art/Sprites/Generated";
        private const string CharacterSpriteRoot =
            "Assets/_Project/Art/Sprites/Characters";
        private const string MaterialRoot =
            "Assets/_Project/Art/Materials";
        private const string WorldSpriteLitMaterialPath =
            MaterialRoot + "/M_WorldSpriteLit.mat";
        private const string IndicatorUnlitMaterialPath =
            MaterialRoot + "/M_IndicatorUnlit.mat";
        private const string UnitSpritePath = SpriteRoot + "/S_UnitSquare.asset";
        private const string PlayerSpritePath =
            CharacterSpriteRoot + "/S_Player_Survivor.png";
        private const string VisorSpritePath = SpriteRoot + "/S_Player_Visor.asset";
        private const string MonsterSpritePath =
            CharacterSpriteRoot + "/S_Monkey_Mutant.png";
        private const string CircleSpritePath = SpriteRoot + "/S_StatusCircle.asset";
        private const string FlashlightSpritePath = SpriteRoot + "/S_FlashlightCone.asset";
        private const string PanelSpritePath = SpriteRoot + "/S_MissionPanel.asset";
        private const float RuntimeMonsterTestTimeoutSeconds = 5f;
        private const float RuntimeAntidoteTestTimeoutSeconds = 3f;
        private const float CorridorWidth = 4.5f;
        private const float WallThickness = 0.32f;

        private static readonly string[] RoomOrder =
        {
            "VaccineA", "LabA", "QuarantineA", "Ward", "VaccineB",
            "Power", "Security", "QuarantineB", "LabB", "Storage"
        };

        private static readonly string[] MonsterSpawnRoomIds =
        {
            "VaccineA", "VaccineB", "LabB", "QuarantineB"
        };

        private static readonly RoomDefinition[] RoomDefinitions =
        {
            new("VaccineA", new Vector2(-34f, 22f), new Vector2(12f, 12f), "백신실 A"),
            new("LabA", new Vector2(-17f, 24f), new Vector2(14f, 10f), "실험실 A"),
            new("QuarantineA", new Vector2(-2f, 24f), new Vector2(10f, 9f), "격리실 A"),
            new("Storage", new Vector2(-36f, 1f), new Vector2(9f, 18f), "액체 보관실"),
            new("Security", new Vector2(-3f, 3f), new Vector2(14f, 14f), "중앙 보안 광장"),
            new("Power", new Vector2(14f, 7f), new Vector2(12f, 10f), "전력 복구실"),
            new("Ward", new Vector2(14f, 23f), new Vector2(18f, 11f), "입원실"),
            new("LabB", new Vector2(-18f, -14f), new Vector2(14f, 10f), "실험실 B"),
            new("QuarantineB", new Vector2(1f, -17f), new Vector2(14f, 12f), "격리실 B"),
            new("VaccineB", new Vector2(31f, 23f), new Vector2(10f, 14f), "백신실 B")
        };

        private static readonly CorridorDefinition[] CorridorDefinitions =
        {
            new(
                "VaccineA", WallSide.East,
                "LabA", WallSide.West,
                new Vector2(-28f, 24f),
                new Vector2(-24f, 24f)),
            new(
                "VaccineA", WallSide.South,
                "Storage", WallSide.North,
                new Vector2(-36f, 16f),
                new Vector2(-36f, 10f)),
            new(
                "LabA", WallSide.East,
                "QuarantineA", WallSide.West,
                new Vector2(-10f, 24f),
                new Vector2(-7f, 24f)),
            new(
                "QuarantineA", WallSide.East,
                "Ward", WallSide.West,
                new Vector2(3f, 24f),
                new Vector2(5f, 24f)),
            new(
                "Ward", WallSide.East,
                "VaccineB", WallSide.West,
                new Vector2(23f, 24f),
                new Vector2(26f, 24f)),
            new(
                "Ward", WallSide.South,
                "Power", WallSide.North,
                new Vector2(14f, 17.5f),
                new Vector2(14f, 12f)),
            new(
                "Power", WallSide.West,
                "Security", WallSide.East,
                new Vector2(8f, 7f),
                new Vector2(4f, 7f)),
            new(
                "Security", WallSide.North,
                "LabA", WallSide.South,
                new Vector2(-3f, 10f),
                new Vector2(-3f, 15f),
                new Vector2(-17f, 15f),
                new Vector2(-17f, 19f)),
            new(
                "Storage", WallSide.East,
                "Security", WallSide.West,
                new Vector2(-31.5f, 3f),
                new Vector2(-10f, 3f)),
            new(
                "Storage", WallSide.South,
                "LabB", WallSide.West,
                new Vector2(-36f, -8f),
                new Vector2(-36f, -14f),
                new Vector2(-25f, -14f)),
            new(
                "LabB", WallSide.East,
                "QuarantineB", WallSide.West,
                new Vector2(-11f, -16f),
                new Vector2(-6f, -16f)),
            new(
                "Security", WallSide.South,
                "QuarantineB", WallSide.North,
                new Vector2(1f, -4f),
                new Vector2(1f, -11f)),
            new(
                "Power", WallSide.South,
                "QuarantineB", WallSide.North,
                new Vector2(14f, 2f),
                new Vector2(14f, -7f),
                new Vector2(1f, -7f),
                new Vector2(1f, -11f)),
            new(
                "QuarantineB", WallSide.East,
                "VaccineB", WallSide.South,
                new Vector2(8f, -17f),
                new Vector2(25f, -17f),
                new Vector2(25f, 14f),
                new Vector2(31f, 14f),
                new Vector2(31f, 16f))
        };

        private static readonly EnvironmentPropDefinition[]
            EnvironmentPropDefinitions =
        {
            // 백신실 A — 실제 제작기·보관함·미션 단말 외의 환경 설비
            new("VaccineA", "SM_PharmaFridge", new Vector2(-4.4f, 0.8f), new Vector2(1.4f, 3.2f), EnvironmentPropCategory.Medical, true),
            new("VaccineA", "SM_VialRack", new Vector2(4.4f, -2.4f), new Vector2(1.2f, 2.8f), EnvironmentPropCategory.Medical, true),
            new("VaccineA", "SM_SterileBench", new Vector2(0f, 4.5f), new Vector2(3.4f, 1.1f), EnvironmentPropCategory.Laboratory, true),
            new("VaccineA", "SM_ColdCabinet", new Vector2(-5f, -3.4f), new Vector2(1.5f, 1.8f), EnvironmentPropCategory.Storage, true),
            new("VaccineA", "SM_VialCart", new Vector2(2.6f, -4.2f), new Vector2(1.5f, 1.1f), EnvironmentPropCategory.Medical, false),
            new("VaccineA", "SM_BiosafetyHood", new Vector2(2.2f, -2.5f), new Vector2(1.2f, 2.1f), EnvironmentPropCategory.Laboratory, true, hasStatusIndicator: true),
            new("VaccineA", "SM_DeconSink", new Vector2(-2.8f, 5f), new Vector2(1.8f, 0.8f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("VaccineA", "SM_PpeDispenser", new Vector2(-5.5f, 4.3f), new Vector2(0.55f, 1.1f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("VaccineA", "VFX_SterileFloorZone", new Vector2(1.9f, 1.5f), new Vector2(3f, 2.1f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 실험실 A
            new("LabA", "SM_LabBench_Long", new Vector2(0f, 3.7f), new Vector2(4.5f, 1.1f), EnvironmentPropCategory.Laboratory, true),
            new("LabA", "SM_Centrifuge", new Vector2(4.9f, 3.3f), new Vector2(1.5f, 1.5f), EnvironmentPropCategory.Laboratory, true),
            new("LabA", "SM_Microscope", new Vector2(-4.7f, -3.3f), new Vector2(1.4f, 1.2f), EnvironmentPropCategory.Laboratory, true),
            new("LabA", "SM_SampleRack", new Vector2(-5.6f, 2.4f), new Vector2(1.1f, 1.8f), EnvironmentPropCategory.Medical, false),
            new("LabA", "SM_ChemicalShelf", new Vector2(5.7f, -3.4f), new Vector2(1f, 2.4f), EnvironmentPropCategory.Storage, true),
            new("LabA", "SM_VentOutlet", new Vector2(-5.6f, 3.9f), new Vector2(1.5f, 0.7f), EnvironmentPropCategory.Hazard, false),
            new("LabA", "SM_SpecimenScanner", new Vector2(3.8f, -3.3f), new Vector2(1.1f, 1.7f), EnvironmentPropCategory.Laboratory, true, hasStatusIndicator: true),
            new("LabA", "SM_EyeWashStation", new Vector2(-5.9f, 0f), new Vector2(0.7f, 1.3f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("LabA", "SM_OverheadServiceRail", new Vector2(0f, -4.45f), new Vector2(5f, 0.28f), EnvironmentPropCategory.Utility, false, EnvironmentPropMountKind.Overhead, 11),
            new("LabA", "VFX_ChemicalSpillMark", new Vector2(2.8f, -2.7f), new Vector2(1.4f, 0.8f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 격리실 A
            new("QuarantineA", "SM_GlassCell_Wide", new Vector2(0f, 2.8f), new Vector2(5.4f, 1.2f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineA", "SM_CagePod_A", new Vector2(-3.5f, -3.2f), new Vector2(1.5f, 1.9f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineA", "SM_CagePod_B", new Vector2(0f, -3.2f), new Vector2(1.5f, 1.9f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineA", "SM_ContainmentLock", new Vector2(-4.1f, 3.7f), new Vector2(1.5f, 0.8f), EnvironmentPropCategory.Hazard, false),
            new("QuarantineA", "VFX_WarningBeacon_A", new Vector2(-3f, 3.7f), new Vector2(0.6f, 0.6f), EnvironmentPropCategory.Hazard, false),
            new("QuarantineA", "VFX_WarningBeacon_B", new Vector2(4f, 3.7f), new Vector2(0.6f, 0.6f), EnvironmentPropCategory.Hazard, false),
            new("QuarantineA", "SM_ObservationConsole", new Vector2(3.8f, 2.9f), new Vector2(1.2f, 1.1f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
            new("QuarantineA", "SM_DeconShower", new Vector2(3.8f, -2.8f), new Vector2(1.2f, 1.6f), EnvironmentPropCategory.Quarantine, false, EnvironmentPropMountKind.WallMounted),
            new("QuarantineA", "SM_RestraintRail", new Vector2(0f, 4.1f), new Vector2(2.8f, 0.25f), EnvironmentPropCategory.Quarantine, false, EnvironmentPropMountKind.WallMounted),
            new("QuarantineA", "VFX_ContainmentFloorGrid", new Vector2(0f, 0f), new Vector2(4.2f, 2.3f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 액체 보관실
            new("Storage", "SM_CryoTank_A", new Vector2(-3.15f, 6.4f), new Vector2(1.8f, 2.5f), EnvironmentPropCategory.Storage, true),
            new("Storage", "SM_CryoTank_B", new Vector2(-3f, 0.2f), new Vector2(1.8f, 2.5f), EnvironmentPropCategory.Storage, true),
            new("Storage", "SM_CryoTank_C", new Vector2(-3.15f, -6.2f), new Vector2(1.8f, 2.5f), EnvironmentPropCategory.Storage, true),
            new("Storage", "SM_ColdShelf_A", new Vector2(3f, 6.3f), new Vector2(1.4f, 2.8f), EnvironmentPropCategory.Storage, true),
            new("Storage", "SM_ColdShelf_B", new Vector2(3f, -5.8f), new Vector2(1.4f, 3.1f), EnvironmentPropCategory.Storage, true),
            new("Storage", "SM_SampleDrum", new Vector2(2.8f, -1.4f), new Vector2(1.6f, 1.6f), EnvironmentPropCategory.Laboratory, true),
            new("Storage", "SM_FrozenPipe", new Vector2(-4f, 3.8f), new Vector2(0.55f, 4.2f), EnvironmentPropCategory.Utility, false),
            new("Storage", "SM_TemperatureTerminal", new Vector2(3.7f, 2.6f), new Vector2(0.65f, 1.2f), EnvironmentPropCategory.Security, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("Storage", "SM_CoolantManifold", new Vector2(-3.8f, -2.8f), new Vector2(0.65f, 2f), EnvironmentPropCategory.Utility, false, EnvironmentPropMountKind.WallMounted),
            new("Storage", "SM_InsulatedPallet", new Vector2(2.5f, 4.1f), new Vector2(1.5f, 1.2f), EnvironmentPropCategory.Storage, true),
            new("Storage", "VFX_FrostDrain", new Vector2(0f, -6.8f), new Vector2(1.3f, 0.5f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 중앙 보안 광장
            new("Security", "SM_ServerRack_A", new Vector2(-5.2f, 4.9f), new Vector2(1.5f, 2.8f), EnvironmentPropCategory.Security, true),
            new("Security", "SM_ServerRack_B", new Vector2(-5.2f, -4.8f), new Vector2(1.5f, 2.8f), EnvironmentPropCategory.Security, true),
            new("Security", "SM_CctvMonitorWall", new Vector2(3.6f, 5.7f), new Vector2(4f, 0.9f), EnvironmentPropCategory.Security, false),
            new("Security", "SM_ElectronicMapTable", new Vector2(-1.2f, 0.3f), new Vector2(2.4f, 1.7f), EnvironmentPropCategory.Security, false),
            new("Security", "SM_LogTerminal", new Vector2(5.6f, -3.5f), new Vector2(1.5f, 1.4f), EnvironmentPropCategory.Security, true),
            new("Security", "SM_QuarantineControl", new Vector2(-1.8f, -5.6f), new Vector2(2.4f, 0.9f), EnvironmentPropCategory.Hazard, true),
            new("Security", "SM_OperatorChair", new Vector2(1.5f, 0.4f), new Vector2(0.9f, 0.9f), EnvironmentPropCategory.Common, false),
            new("Security", "SM_OperatorConsole_A", new Vector2(3.4f, 2.4f), new Vector2(1.6f, 1.1f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
            new("Security", "SM_OperatorConsole_B", new Vector2(-3.5f, -2.8f), new Vector2(1.5f, 1.1f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
            new("Security", "SM_ServerCoolingUnit", new Vector2(-3.5f, 4.8f), new Vector2(1f, 1.8f), EnvironmentPropCategory.Utility, true, hasStatusIndicator: true),
            new("Security", "SM_AlarmPanel", new Vector2(5.9f, 0f), new Vector2(0.45f, 1.1f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("Security", "VFX_CableChannel", new Vector2(0f, -2.1f), new Vector2(5f, 0.35f), EnvironmentPropCategory.Utility, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 전력 복구실
            new("Power", "SM_Generator", new Vector2(4.2f, 0f), new Vector2(2.1f, 3.2f), EnvironmentPropCategory.Power, true),
            new("Power", "SM_BreakerBank", new Vector2(4.4f, -3.6f), new Vector2(2f, 1f), EnvironmentPropCategory.Power, true),
            new("Power", "SM_CableReel_A", new Vector2(-4.4f, 3.3f), new Vector2(1.4f, 1.4f), EnvironmentPropCategory.Utility, true),
            new("Power", "SM_CableReel_B", new Vector2(-4.4f, -3.2f), new Vector2(1.4f, 1.4f), EnvironmentPropCategory.Utility, true),
            new("Power", "SM_BackupCellRack", new Vector2(3.8f, 3.8f), new Vector2(2.2f, 1f), EnvironmentPropCategory.Power, true, hasStatusIndicator: true),
            new("Power", "SM_FloorCable", new Vector2(2f, 0.1f), new Vector2(3.6f, 0.35f), EnvironmentPropCategory.Hazard, false),
            new("Power", "SM_TransformerPanel", new Vector2(5.2f, 3.1f), new Vector2(0.8f, 1.4f), EnvironmentPropCategory.Power, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("Power", "SM_ToolCabinet", new Vector2(-2.7f, -3.8f), new Vector2(0.8f, 1.8f), EnvironmentPropCategory.Utility, true),
            new("Power", "SM_EmergencyCutoff", new Vector2(5.3f, -1.9f), new Vector2(0.5f, 0.8f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("Power", "VFX_HighVoltageFloorMark", new Vector2(1.5f, 2.1f), new Vector2(2.8f, 1.3f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 입원실
            new("Ward", "SM_HospitalBed_A", new Vector2(-6.8f, 4.2f), new Vector2(2.8f, 1.25f), EnvironmentPropCategory.Medical, true),
            new("Ward", "SM_HospitalBed_B", new Vector2(-3.4f, 3.5f), new Vector2(2.8f, 1.25f), EnvironmentPropCategory.Medical, true),
            new("Ward", "SM_HospitalBed_C", new Vector2(3.6f, -3.6f), new Vector2(2.8f, 1.25f), EnvironmentPropCategory.Medical, true),
            new("Ward", "SM_HospitalBed_D", new Vector2(6.8f, -3.6f), new Vector2(2.8f, 1.25f), EnvironmentPropCategory.Medical, true),
            new("Ward", "SM_CurtainRail_A", new Vector2(-5.2f, 2.4f), new Vector2(5.5f, 0.3f), EnvironmentPropCategory.Medical, false),
            new("Ward", "SM_CurtainRail_B", new Vector2(5.2f, -2.5f), new Vector2(5.5f, 0.3f), EnvironmentPropCategory.Medical, false),
            new("Ward", "SM_IvStand_A", new Vector2(-8f, 2.2f), new Vector2(0.5f, 0.5f), EnvironmentPropCategory.Medical, false),
            new("Ward", "SM_IvStand_B", new Vector2(8f, -2.2f), new Vector2(0.5f, 0.5f), EnvironmentPropCategory.Medical, false),
            new("Ward", "SM_MedicalMonitor_A", new Vector2(-8f, 4.5f), new Vector2(0.8f, 0.8f), EnvironmentPropCategory.Security, false),
            new("Ward", "SM_MedicalMonitor_B", new Vector2(8f, -4.5f), new Vector2(0.8f, 0.8f), EnvironmentPropCategory.Security, false),
            new("Ward", "SM_MedicineCart", new Vector2(6.8f, 3.7f), new Vector2(1.5f, 1f), EnvironmentPropCategory.Medical, true),
            new("Ward", "VFX_BloodStain_A", new Vector2(0.2f, 3.4f), new Vector2(1.8f, 0.8f), EnvironmentPropCategory.Hazard, false),
            new("Ward", "VFX_BloodStain_B", new Vector2(-0.8f, -3.2f), new Vector2(1.3f, 0.7f), EnvironmentPropCategory.Hazard, false),
            new("Ward", "SM_NurseStation", new Vector2(0f, 4.4f), new Vector2(3.2f, 1f), EnvironmentPropCategory.Medical, true, hasStatusIndicator: true),
            new("Ward", "SM_OxygenPorts_A", new Vector2(-5.1f, 5f), new Vector2(2.2f, 0.35f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("Ward", "SM_OxygenPorts_B", new Vector2(5.2f, -5f), new Vector2(2.2f, 0.35f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("Ward", "SM_MedicineCabinet", new Vector2(8.4f, 0f), new Vector2(0.7f, 2f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("Ward", "VFX_TriageFloorNumbers", new Vector2(0f, 0f), new Vector2(5.4f, 1.2f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 실험실 B
            new("LabB", "SM_PackagingBench", new Vector2(0f, 3.7f), new Vector2(4.5f, 1.1f), EnvironmentPropCategory.Laboratory, true),
            new("LabB", "SM_Centrifuge_Industrial", new Vector2(-5.2f, -3.4f), new Vector2(1.8f, 1.6f), EnvironmentPropCategory.Laboratory, true),
            new("LabB", "SM_ServerBackupRack", new Vector2(5.5f, 1.7f), new Vector2(1.2f, 2.2f), EnvironmentPropCategory.Security, true),
            new("LabB", "SM_SampleSealer", new Vector2(-4.8f, 3.1f), new Vector2(1.6f, 1.3f), EnvironmentPropCategory.Laboratory, true),
            new("LabB", "SM_ChemicalShelf_B", new Vector2(3.8f, 1.3f), new Vector2(1f, 1.8f), EnvironmentPropCategory.Storage, true),
            new("LabB", "SM_VentOutlet", new Vector2(-5.7f, 4f), new Vector2(1.5f, 0.7f), EnvironmentPropCategory.Hazard, false),
            new("LabB", "SM_PackageScanner", new Vector2(3.4f, -3.2f), new Vector2(0.9f, 1.5f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
            new("LabB", "SM_SealedCrateStack", new Vector2(-2.8f, -3.5f), new Vector2(1.7f, 1.3f), EnvironmentPropCategory.Storage, true),
            new("LabB", "SM_WashSink_B", new Vector2(1.8f, -4.4f), new Vector2(1.8f, 0.7f), EnvironmentPropCategory.Laboratory, false, EnvironmentPropMountKind.WallMounted),
            new("LabB", "VFX_PackagingRoute", new Vector2(1.7f, 1.4f), new Vector2(3.8f, 0.55f), EnvironmentPropCategory.Laboratory, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 격리실 B
            new("QuarantineB", "SM_GlassCell_A", new Vector2(-4.9f, -4.4f), new Vector2(2.1f, 1.8f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineB", "SM_GlassCell_B", new Vector2(0f, -4.5f), new Vector2(2.1f, 1.8f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineB", "SM_GlassCell_C", new Vector2(4.9f, -4.4f), new Vector2(2.1f, 1.8f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineB", "SM_DeconUnit_A", new Vector2(-5.6f, 4.4f), new Vector2(1.3f, 2.1f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineB", "SM_DeconUnit_B", new Vector2(5.5f, 3.5f), new Vector2(1.3f, 2.1f), EnvironmentPropCategory.Quarantine, true),
            new("QuarantineB", "SM_ContainmentLock_B", new Vector2(3f, -5.3f), new Vector2(1.5f, 0.7f), EnvironmentPropCategory.Hazard, false),
            new("QuarantineB", "VFX_QuarantineWarning", new Vector2(-3f, -5.3f), new Vector2(0.8f, 0.6f), EnvironmentPropCategory.Hazard, false),
            new("QuarantineB", "SM_ObservationConsole_B", new Vector2(3.6f, 2.7f), new Vector2(0.9f, 1.6f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
            new("QuarantineB", "SM_RestraintController", new Vector2(-5.9f, 0f), new Vector2(0.7f, 1.2f), EnvironmentPropCategory.Quarantine, false, EnvironmentPropMountKind.WallMounted, hasStatusIndicator: true),
            new("QuarantineB", "SM_DeconShower_B", new Vector2(2.8f, 4.9f), new Vector2(1.4f, 0.8f), EnvironmentPropCategory.Quarantine, false, EnvironmentPropMountKind.WallMounted),
            new("QuarantineB", "VFX_BrokenGlass_A", new Vector2(-2.8f, 1.8f), new Vector2(1.4f, 0.8f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),
            new("QuarantineB", "VFX_ContainmentFloorNumbers", new Vector2(2.4f, -1.6f), new Vector2(3.2f, 0.7f), EnvironmentPropCategory.Quarantine, false, EnvironmentPropMountKind.FloorDecal, 2),

            // 백신실 B
            new("VaccineB", "SM_PharmaFridge_B", new Vector2(3.8f, 1.1f), new Vector2(1.4f, 3f), EnvironmentPropCategory.Medical, true),
            new("VaccineB", "SM_VialRack_B", new Vector2(3.8f, 5.2f), new Vector2(1.3f, 1.8f), EnvironmentPropCategory.Medical, true),
            new("VaccineB", "SM_MixingBench", new Vector2(0.7f, 5.8f), new Vector2(3.1f, 0.9f), EnvironmentPropCategory.Laboratory, true),
            new("VaccineB", "SM_ColdCabinet_B", new Vector2(3.8f, -4.2f), new Vector2(1.4f, 2f), EnvironmentPropCategory.Storage, true),
            new("VaccineB", "SM_VialCart_B", new Vector2(0.4f, 1.5f), new Vector2(1.4f, 1f), EnvironmentPropCategory.Medical, false),
            new("VaccineB", "SM_BiosafetyHood_B", new Vector2(-3.8f, 5.2f), new Vector2(1.2f, 2f), EnvironmentPropCategory.Laboratory, true, hasStatusIndicator: true),
            new("VaccineB", "SM_InjectorTester", new Vector2(-3.9f, -3.3f), new Vector2(1f, 1.5f), EnvironmentPropCategory.Medical, true, hasStatusIndicator: true),
            new("VaccineB", "SM_DeconSink_B", new Vector2(-1.5f, -6.3f), new Vector2(1.8f, 0.65f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("VaccineB", "SM_PpeDispenser_B", new Vector2(4.5f, -6f), new Vector2(0.55f, 1.1f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.WallMounted),
            new("VaccineB", "VFX_SterileFloorZone_B", new Vector2(-0.8f, -1.6f), new Vector2(3f, 2f), EnvironmentPropCategory.Medical, false, EnvironmentPropMountKind.FloorDecal, 2)
        };

        private static readonly Vector2[] PlayerSpawnPositions =
        {
            new(-22f, 3f), new(-17f, 15f), new(6f, 7f),
            new(14f, 15f), new(-18f, -7f), new(1f, -7f)
        };

        private static MonsterBrain _runtimeTestMonster;
        private static MonsterTarget _runtimeTestTarget;
        private static InfectionService _runtimeTestInfection;
        private static int _runtimeTestInitialBiteCount;
        private static double _runtimeTestStartedAt;
        private static bool _runtimeTestObservedChase;
        private static bool _runtimeTestObservedPatrolAfterBite;
        private static InfectionService _runtimeAntidoteTestInfection;
        private static AntidoteService _runtimeAntidoteTestService;
        private static double _runtimeAntidoteTestStartedAt;
        private static Material _worldSpriteLitMaterial;
        private static Material _indicatorUnlitMaterial;

        [MenuItem("Tools/Monkey Lab/Build First Playable")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before building the first playable.");
            }

            EnsureSpriteAssets();
            EnsureLightingMaterials();
            var scene = EditorSceneManager.OpenScene(
                LaboratoryScenePath,
                OpenSceneMode.Single);
            ClearOldLaboratoryObjects();

            var mapRoot = new GameObject("[Map] Laboratory2D");
            var rooms = BuildMap(mapRoot.transform);
            var prototypeRoot = new GameObject("[Prototype] FirstPlayable");
            var roundPhase = CreateRoundPhase(prototypeRoot.transform);
            CreateGracePeriodView(prototypeRoot.transform, roundPhase);
            CreateRoundHudView(prototypeRoot.transform);
            CreateVillainUpgradeHudView(prototypeRoot.transform);
            var monsterTierRuntime = CreateMonsterTierRuntime(prototypeRoot.transform);
            var noiseService = CreateNoiseService(prototypeRoot.transform);
            var navigationGraph = CreateNavigationGraph(
                prototypeRoot.transform,
                rooms);
            var fuseStations = new[]
            {
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["VaccineA"],
                    "MissionStation_VaccineA",
                    new Vector2(0f, -3.5f),
                    MissionPrototypeKind.SampleSorting),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["LabA"],
                    "MissionStation_LabA",
                    new Vector2(3f, -3f),
                    MissionPrototypeKind.ServerLogRecovery),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["QuarantineA"],
                    "MissionStation_QuarantineA",
                    new Vector2(3f, -3.5f),
                    MissionPrototypeKind.SecurityCircuit),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Storage"],
                    "MissionStation_Storage",
                    new Vector2(-3f, 3f),
                    MissionPrototypeKind.BatteryTransport),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Security"],
                    "MissionStation_Security",
                    new Vector2(3f, 3f),
                    MissionPrototypeKind.CctvReboot),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Power"],
                    "MissionStation_Power",
                    new Vector2(3.3f, 3.6f),
                    MissionPrototypeKind.FuseSequence),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["Ward"],
                    "MissionStation_Ward",
                    new Vector2(3f, 3f),
                    MissionPrototypeKind.AntennaAlignment),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["LabB"],
                    "MissionStation_LabB",
                    new Vector2(3f, -3f),
                    MissionPrototypeKind.SampleSorting),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["QuarantineB"],
                    "MissionStation_QuarantineB",
                    new Vector2(-3f, 3.5f),
                    MissionPrototypeKind.PressureValves),
                CreateFuseStation(
                    prototypeRoot.transform,
                    rooms["VaccineB"],
                    "MissionStation_VaccineB",
                    new Vector2(-3f, 3.5f),
                    MissionPrototypeKind.BreakerSequence)
            };
            CreateBatteryReceiver(
                prototypeRoot.transform,
                rooms["Ward"],
                "MissionBatteryReceiver_Ward",
                new Vector2(-3f, -3f),
                fuseStations[3]);
            var missionRoomIds = new[]
            {
                "vaccine_a", "lab_a", "quarantine_a", "storage",
                "security", "power", "ward", "lab_b", "quarantine_b",
                "vaccine_b"
            };
            var missionAuthorities =
                new NetworkFuseStationAuthority[fuseStations.Length];
            for (var index = 0; index < fuseStations.Length; index++)
            {
                ConfigureFuseStationFeedback(
                    fuseStations[index],
                    noiseService,
                    missionRoomIds[index]);
                CreateFuseMissionView(
                    prototypeRoot.transform,
                    fuseStations[index],
                    index == 5
                        ? "[UI] FuseMission"
                        : $"[UI] Mission_{index + 1:00}");
                missionAuthorities[index] = fuseStations[index]
                    .GetComponent<NetworkFuseStationAuthority>();
            }

            CreateNetworkRoundState(
                prototypeRoot.transform,
                roundPhase,
                missionAuthorities);
            CreateMilestoneWorldPresentation(
                prototypeRoot.transform,
                mapRoot.transform,
                rooms);
            CreateNoiseAlertView(prototypeRoot.transform, noiseService);

            var player = CreatePlayer(
                prototypeRoot.transform,
                PlayerSpawnPositions[0]);
            var monsterTarget = player.GetComponent<MonsterTarget>();
            CreateInfectionPrototype(
                prototypeRoot.transform,
                player,
                monsterTarget,
                monsterTierRuntime);
            CreateMonsterBiteAlertView(prototypeRoot.transform, monsterTarget);
            var baseMonsters = CreateMonsters(
                prototypeRoot.transform,
                rooms,
                navigationGraph,
                noiseService,
                roundPhase,
                monsterTierRuntime,
                monsterTarget);
            CreateUpgradeSystem(
                prototypeRoot.transform,
                rooms,
                navigationGraph,
                noiseService,
                roundPhase,
                monsterTierRuntime,
                monsterTarget,
                baseMonsters);
            CreateAntidoteEconomy(prototypeRoot.transform, rooms, player);
            ConfigureCamera(player.transform);
            CreateGameplayFeelView(
                prototypeRoot.transform,
                Camera.main.GetComponent<TopDownCamera>(),
                player,
                fuseStations,
                prototypeRoot.GetComponentsInChildren<MonsterBrain>(
                    includeInactive: true));
            CreateEndingWorldPresentation(
                prototypeRoot.transform,
                rooms,
                Camera.main.GetComponent<TopDownCamera>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LaboratoryScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Validate();
            Debug.Log(
                "[MonkeyLab] 2D top-down first playable is ready: " +
                "WASD, mouse aim, F flashlight, E interaction, R antidote.");
        }

        internal static void EnsureTopDownArtAssets()
        {
            EnsureSpriteAssets();
            EnsureLightingMaterials();
        }

        [MenuItem("Tools/Monkey Lab/Build Complete 2D Top Down")]
        public static void BuildCompleteTopDown()
        {
            Build();
            ProjectBootstrap.BuildNetworkPlayerFlow();
            EditorSceneManager.OpenScene(LaboratoryScenePath, OpenSceneMode.Single);
            Debug.Log(
                "[MonkeyLab] Complete 2D conversion finished, including the network player prefab.");
        }

        [MenuItem("Tools/Monkey Lab/Validate First Playable")]
        public static void Validate()
        {
            if (SceneManager.GetActiveScene().path != LaboratoryScenePath)
            {
                EditorSceneManager.OpenScene(
                    LaboratoryScenePath,
                    OpenSceneMode.Single);
            }

            var failures = new List<string>();
            ValidateCorridorLayout(failures);
            ValidateEnvironmentPropDefinitions(failures);
            ValidateLightingPresentation(failures);
            var player = GameObject.Find("P_Player_Local");
            RequireComponent<Rigidbody2D>(player, failures);
            RequireComponent<CapsuleCollider2D>(player, failures);
            RequireComponent<PlayerInputReader>(player, failures);
            RequireComponent<PlayerMotor>(player, failures);
            RequireComponent<PlayerAimController>(player, failures);
            RequireComponent<PlayerInteractor>(player, failures);
            RequireComponent<MonsterTarget>(player, failures);
            RequireComponent<InfectionService>(player, failures);
            RequireComponent<AntidoteService>(player, failures);

            if (player != null &&
                (player.GetComponent<CharacterController>() != null ||
                 player.GetComponent<Collider>() != null ||
                 (player.GetComponent<Rigidbody2D>().constraints &
                 RigidbodyConstraints2D.FreezeRotation) == 0 ||
                 player.transform.Find(
                     "VisualRoot/AimPivot/FlashlightCone") == null))
            {
                failures.Add(
                    "P_Player_Local movement, fixed visual or flashlight pivot is incomplete.");
            }

            var mainCamera = Camera.main;
            if (mainCamera == null || !mainCamera.orthographic ||
                mainCamera.GetComponent<TopDownCamera>() == null)
            {
                failures.Add("Main Camera is missing the orthographic TopDownCamera.");
            }

            var graph = GameObject.Find("[Navigation] Laboratory2D")?
                .GetComponent<TopDownNavigationGraph>();
            if (graph == null ||
                graph.NodeCount <
                RoomDefinitions.Length + CorridorDefinitions.Length * 2 ||
                graph.LinkCount < CorridorDefinitions.Length * 3)
            {
                failures.Add("The 2D laboratory navigation graph is incomplete.");
            }

            var walls = GameObject.Find("[Map] CollisionWalls");
            if (walls == null || walls.GetComponentsInChildren<BoxCollider2D>().Length < 20)
            {
                failures.Add("The 2D room and corridor collision walls are missing.");
            }

            var automaticDoors =
                UnityEngine.Object.FindObjectsByType<
                    NetworkAutomaticDoorAuthority>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (automaticDoors.Length != CountUniqueDoorways())
            {
                failures.Add(
                    "Every unique room entrance must contain one automatic door.");
            }
            else
            {
                foreach (var door in automaticDoors)
                {
                    if (door.GetComponent<NetworkObject>() == null ||
                        door.GetComponent<AutomaticDoorMotor>() == null ||
                        !HasDoorTriggerAndBlocker(door.gameObject))
                    {
                        failures.Add(
                            $"Automatic door {door.name} is incomplete.");
                    }
                }
            }

            var environmentProps =
                UnityEngine.Object.FindObjectsByType<EnvironmentPropSlot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var minimumPropCount = EnvironmentPropDefinitions.Length +
                                   RoomDefinitions.Length * 14;
            if (environmentProps.Length < minimumPropCount)
            {
                failures.Add(
                    "Room environment prop replacement slots are incomplete.");
            }

            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (roundPhase == null || roundPhase.Config == null ||
                !Mathf.Approximately(
                    roundPhase.Config.InitialGracePeriodSeconds,
                    30f))
            {
                failures.Add("The local 30 second grace period is missing.");
            }

            var station = GameObject.Find("MissionStation_Power")?
                .GetComponent<FuseStationPrototype>();
            if (station == null || station.Config == null ||
                station.GetComponent<Collider2D>() == null)
            {
                failures.Add("The 2D fuse mission station is incomplete.");
            }

            var missionStations =
                UnityEngine.Object.FindObjectsByType<FuseStationPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (missionStations.Length != RoomDefinitions.Length)
            {
                failures.Add(
                    "Each laboratory room must contain one MVP mission station.");
            }

            var noiseService = GameObject.Find("[Gameplay] NoiseService")?
                .GetComponent<NoiseService>();
            if (noiseService == null || noiseService.Config == null)
            {
                failures.Add("NoiseService or its config is missing.");
            }

            var monster = GameObject.Find("P_Monster_01");
            var monsterBrain = monster?.GetComponent<MonsterBrain>();
            var monsterBrains = UnityEngine.Object.FindObjectsByType<MonsterBrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            // 기본 4마리 + 개체 강화 예비 4마리(2단계 × 2마리)를 모두 센다.
            const int reinforcementMonsterCount = 4;
            if (monsterBrains.Length !=
                    MonsterSpawnRoomIds.Length + reinforcementMonsterCount ||
                monsterBrain == null || monsterBrain.Config == null ||
                monsterBrain.PatrolPointCount < 3 ||
                monster.GetComponent<Rigidbody2D>() == null ||
                monster.GetComponent<CapsuleCollider2D>() == null ||
                monster.GetComponent<MonsterSenses>() == null ||
                monster.GetComponent<MonsterBiteController>() == null ||
                (monster.GetComponent<Rigidbody2D>().constraints &
                 RigidbodyConstraints2D.FreezeRotation) == 0 ||
                monsterBrain.NavigationGraph != graph)
            {
                failures.Add("The 2D monster AI setup is incomplete.");
            }

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            if (inputActions == null ||
                inputActions.FindAction("Gameplay/Move") == null ||
                inputActions.FindAction("Gameplay/Look") == null ||
                inputActions.FindAction("Gameplay/Interact") == null ||
                inputActions.FindAction("Gameplay/Flashlight") == null ||
                inputActions.FindAction("Gameplay/UseAntidote") == null ||
                inputActions.FindAction("Gameplay/Cancel") == null)
            {
                failures.Add("Required player input actions are missing.");
            }

            if (GameObject.Find("[UI] GracePeriod")?.GetComponent<GracePeriodView>() == null ||
                GameObject.Find("[UI] FuseMission")?.GetComponent<FuseMissionView>() == null ||
                GameObject.Find("[UI] NoiseAlert")?.GetComponent<NoiseAlertView>() == null ||
                GameObject.Find("[UI] MonsterBiteAlert")?
                    .GetComponent<MonsterBiteAlertView>() == null ||
                GameObject.Find("[UI] InfectionHud")?.GetComponent<InfectionHudView>() == null)
            {
                failures.Add("One or more local gameplay HUD presenters are missing.");
            }

            var gameplayFeel = GameObject.Find("[UI] GameplayFeel")?
                .GetComponent<GameplayFeelView>();
            if (gameplayFeel == null ||
                gameplayFeel.WorldCamera == null ||
                gameplayFeel.RoomCount != RoomDefinitions.Length ||
                gameplayFeel.StationCount != RoomDefinitions.Length)
            {
                failures.Add(
                    "The integrated gameplay feedback presentation is incomplete.");
            }

            var upgradeStations =
                UnityEngine.Object.FindObjectsByType<UpgradeStationPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var hasEveryUpgradeAxis =
                upgradeStations.Length == 3 &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Scent) &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Population) &&
                Array.Exists(
                    upgradeStations,
                    item => item.Axis == UpgradeAxis.Toxicity);
            if (!hasEveryUpgradeAxis ||
                Array.Exists(
                    upgradeStations,
                    item =>
                        item.Config == null ||
                        item.GetComponent<Collider2D>() == null ||
                        item.GetComponent<NetworkUpgradeStationAuthority>() ==
                            null))
            {
                failures.Add("The villain upgrade stations are incomplete.");
            }

            var upgradeAuthority =
                GameObject.Find("[Network] VillainUpgradeAuthority")?
                    .GetComponent<NetworkVillainUpgradeAuthority>();
            var populationSpawner =
                GameObject.Find("[Network] MonsterPopulationSpawner")?
                    .GetComponent<NetworkMonsterPopulationSpawner>();
            if (upgradeAuthority == null || upgradeAuthority.Config == null ||
                populationSpawner == null ||
                populationSpawner.TierConfig == null ||
                !populationSpawner.MatchesBalanceTable(0) ||
                !populationSpawner.MatchesBalanceTable(1) ||
                !populationSpawner.MatchesBalanceTable(2))
            {
                failures.Add(
                    "The villain upgrade authority setup does not match the monster tier table.");
            }

            if (GameObject.Find("[UI] VillainUpgradeHud")?
                    .GetComponent<VillainUpgradeHudView>() == null)
            {
                failures.Add("The villain upgrade HUD presenter is missing.");
            }

            var clueMarkers =
                UnityEngine.Object.FindObjectsByType<ClueMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var clueAuthority =
                GameObject.Find("[Network] ClueAuthority")?
                    .GetComponent<NetworkClueAuthority>();
            if (clueMarkers.Length != 16 ||
                clueAuthority == null ||
                clueAuthority.MarkerCount != clueMarkers.Length ||
                Array.Exists(clueMarkers, marker => marker.IsActive) ||
                Array.Exists(
                    clueMarkers,
                    marker => string.IsNullOrEmpty(marker.RoomId)))
            {
                failures.Add("The scene clue setup is incomplete.");
            }

            var speakerAuthority =
                GameObject.Find("[Network] SpeakerAuthority")?
                    .GetComponent<NetworkSpeakerAuthority>();
            if (speakerAuthority == null ||
                speakerAuthority.Config == null ||
                speakerAuthority.SpeakerCount != RoomOrder.Length ||
                GameObject.Find("[UI] SpeakerRemote")?
                    .GetComponent<SpeakerRemoteView>() == null)
            {
                failures.Add("The speaker remote setup is incomplete.");
            }

            var meetingAuthorityObject =
                GameObject.Find("[Network] MeetingAuthority");
            var meetingChatAuthority = meetingAuthorityObject?
                .GetComponent<NetworkMeetingChatAuthority>();
            var ghostChatAuthority = meetingAuthorityObject?
                .GetComponent<NetworkGhostChatAuthority>();
            if (meetingAuthorityObject?
                    .GetComponent<NetworkMeetingAuthority>() == null ||
                meetingChatAuthority == null ||
                meetingChatAuthority.Config == null ||
                ghostChatAuthority == null ||
                ghostChatAuthority.Config == null ||
                GameObject.Find("[UI] Meeting")?
                    .GetComponent<MeetingView>() == null ||
                GameObject.Find("[UI] GhostChat")?
                    .GetComponent<GhostChatView>() == null)
            {
                failures.Add("The meeting setup is incomplete.");
            }

            var securityTerminal =
                GameObject.Find("[Network] SecurityTerminalAuthority")?
                    .GetComponent<NetworkSecurityTerminalAuthority>();
            if (securityTerminal == null ||
                GameObject.Find("[UI] SecurityTerminal")?
                    .GetComponent<SecurityTerminalView>() == null ||
                GameObject.Find("Security_CctvTerminal")?
                    .GetComponent<SecurityTerminalPrototype>() == null ||
                GameObject.Find("[CCTV] LiveFeeds")?
                    .GetComponent<CctvFeedController>()?.FeedCount != 7)
            {
                failures.Add("The security terminal setup is incomplete.");
            }

            if (GameObject.Find("[World] ProjectMilestones")?
                    .GetComponent<ProjectMilestoneWorldPresenter>() == null ||
                GameObject.Find("[World] RoundEnding")?
                    .GetComponent<RoundEndingSequencePresenter>() == null)
            {
                failures.Add(
                    "The milestone or round ending world presentation is missing.");
            }

            var disconnectPolicy =
                GameObject.Find("[Network] DisconnectPolicy")?
                    .GetComponent<NetworkDisconnectPolicyAuthority>();
            if (GameObject.Find("[Network] RoundSummary")?
                    .GetComponent<NetworkRoundSummaryAuthority>() == null ||
                disconnectPolicy == null ||
                disconnectPolicy.Config == null ||
                GameObject.Find("[Network] SessionWatchdog")?
                    .GetComponent<NetworkSessionWatchdog>() == null ||
                GameObject.Find("[UI] PlayerNameTags")?
                    .GetComponent<PlayerNameTagView>() == null ||
                GameObject.Find("[UI] MissionJournal")?
                    .GetComponent<MissionJournalView>() == null)
            {
                failures.Add(
                    "The round summary, disconnect policy, session watchdog, " +
                    "player name tag or mission journal setup is missing.");
            }

            ValidateAntidoteEconomy(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, failures));
            }

            Debug.Log("[MonkeyLab] 2D first playable validation passed.");
        }

        [MenuItem("Tools/Monkey Lab/Test Fuse Failure Noise")]
        public static void TestFuseFailureNoise()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing fuse failure noise.");
            }

            var player = GameObject.Find("P_Player_Local");
            var station = GameObject.Find("MissionStation_Power")?
                .GetComponent<FuseStationPrototype>();
            var nearbyMonster = GameObject.Find("P_Monster_01")?
                .GetComponent<MonsterBrain>();
            var secondNearbyMonster = GameObject.Find("P_Monster_02")?
                .GetComponent<MonsterBrain>();
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            var securityRoom = GameObject.Find("Room_Security");
            var powerRoom = GameObject.Find("Room_Power");
            if (player == null || station == null || nearbyMonster == null ||
                secondNearbyMonster == null || securityRoom == null ||
                powerRoom == null ||
                roundPhase == null)
            {
                throw new InvalidOperationException(
                    "Runtime fuse noise test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            nearbyMonster.transform.position = securityRoom.transform.position;
            secondNearbyMonster.transform.position = powerRoom.transform.position;
            station.Interact(player);
            if (!station.IsMissionActive || station.RequiredOrder.Count == 0)
            {
                throw new InvalidOperationException(
                    "Fuse mission did not start during the runtime test.");
            }

            var expectedFuseId = station.RequiredOrder[0];
            station.SubmitFuse(expectedFuseId == 1 ? 2 : 1);
            if (nearbyMonster.State != MonsterState.InvestigateNoise ||
                secondNearbyMonster.State != MonsterState.InvestigateNoise)
            {
                throw new InvalidOperationException(
                    "Every monster inside the Medium path radius must investigate " +
                    $"the fuse noise. Current states: {nearbyMonster.State}, " +
                    $"{secondNearbyMonster.State}.");
            }

            Debug.Log(
                "[MonkeyLab] 2D fuse noise validation passed: " +
                $"noise={nearbyMonster.CurrentNoiseId}, responders=2.");
        }

        [MenuItem("Tools/Monkey Lab/Test Monster Chase And Bite")]
        public static void TestMonsterChaseAndBite()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing monster chase and bite.");
            }

            StopRuntimeMonsterTest();
            var player = GameObject.Find("P_Player_Local");
            var target = player?.GetComponent<MonsterTarget>();
            var playerCollider = player?.GetComponent<Collider2D>();
            var monster = GameObject.Find("P_Monster_01");
            var body = monster?.GetComponent<Rigidbody2D>();
            var monsterCollider = monster?.GetComponent<Collider2D>();
            var brain = monster?.GetComponent<MonsterBrain>();
            var infectionService = player?.GetComponent<InfectionService>();
            var roundPhase = GameObject.Find("[Gameplay] LocalRoundPhase")?
                .GetComponent<LocalRoundPhasePrototype>();
            if (player == null || target == null || playerCollider == null ||
                monster == null || body == null || monsterCollider == null ||
                brain == null || infectionService == null || roundPhase == null)
            {
                throw new InvalidOperationException(
                    "Runtime monster chase and bite test objects are missing.");
            }

            roundPhase.SkipGracePeriodForDevelopment();
            var centerSeparation = Mathf.Max(
                0.2f,
                brain.Senses.TierRuntime.CurrentProximityDetectionRadius -
                0.1f);
            var desiredPosition = (Vector2)player.transform.position -
                                  Vector2.up * centerSeparation;
            monster.transform.position = desiredPosition;
            body.position = desiredPosition;
            body.rotation = 0f;
            brain.Senses.SetFacingDirection(Vector2.up);
            Physics2D.SyncTransforms();
            var initiallyDetected = brain.Senses.TryDetectTarget(
                out var initialDetectionType);
            Debug.Log(
                $"[MonkeyLab] Runtime monster test placed monster={body.position}, " +
                $"target={player.transform.position}, detected={initiallyDetected}, " +
                $"detection={initialDetectionType}, " +
                $"pathClear={brain.Senses.HasClearPathToTarget()}, " +
                $"blocker={brain.Senses.LastPathBlocker?.name ?? "none"}.");
            if (!initiallyDetected)
            {
                throw new InvalidOperationException(
                    "Runtime monster test placement could not detect the target.");
            }

            _runtimeTestMonster = brain;
            _runtimeTestTarget = target;
            _runtimeTestInfection = infectionService;
            _runtimeTestInitialBiteCount = target.BiteCount;
            _runtimeTestStartedAt = EditorApplication.timeSinceStartup;
            _runtimeTestObservedChase = false;
            _runtimeTestObservedPatrolAfterBite = false;
            brain.StateChanged += HandleRuntimeMonsterStateChanged;
            EditorApplication.update += MonitorRuntimeMonsterTest;
        }

        [MenuItem("Tools/Monkey Lab/Test Infection And Antidote")]
        public static void TestInfectionAndAntidote()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before testing infection and antidote use.");
            }

            StopRuntimeAntidoteTest();
            var player = GameObject.Find("P_Player_Local");
            var target = player?.GetComponent<MonsterTarget>();
            var infectionService = player?.GetComponent<InfectionService>();
            var antidoteService = player?.GetComponent<AntidoteService>();
            var tierRuntime = GameObject.Find("[Gameplay] MonsterTierRuntime")?
                .GetComponent<MonsterTierRuntime>();
            if (target == null || infectionService == null ||
                antidoteService == null || tierRuntime == null)
            {
                throw new InvalidOperationException(
                    "Runtime infection test objects are missing.");
            }

            if (infectionService.State != PlayerLifeState.AliveHealthy ||
                antidoteService.HasAntidote)
            {
                throw new InvalidOperationException(
                    "Start the infection test from a fresh Play Mode session.");
            }

            tierRuntime.SetToxicityTier(MonsterTierConfig.MinimumTier);
            if (!antidoteService.TryAddAntidote() ||
                !target.TryReceiveBite(null, Time.time, 0f) ||
                !infectionService.IsInfected ||
                !Mathf.Approximately(
                    infectionService.DurationAtBiteSeconds,
                    90f) ||
                !antidoteService.TryBeginUse(Time.time))
            {
                throw new InvalidOperationException(
                    "Infection or antidote use did not start correctly.");
            }

            _runtimeAntidoteTestInfection = infectionService;
            _runtimeAntidoteTestService = antidoteService;
            _runtimeAntidoteTestStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += MonitorRuntimeAntidoteTest;
        }

        private static Dictionary<string, RoomDefinition> BuildMap(Transform mapRoot)
        {
            var unitSprite = LoadSprite(UnitSpritePath);
            var rooms = new Dictionary<string, RoomDefinition>();
            var walkableAreas = new List<Rect>(
                RoomDefinitions.Length + CorridorDefinitions.Length * 4);
            foreach (var definition in RoomDefinitions)
            {
                rooms[definition.Id] = definition;
            }

            var corridorRoot = new GameObject("[Map] Corridors").transform;
            corridorRoot.SetParent(mapRoot);
            var collisionRoot =
                new GameObject("[Map] CollisionWalls").transform;
            collisionRoot.SetParent(mapRoot);
            foreach (var corridor in CorridorDefinitions)
            {
                CreateCorridor(
                    corridor,
                    unitSprite,
                    corridorRoot,
                    walkableAreas);
            }

            var floorRoot = new GameObject("[Map] Rooms").transform;
            floorRoot.SetParent(mapRoot);
            foreach (var room in RoomDefinitions)
            {
                var floorColor = GetRoomColor(room.Id);
                CreateSpriteObject(
                    "Room_" + room.Id,
                    unitSprite,
                    room.Position,
                    room.Size,
                    floorColor,
                    0,
                    floorRoot);
                CreateRoomLabel(room, floorRoot);
                walkableAreas.Add(CreateRect(room.Position, room.Size));
            }

            CreateCollisionBoundary(
                walkableAreas,
                unitSprite,
                collisionRoot);
            CreateAutomaticDoors(mapRoot, unitSprite);
            CreateEnvironmentProps(mapRoot, rooms, unitSprite);
            CreateCorridorGuideFixtures(mapRoot, unitSprite);
            CreateSpawnMarkers(mapRoot, rooms);
            return rooms;
        }

        private static void CreateCorridor(
            CorridorDefinition definition,
            Sprite unitSprite,
            Transform floorRoot,
            List<Rect> walkableAreas)
        {
            var name = definition.A + "_to_" + definition.B;
            var path = definition.PathPoints;
            for (var index = 1; index < path.Count; index++)
            {
                CreateCorridorSegment(
                    name,
                    index - 1,
                    path[index - 1],
                    path[index],
                    unitSprite,
                    floorRoot,
                    walkableAreas);
            }

            for (var index = 1; index < path.Count - 1; index++)
            {
                CreateSpriteObject(
                    $"CorridorJoint_{name}_{index:00}",
                    unitSprite,
                    path[index],
                    new Vector2(CorridorWidth, CorridorWidth),
                    new Color(0.10f, 0.17f, 0.23f),
                    0,
                    floorRoot);
                walkableAreas.Add(CreateRect(
                    path[index],
                    new Vector2(CorridorWidth, CorridorWidth)));
            }
        }

        private static void CreateCorridorSegment(
            string name,
            int segmentIndex,
            Vector2 start,
            Vector2 end,
            Sprite unitSprite,
            Transform floorRoot,
            List<Rect> walkableAreas)
        {
            var length = Vector2.Distance(start, end);
            if (length <= 0.01f)
            {
                return;
            }

            var midpoint = (start + end) * 0.5f;
            var delta = end - start;
            var isHorizontal = Mathf.Abs(delta.y) <= 0.001f;
            var isVertical = Mathf.Abs(delta.x) <= 0.001f;
            if (!isHorizontal && !isVertical)
            {
                throw new InvalidOperationException(
                    $"Corridor {name} segment {segmentIndex} is not axis aligned.");
            }

            var walkableSize = isHorizontal
                ? new Vector2(length, CorridorWidth)
                : new Vector2(CorridorWidth, length);
            var renderSize = isHorizontal
                ? new Vector2(length + 0.08f, CorridorWidth)
                : new Vector2(CorridorWidth, length + 0.08f);
            CreateSpriteObject(
                $"Corridor_{name}_{segmentIndex:00}",
                unitSprite,
                midpoint,
                renderSize,
                new Color(0.10f, 0.17f, 0.23f),
                0,
                floorRoot);
            walkableAreas.Add(CreateRect(midpoint, walkableSize));
        }

        private static Rect CreateRect(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        private static void CreateCollisionBoundary(
            IReadOnlyList<Rect> walkableAreas,
            Sprite unitSprite,
            Transform parent)
        {
            var xCoordinates = new List<float>(walkableAreas.Count * 2);
            var yCoordinates = new List<float>(walkableAreas.Count * 2);
            foreach (var area in walkableAreas)
            {
                AddDistinctCoordinate(xCoordinates, area.xMin);
                AddDistinctCoordinate(xCoordinates, area.xMax);
                AddDistinctCoordinate(yCoordinates, area.yMin);
                AddDistinctCoordinate(yCoordinates, area.yMax);
            }

            xCoordinates.Sort();
            yCoordinates.Sort();
            var walkable = new bool[
                xCoordinates.Count - 1,
                yCoordinates.Count - 1];
            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    var midpoint = new Vector2(
                        (xCoordinates[x] + xCoordinates[x + 1]) * 0.5f,
                        (yCoordinates[y] + yCoordinates[y + 1]) * 0.5f);
                    walkable[x, y] =
                        IsPointInsideAnyArea(midpoint, walkableAreas);
                }
            }

            var edges = new List<BoundaryEdge>(walkableAreas.Count * 8);
            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    if (!walkable[x, y])
                    {
                        continue;
                    }

                    var xMin = xCoordinates[x];
                    var xMax = xCoordinates[x + 1];
                    var yMin = yCoordinates[y];
                    var yMax = yCoordinates[y + 1];
                    if (x == 0 || !walkable[x - 1, y])
                    {
                        edges.Add(new BoundaryEdge(
                            false,
                            xMin,
                            yMin,
                            yMax));
                    }

                    if (x == xCoordinates.Count - 2 ||
                        !walkable[x + 1, y])
                    {
                        edges.Add(new BoundaryEdge(
                            false,
                            xMax,
                            yMin,
                            yMax));
                    }

                    if (y == 0 || !walkable[x, y - 1])
                    {
                        edges.Add(new BoundaryEdge(
                            true,
                            yMin,
                            xMin,
                            xMax));
                    }

                    if (y == yCoordinates.Count - 2 ||
                        !walkable[x, y + 1])
                    {
                        edges.Add(new BoundaryEdge(
                            true,
                            yMax,
                            xMin,
                            xMax));
                    }
                }
            }

            edges.Sort(CompareBoundaryEdges);
            var mergedEdges = MergeBoundaryEdges(edges);
            for (var index = 0; index < mergedEdges.Count; index++)
            {
                CreateBoundaryWall(
                    mergedEdges[index],
                    index,
                    unitSprite,
                    parent);
            }
        }

        private static void AddDistinctCoordinate(
            List<float> coordinates,
            float value)
        {
            foreach (var existing in coordinates)
            {
                if (Mathf.Approximately(existing, value))
                {
                    return;
                }
            }

            coordinates.Add(value);
        }

        private static bool IsPointInsideAnyArea(
            Vector2 point,
            IReadOnlyList<Rect> areas)
        {
            foreach (var area in areas)
            {
                if (point.x > area.xMin && point.x < area.xMax &&
                    point.y > area.yMin && point.y < area.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareBoundaryEdges(
            BoundaryEdge left,
            BoundaryEdge right)
        {
            var orientationComparison =
                left.IsHorizontal.CompareTo(right.IsHorizontal);
            if (orientationComparison != 0)
            {
                return orientationComparison;
            }

            var fixedComparison =
                left.FixedCoordinate.CompareTo(right.FixedCoordinate);
            return fixedComparison != 0
                ? fixedComparison
                : left.Start.CompareTo(right.Start);
        }

        private static List<BoundaryEdge> MergeBoundaryEdges(
            IReadOnlyList<BoundaryEdge> sortedEdges)
        {
            var merged = new List<BoundaryEdge>(sortedEdges.Count);
            foreach (var edge in sortedEdges)
            {
                if (merged.Count == 0)
                {
                    merged.Add(edge);
                    continue;
                }

                var previous = merged[^1];
                if (previous.IsHorizontal == edge.IsHorizontal &&
                    Mathf.Approximately(
                        previous.FixedCoordinate,
                        edge.FixedCoordinate) &&
                    edge.Start <= previous.End + 0.001f)
                {
                    merged[^1] = new BoundaryEdge(
                        previous.IsHorizontal,
                        previous.FixedCoordinate,
                        previous.Start,
                        Mathf.Max(previous.End, edge.End));
                    continue;
                }

                merged.Add(edge);
            }

            return merged;
        }

        private static void CreateBoundaryWall(
            BoundaryEdge edge,
            int index,
            Sprite sprite,
            Transform parent)
        {
            var length = edge.End - edge.Start;
            var center = (edge.Start + edge.End) * 0.5f;
            var position = edge.IsHorizontal
                ? new Vector2(center, edge.FixedCoordinate)
                : new Vector2(edge.FixedCoordinate, center);
            var size = edge.IsHorizontal
                ? new Vector2(length + WallThickness, WallThickness)
                : new Vector2(WallThickness, length + WallThickness);
            CreateWall(
                $"Wall_Boundary_{index:000}",
                position,
                size,
                sprite,
                parent);
        }

        private static GameObject CreateWall(
            string name,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Transform parent)
        {
            var wall = CreateSpriteObject(
                name,
                sprite,
                position,
                size,
                new Color(0.045f, 0.09f, 0.13f),
                20,
                parent);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return wall;
        }

        private static void CreateRoomLabel(
            RoomDefinition room,
            Transform parent)
        {
            var labelObject = new GameObject("Label_" + room.Id);
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(
                room.Position.x,
                room.Position.y + room.Size.y * 0.36f,
                0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException(
                    "Unity built-in LegacyRuntime font could not be loaded.");
            }

            var label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.text = room.DisplayName;
            label.fontSize = 56;
            label.characterSize = 0.085f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.55f, 0.78f, 0.84f, 0.85f);
            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 3;
        }

        private static void CreateEnvironmentProps(
            Transform mapRoot,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            Sprite unitSprite)
        {
            var propRoot =
                new GameObject("[Map] EnvironmentProps").transform;
            propRoot.SetParent(mapRoot);
            var roomRoots = new Dictionary<string, Transform>(
                RoomDefinitions.Length);
            foreach (var room in RoomDefinitions)
            {
                var roomRoot = new GameObject(
                    $"[Props] {room.Id}").transform;
                roomRoot.SetParent(propRoot);
                roomRoots.Add(room.Id, roomRoot);
            }

            for (var index = 0;
                 index < EnvironmentPropDefinitions.Length;
                 index++)
            {
                var definition = EnvironmentPropDefinitions[index];
                if (!rooms.TryGetValue(definition.RoomId, out var room))
                {
                    throw new InvalidOperationException(
                        $"Environment prop {definition.AssetKey} references unknown room {definition.RoomId}.");
                }

                CreateEnvironmentProp(
                    roomRoots[definition.RoomId],
                    definition.RoomId,
                    definition.AssetKey,
                    index,
                    room.Position + definition.LocalPosition,
                    definition.Footprint,
                    GetEnvironmentPropColor(definition.Category),
                    definition.IsObstacle,
                    unitSprite,
                    showLabel: true,
                    definition.MountKind,
                    definition.SortingOrder,
                    definition.HasStatusIndicator);
            }

            foreach (var room in RoomDefinitions)
            {
                CreateCommonRoomFixtures(
                    room,
                    roomRoots[room.Id],
                    unitSprite);
                CreateRoomArchitectureFixtures(
                    room,
                    roomRoots[room.Id],
                    unitSprite);
            }
        }

        private static void CreateCommonRoomFixtures(
            RoomDefinition room,
            Transform parent,
            Sprite unitSprite)
        {
            var halfSize = room.Size * 0.5f;
            var positions = new[]
            {
                room.Position + new Vector2(
                    -halfSize.x + 0.7f,
                    halfSize.y - 0.75f),
                room.Position + new Vector2(
                    halfSize.x - 0.7f,
                    halfSize.y - 0.75f),
                room.Position + new Vector2(
                    -halfSize.x + 0.7f,
                    -halfSize.y + 0.75f),
                room.Position + new Vector2(
                    halfSize.x - 0.7f,
                    -halfSize.y + 0.75f)
            };
            var assetKeys = new[]
            {
                "SM_WallMonitor",
                "SM_FireExtinguisher",
                "SM_TrashBin",
                "SM_EmergencyPhone"
            };
            var sizes = new[]
            {
                new Vector2(1.1f, 0.55f),
                new Vector2(0.45f, 0.7f),
                new Vector2(0.65f, 0.65f),
                new Vector2(0.55f, 0.7f)
            };
            var categories = new[]
            {
                EnvironmentPropCategory.Security,
                EnvironmentPropCategory.Hazard,
                EnvironmentPropCategory.Common,
                EnvironmentPropCategory.Security
            };
            for (var index = 0; index < assetKeys.Length; index++)
            {
                CreateEnvironmentProp(
                    parent,
                    room.Id,
                    assetKeys[index],
                    index,
                    positions[index],
                    sizes[index],
                    GetEnvironmentPropColor(categories[index]),
                    isObstacle: false,
                    unitSprite,
                    showLabel: false);
            }
        }

        private static void CreateRoomArchitectureFixtures(
            RoomDefinition room,
            Transform parent,
            Sprite unitSprite)
        {
            var architectureRoot = new GameObject("[Architecture]").transform;
            architectureRoot.SetParent(parent);
            var halfSize = room.Size * 0.5f;
            var trimColor = new Color(0.12f, 0.27f, 0.31f, 0.82f);
            var cornerColor = new Color(0.66f, 0.47f, 0.12f, 0.88f);
            var laneColor = new Color(0.12f, 0.45f, 0.48f, 0.10f);
            var trimDefinitions = new[]
            {
                ("SM_WallTrim_North",
                    room.Position + new Vector2(0f, halfSize.y - 0.24f),
                    new Vector2(room.Size.x - 0.7f, 0.18f)),
                ("SM_WallTrim_South",
                    room.Position + new Vector2(0f, -halfSize.y + 0.24f),
                    new Vector2(room.Size.x - 0.7f, 0.18f)),
                ("SM_WallTrim_West",
                    room.Position + new Vector2(-halfSize.x + 0.24f, 0f),
                    new Vector2(0.18f, room.Size.y - 0.7f)),
                ("SM_WallTrim_East",
                    room.Position + new Vector2(halfSize.x - 0.24f, 0f),
                    new Vector2(0.18f, room.Size.y - 0.7f))
            };
            for (var index = 0; index < trimDefinitions.Length; index++)
            {
                var trim = trimDefinitions[index];
                CreateEnvironmentProp(
                    architectureRoot,
                    room.Id,
                    trim.Item1,
                    index,
                    trim.Item2,
                    trim.Item3,
                    trimColor,
                    isObstacle: false,
                    unitSprite,
                    showLabel: false,
                    EnvironmentPropMountKind.WallMounted,
                    sortingOrder: 4);
            }

            var cornerPositions = new[]
            {
                room.Position + new Vector2(-halfSize.x + 0.42f, halfSize.y - 0.42f),
                room.Position + new Vector2(halfSize.x - 0.42f, halfSize.y - 0.42f),
                room.Position + new Vector2(-halfSize.x + 0.42f, -halfSize.y + 0.42f),
                room.Position + new Vector2(halfSize.x - 0.42f, -halfSize.y + 0.42f)
            };
            for (var index = 0; index < cornerPositions.Length; index++)
            {
                CreateEnvironmentProp(
                    architectureRoot,
                    room.Id,
                    "SM_CornerGuard",
                    index,
                    cornerPositions[index],
                    new Vector2(0.34f, 0.34f),
                    cornerColor,
                    isObstacle: false,
                    unitSprite,
                    showLabel: false,
                    EnvironmentPropMountKind.WallMounted,
                    sortingOrder: 5);
            }

            CreateEnvironmentProp(
                architectureRoot,
                room.Id,
                "VFX_CirculationLane_Horizontal",
                0,
                room.Position,
                new Vector2(Mathf.Max(2f, room.Size.x - 1.8f), 2.2f),
                laneColor,
                isObstacle: false,
                unitSprite,
                showLabel: false,
                EnvironmentPropMountKind.FloorDecal,
                sortingOrder: 1);
            CreateEnvironmentProp(
                architectureRoot,
                room.Id,
                "VFX_CirculationLane_Vertical",
                1,
                room.Position,
                new Vector2(2.2f, Mathf.Max(2f, room.Size.y - 1.8f)),
                laneColor,
                isObstacle: false,
                unitSprite,
                showLabel: false,
                EnvironmentPropMountKind.FloorDecal,
                sortingOrder: 1);
        }

        private static EnvironmentPropSlot CreateEnvironmentProp(
            Transform parent,
            string roomId,
            string assetKey,
            int instanceIndex,
            Vector2 position,
            Vector2 footprint,
            Color color,
            bool isObstacle,
            Sprite unitSprite,
            bool showLabel,
            EnvironmentPropMountKind mountKind =
                EnvironmentPropMountKind.FloorStanding,
            int sortingOrder = 8,
            bool hasStatusIndicator = false)
        {
            var root = new GameObject(
                $"PROP_{roomId}_{assetKey}_{instanceIndex:00}");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var placeholderRenderers = new List<SpriteRenderer>(4);
            if (isObstacle && mountKind == EnvironmentPropMountKind.FloorStanding)
            {
                var shadow = CreateSpriteObject(
                    "PlaceholderShadow",
                    unitSprite,
                    position + new Vector2(0.10f, -0.10f),
                    footprint + new Vector2(0.16f, 0.16f),
                    new Color(0f, 0f, 0f, 0.32f),
                    sortingOrder - 1,
                    root.transform);
                placeholderRenderers.Add(shadow.GetComponent<SpriteRenderer>());
            }

            var visual = CreateSpriteObject(
                "PlaceholderVisual",
                unitSprite,
                position,
                footprint,
                color,
                sortingOrder,
                root.transform);
            var mainRenderer = visual.GetComponent<SpriteRenderer>();
            placeholderRenderers.Add(mainRenderer);
            if ((mountKind is EnvironmentPropMountKind.FloorStanding or
                 EnvironmentPropMountKind.WallMounted) &&
                footprint.x >= 0.55f && footprint.y >= 0.55f)
            {
                var trimHeight = Mathf.Min(0.16f, footprint.y * 0.18f);
                var trimPosition = position + new Vector2(
                    0f,
                    footprint.y * 0.5f - trimHeight * 0.8f);
                var trim = CreateSpriteObject(
                    "PlaceholderTrim",
                    unitSprite,
                    trimPosition,
                    new Vector2(
                        Mathf.Max(0.2f, footprint.x * 0.78f),
                        trimHeight),
                    Color.Lerp(color, Color.white, 0.28f),
                    sortingOrder + 1,
                    root.transform);
                placeholderRenderers.Add(trim.GetComponent<SpriteRenderer>());
            }

            if (hasStatusIndicator)
            {
                var indicator = CreateSpriteObject(
                    "PlaceholderStatusIndicator",
                    unitSprite,
                    position + new Vector2(
                        footprint.x * 0.32f,
                        footprint.y * 0.28f),
                    new Vector2(0.20f, 0.20f),
                    new Color(0.20f, 0.95f, 0.76f, 1f),
                    sortingOrder + 2,
                    root.transform);
                var indicatorRenderer = indicator.GetComponent<SpriteRenderer>();
                indicatorRenderer.sharedMaterial = GetIndicatorUnlitMaterial();
                placeholderRenderers.Add(indicatorRenderer);
            }

            if (isObstacle)
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = footprint * 0.88f;
            }

            var replacementAnchor = new GameObject("ReplacementAnchor").transform;
            replacementAnchor.SetParent(root.transform);
            replacementAnchor.localPosition = Vector3.zero;
            var slot = root.AddComponent<EnvironmentPropSlot>();
            slot.ConfigureDetailed(
                roomId,
                assetKey,
                footprint,
                isObstacle,
                mountKind,
                sortingOrder,
                replacementAnchor,
                mainRenderer,
                placeholderRenderers.ToArray());
            if (showLabel)
            {
                CreateEnvironmentPropLabel(root.transform, assetKey);
            }

            return slot;
        }

        private static void CreateEnvironmentPropLabel(
            Transform parent,
            string assetKey)
        {
            var labelObject = new GameObject("PlaceholderLabel");
            labelObject.transform.SetParent(parent);
            labelObject.transform.localPosition = Vector3.zero;
            var font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            if (font == null)
            {
                return;
            }

            var label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.text = assetKey
                .Replace("SM_", string.Empty)
                .Replace("VFX_", string.Empty);
            label.fontSize = 24;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.88f, 0.94f, 0.96f, 0.9f);
            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 9;
            renderer.enabled = false;
        }

        private static Color GetEnvironmentPropColor(
            EnvironmentPropCategory category)
        {
            return category switch
            {
                EnvironmentPropCategory.Common =>
                    new Color(0.38f, 0.45f, 0.49f),
                EnvironmentPropCategory.Laboratory =>
                    new Color(0.34f, 0.67f, 0.69f),
                EnvironmentPropCategory.Medical =>
                    new Color(0.69f, 0.81f, 0.82f),
                EnvironmentPropCategory.Storage =>
                    new Color(0.28f, 0.52f, 0.58f),
                EnvironmentPropCategory.Security =>
                    new Color(0.24f, 0.48f, 0.72f),
                EnvironmentPropCategory.Power =>
                    new Color(0.82f, 0.60f, 0.16f),
                EnvironmentPropCategory.Quarantine =>
                    new Color(0.32f, 0.66f, 0.74f, 0.82f),
                EnvironmentPropCategory.Utility =>
                    new Color(0.48f, 0.53f, 0.56f),
                EnvironmentPropCategory.Hazard =>
                    new Color(0.78f, 0.20f, 0.23f, 0.88f),
                _ => Color.gray
            };
        }

        private static void CreateCorridorGuideFixtures(
            Transform mapRoot,
            Sprite unitSprite)
        {
            var root = new GameObject(
                "[Map] CorridorFixtures").transform;
            root.SetParent(mapRoot);
            var fixtureIndex = 0;
            foreach (var corridor in CorridorDefinitions)
            {
                for (var pointIndex = 1;
                     pointIndex < corridor.PathPoints.Count;
                     pointIndex++)
                {
                    var start = corridor.PathPoints[pointIndex - 1];
                    var end = corridor.PathPoints[pointIndex];
                    var delta = end - start;
                    var isHorizontal = Mathf.Abs(delta.y) <= 0.001f;
                    var distance = delta.magnitude;
                    var guideCount = Mathf.Max(1, Mathf.FloorToInt(distance / 4f));
                    for (var guideIndex = 0;
                         guideIndex < guideCount;
                         guideIndex++)
                    {
                        var normalized = (guideIndex + 1f) / (guideCount + 1f);
                        var guideFixture = CreateEnvironmentProp(
                            root,
                            "Corridor",
                            "VFX_FloorGuideLight",
                            fixtureIndex++,
                            Vector2.Lerp(start, end, normalized),
                            isHorizontal
                                ? new Vector2(0.9f, 0.18f)
                                : new Vector2(0.18f, 0.9f),
                            new Color(0.06f, 0.22f, 0.24f, 0.28f),
                            isObstacle: false,
                            unitSprite,
                            showLabel: false,
                            EnvironmentPropMountKind.FloorDecal,
                            sortingOrder: 2);
                        guideFixture.PlaceholderRenderer.sharedMaterial =
                            GetIndicatorUnlitMaterial();
                    }

                    var conduitOffset = isHorizontal
                        ? new Vector2(0f, CorridorWidth * 0.5f - 0.28f)
                        : new Vector2(CorridorWidth * 0.5f - 0.28f, 0f);
                    CreateEnvironmentProp(
                        root,
                        "Corridor",
                        "SM_CorridorUtilityConduit",
                        fixtureIndex++,
                        (start + end) * 0.5f + conduitOffset,
                        isHorizontal
                            ? new Vector2(Mathf.Max(0.6f, distance - 0.5f), 0.16f)
                            : new Vector2(0.16f, Mathf.Max(0.6f, distance - 0.5f)),
                        new Color(0.24f, 0.31f, 0.34f, 0.82f),
                        isObstacle: false,
                        unitSprite,
                        showLabel: false,
                        EnvironmentPropMountKind.WallMounted,
                        sortingOrder: 4);
                }

                for (var pointIndex = 1;
                     pointIndex < corridor.PathPoints.Count - 1;
                     pointIndex++)
                {
                    CreateEnvironmentProp(
                        root,
                        "Corridor",
                        "SM_CorridorDirectionSign",
                        fixtureIndex++,
                        corridor.PathPoints[pointIndex],
                        new Vector2(0.75f, 0.75f),
                        new Color(0.87f, 0.61f, 0.18f),
                        isObstacle: false,
                        unitSprite,
                        showLabel: false,
                        EnvironmentPropMountKind.Overhead,
                        sortingOrder: 6,
                        hasStatusIndicator: true);
                }
            }
        }

        private static void CreateAutomaticDoors(
            Transform mapRoot,
            Sprite unitSprite)
        {
            var doorRoot = new GameObject(
                "[Map] AutomaticDoors").transform;
            doorRoot.SetParent(mapRoot);
            var config = EnsureDoorBalanceConfig();
            var createdDoorways = new HashSet<string>();
            var doorIndex = 0;
            foreach (var corridor in CorridorDefinitions)
            {
                TryCreateAutomaticDoor(
                    corridor.A,
                    corridor.B,
                    corridor.SideA,
                    corridor.Start,
                    ref doorIndex,
                    createdDoorways,
                    doorRoot,
                    unitSprite,
                    config);
                TryCreateAutomaticDoor(
                    corridor.B,
                    corridor.A,
                    corridor.SideB,
                    corridor.End,
                    ref doorIndex,
                    createdDoorways,
                    doorRoot,
                    unitSprite,
                    config);
            }
        }

        private static void TryCreateAutomaticDoor(
            string roomId,
            string connectedRoomId,
            WallSide wallSide,
            Vector2 position,
            ref int doorIndex,
            HashSet<string> createdDoorways,
            Transform parent,
            Sprite unitSprite,
            DoorBalanceConfig config)
        {
            var doorwayKey = GetDoorwayKey(roomId, wallSide, position);
            if (!createdDoorways.Add(doorwayKey))
            {
                return;
            }

            var root = new GameObject(
                $"Door_{doorIndex:00}_{roomId}_to_{connectedRoomId}");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var isHorizontalWall =
                wallSide is WallSide.North or WallSide.South;
            var panelSpan = (CorridorWidth - 0.24f) * 0.5f;
            var panelSize = isHorizontalWall
                ? new Vector2(panelSpan, 0.55f)
                : new Vector2(0.55f, panelSpan);
            var panelOffset = isHorizontalWall
                ? new Vector2(panelSpan * 0.5f, 0f)
                : new Vector2(0f, panelSpan * 0.5f);
            var panelA = CreateSpriteObject(
                "Panel_A",
                unitSprite,
                position - panelOffset,
                panelSize,
                new Color(0.26f, 0.62f, 0.70f),
                35,
                root.transform);
            var panelB = CreateSpriteObject(
                "Panel_B",
                unitSprite,
                position + panelOffset,
                panelSize,
                new Color(0.26f, 0.62f, 0.70f),
                35,
                root.transform);

            var frameAxis = isHorizontalWall
                ? Vector2.right
                : Vector2.up;
            var frameOffset = frameAxis *
                              (CorridorWidth * 0.5f + 0.16f);
            var frameSize = isHorizontalWall
                ? new Vector2(0.34f, 1.35f)
                : new Vector2(1.35f, 0.34f);
            var frameA = CreateSpriteObject(
                "Frame_A",
                unitSprite,
                position - frameOffset,
                frameSize,
                new Color(0.08f, 0.12f, 0.15f),
                36,
                root.transform);
            var frameB = CreateSpriteObject(
                "Frame_B",
                unitSprite,
                position + frameOffset,
                frameSize,
                new Color(0.08f, 0.12f, 0.15f),
                36,
                root.transform);
            var indicatorSize = isHorizontalWall
                ? new Vector2(0.12f, 0.65f)
                : new Vector2(0.65f, 0.12f);
            var statusIndicators = new[]
            {
                CreateSpriteObject(
                    "Status_A",
                    unitSprite,
                    position - frameOffset,
                    indicatorSize,
                    new Color(0.10f, 0.62f, 0.72f),
                    37,
                    root.transform).GetComponent<SpriteRenderer>(),
                CreateSpriteObject(
                    "Status_B",
                    unitSprite,
                    position + frameOffset,
                    indicatorSize,
                    new Color(0.10f, 0.62f, 0.72f),
                    37,
                    root.transform).GetComponent<SpriteRenderer>()
            };
            foreach (var statusIndicator in statusIndicators)
            {
                statusIndicator.sharedMaterial =
                    GetIndicatorUnlitMaterial();
            }

            var sensor = root.AddComponent<BoxCollider2D>();
            sensor.isTrigger = true;
            sensor.size = isHorizontalWall
                ? new Vector2(CorridorWidth, config.SensorDepthMeters)
                : new Vector2(config.SensorDepthMeters, CorridorWidth);
            var blockerObject = new GameObject("DoorBlocker");
            blockerObject.transform.SetParent(root.transform);
            blockerObject.transform.localPosition = Vector3.zero;
            var blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.size = isHorizontalWall
                ? new Vector2(CorridorWidth - 0.2f, 0.38f)
                : new Vector2(0.38f, CorridorWidth - 0.2f);

            var motor = root.AddComponent<AutomaticDoorMotor>();
            motor.Configure(
                panelA.transform,
                panelB.transform,
                blocker,
                config,
                frameAxis,
                statusIndicators);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkAutomaticDoorAuthority>()
                .Configure(motor, sensor, config);
            root.AddComponent<EnvironmentPropSlot>().ConfigureDetailed(
                roomId,
                "P_AutomaticDoor",
                isHorizontalWall
                    ? new Vector2(CorridorWidth, 0.55f)
                    : new Vector2(0.55f, CorridorWidth),
                isObstacle: true,
                EnvironmentPropMountKind.DoorAssembly,
                sortingOrder: 35,
                root.transform,
                panelA.GetComponent<SpriteRenderer>(),
                new[]
                {
                    panelA.GetComponent<SpriteRenderer>(),
                    panelB.GetComponent<SpriteRenderer>(),
                    frameA.GetComponent<SpriteRenderer>(),
                    frameB.GetComponent<SpriteRenderer>(),
                    statusIndicators[0],
                    statusIndicators[1]
                });
            doorIndex++;
        }

        private static string GetDoorwayKey(
            string roomId,
            WallSide wallSide,
            Vector2 position)
        {
            return $"{roomId}:{wallSide}:" +
                   $"{Mathf.RoundToInt(position.x * 100f)}:" +
                   $"{Mathf.RoundToInt(position.y * 100f)}";
        }

        private static int CountUniqueDoorways()
        {
            var doorwayKeys = new HashSet<string>();
            foreach (var corridor in CorridorDefinitions)
            {
                doorwayKeys.Add(GetDoorwayKey(
                    corridor.A,
                    corridor.SideA,
                    corridor.Start));
                doorwayKeys.Add(GetDoorwayKey(
                    corridor.B,
                    corridor.SideB,
                    corridor.End));
            }

            return doorwayKeys.Count;
        }

        private static bool HasDoorTriggerAndBlocker(GameObject door)
        {
            var colliders = door.GetComponentsInChildren<BoxCollider2D>(
                includeInactive: true);
            var hasTrigger = false;
            var hasBlocker = false;
            foreach (var collider in colliders)
            {
                if (collider.isTrigger)
                {
                    hasTrigger = true;
                }
                else
                {
                    hasBlocker = true;
                }
            }

            return hasTrigger && hasBlocker;
        }

        private static void CreateSpawnMarkers(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var spawnRoot = new GameObject("[Map] SpawnPoints").transform;
            spawnRoot.SetParent(parent);
            for (var index = 0; index < PlayerSpawnPositions.Length; index++)
            {
                var marker = new GameObject($"PlayerSpawn_{index + 1:00}");
                marker.transform.SetParent(spawnRoot);
                marker.transform.position = PlayerSpawnPositions[index];
            }

            for (var index = 0; index < MonsterSpawnRoomIds.Length; index++)
            {
                var marker = new GameObject($"MonsterSpawn_{index + 1:00}");
                marker.transform.SetParent(spawnRoot);
                marker.transform.position =
                    rooms[MonsterSpawnRoomIds[index]].Position;
            }
        }

        private static TopDownNavigationGraph CreateNavigationGraph(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var graphObject = new GameObject("[Navigation] Laboratory2D");
            graphObject.transform.SetParent(parent);
            var nodeRoot = new GameObject("Nodes").transform;
            nodeRoot.SetParent(graphObject.transform);
            var nodes = new List<Transform>(
                RoomOrder.Length + CorridorDefinitions.Length * 9);
            var roomIndices = new Dictionary<string, int>();
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                var roomId = RoomOrder[index];
                roomIndices[roomId] = nodes.Count;
                var node = new GameObject("Node_" + roomId);
                node.transform.SetParent(nodeRoot);
                node.transform.position = rooms[roomId].Position;
                nodes.Add(node.transform);
            }

            var links = new List<TopDownNavigationGraph.Link>(
                CorridorDefinitions.Length * 9);
            for (var index = 0;
                 index < CorridorDefinitions.Length;
                 index++)
            {
                var corridor = CorridorDefinitions[index];
                var corridorPath = corridor.PathPoints;
                var previousIndex = roomIndices[corridor.A];
                previousIndex = AddRoomNavigationApproach(
                    corridor.A,
                    corridor.SideA,
                    corridor.Start,
                    rooms[corridor.A],
                    previousIndex,
                    nodes,
                    links,
                    nodeRoot,
                    index,
                    "A");
                for (var pathIndex = 0;
                     pathIndex < corridorPath.Count;
                     pathIndex++)
                {
                    var pathNode = new GameObject(
                        $"Node_{corridor.A}_{corridor.B}_{pathIndex:00}");
                    pathNode.transform.SetParent(nodeRoot);
                    pathNode.transform.position = corridorPath[pathIndex];
                    var currentIndex = nodes.Count;
                    nodes.Add(pathNode.transform);
                    links.Add(new TopDownNavigationGraph.Link(
                        previousIndex,
                        currentIndex));
                    previousIndex = currentIndex;
                }

                AddRoomNavigationApproach(
                    corridor.B,
                    corridor.SideB,
                    corridor.End,
                    rooms[corridor.B],
                    previousIndex,
                    nodes,
                    links,
                    nodeRoot,
                    index,
                    "B",
                    reverseLinkOrder: true,
                    roomCenterIndex: roomIndices[corridor.B]);
            }

            var graph = graphObject.AddComponent<TopDownNavigationGraph>();
            graph.Configure(nodes.ToArray(), links.ToArray());
            return graph;
        }

        private static int AddRoomNavigationApproach(
            string roomId,
            WallSide wallSide,
            Vector2 doorwayPosition,
            RoomDefinition room,
            int previousIndex,
            List<Transform> nodes,
            List<TopDownNavigationGraph.Link> links,
            Transform parent,
            int corridorIndex,
            string endpointLabel,
            bool reverseLinkOrder = false,
            int roomCenterIndex = -1)
        {
            const float doorwayApproachDepth = 1.7f;
            var inward = GetRoomInwardDirection(wallSide);
            var approachPosition =
                doorwayPosition + inward * doorwayApproachDepth;
            var lanePosition = wallSide is WallSide.North or WallSide.South
                ? new Vector2(doorwayPosition.x, room.Position.y)
                : new Vector2(room.Position.x, doorwayPosition.y);

            if (!reverseLinkOrder)
            {
                if ((lanePosition - room.Position).sqrMagnitude > 0.04f)
                {
                    var laneIndex = AddNavigationNode(
                        $"Node_{roomId}_Lane_{corridorIndex:00}{endpointLabel}",
                        lanePosition,
                        parent,
                        nodes);
                    links.Add(new TopDownNavigationGraph.Link(
                        previousIndex,
                        laneIndex));
                    previousIndex = laneIndex;
                }

                var approachIndex = AddNavigationNode(
                    $"Node_{roomId}_Approach_{corridorIndex:00}{endpointLabel}",
                    approachPosition,
                    parent,
                    nodes);
                links.Add(new TopDownNavigationGraph.Link(
                    previousIndex,
                    approachIndex));
                return approachIndex;
            }

            var reverseApproachIndex = AddNavigationNode(
                $"Node_{roomId}_Approach_{corridorIndex:00}{endpointLabel}",
                approachPosition,
                parent,
                nodes);
            links.Add(new TopDownNavigationGraph.Link(
                previousIndex,
                reverseApproachIndex));
            previousIndex = reverseApproachIndex;
            if ((lanePosition - room.Position).sqrMagnitude > 0.04f)
            {
                var laneIndex = AddNavigationNode(
                    $"Node_{roomId}_Lane_{corridorIndex:00}{endpointLabel}",
                    lanePosition,
                    parent,
                    nodes);
                links.Add(new TopDownNavigationGraph.Link(
                    previousIndex,
                    laneIndex));
                previousIndex = laneIndex;
            }

            links.Add(new TopDownNavigationGraph.Link(
                previousIndex,
                roomCenterIndex));
            return roomCenterIndex;
        }

        private static int AddNavigationNode(
            string name,
            Vector2 position,
            Transform parent,
            List<Transform> nodes)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent);
            node.transform.position = position;
            var index = nodes.Count;
            nodes.Add(node.transform);
            return index;
        }

        private static Vector2 GetRoomInwardDirection(WallSide wallSide)
        {
            return wallSide switch
            {
                WallSide.North => Vector2.down,
                WallSide.South => Vector2.up,
                WallSide.East => Vector2.left,
                WallSide.West => Vector2.right,
                _ => Vector2.zero
            };
        }

        private static GameObject CreatePlayer(
            Transform parent,
            Vector2 spawnPosition)
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            var movementConfig =
                AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                    MovementConfigPath);
            var interactionConfig = EnsureInteractionBalanceConfig();
            if (inputActions == null || movementConfig == null ||
                interactionConfig == null)
            {
                throw new InvalidOperationException(
                    "Player input or movement config is missing.");
            }

            var player = new GameObject("P_Player_Local");
            player.transform.SetParent(parent);
            player.transform.position = spawnPosition;
            var body = player.AddComponent<Rigidbody2D>();
            ConfigureDynamicBody(body);
            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1.05f, 1.45f);

            var input = player.AddComponent<PlayerInputReader>();
            input.Configure(inputActions);
            var motor = player.AddComponent<PlayerMotor>();
            motor.Configure(input, body, movementConfig);
            var aim = player.AddComponent<PlayerAimController>();
            aim.Configure(input, Camera.main, movementConfig);
            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.Configure(
                input,
                interactionConfig.GeneralInteractionRangeMeters);
            var monsterTarget = player.AddComponent<MonsterTarget>();
            monsterTarget.Configure(true, true);
            motor.BindMonsterTarget(monsterTarget);

            CreatePlayerVisuals(
                player.transform,
                new Color(0.12f, 0.56f, 0.96f),
                input,
                out _);
            var promptObject = new GameObject("[UI] InteractionPrompt");
            promptObject.transform.SetParent(parent);
            promptObject.AddComponent<InteractionPromptView>()
                .Configure(interactor);
            return player;
        }

        internal static GameObject CreatePlayerVisuals(
            Transform parent,
            Color bodyColor,
            PlayerInputReader input,
            out FlashlightController flashlightController)
        {
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(parent, false);
            var bodyObject = CreateSpriteObject(
                "Body",
                LoadSprite(PlayerSpritePath),
                Vector2.zero,
                new Vector2(2f, 2f),
                bodyColor,
                40,
                visualRoot.transform);
            bodyObject.transform.localPosition = Vector3.zero;

            var personalGlowObject = new GameObject("PersonalGlow");
            personalGlowObject.transform.SetParent(visualRoot.transform, false);
            var personalGlow = personalGlowObject.AddComponent<Light2D>();
            personalGlow.lightType = Light2D.LightType.Point;
            personalGlow.pointLightInnerRadius = 0.12f;
            personalGlow.pointLightOuterRadius = 0.5f;
            personalGlow.color = new Color(0.22f, 0.34f, 0.42f);
            personalGlow.intensity = 0.006f;

            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(visualRoot.transform, false);
            var cone = CreateSpriteObject(
                "FlashlightCone",
                LoadSprite(FlashlightSpritePath),
                new Vector2(0f, 0.55f),
                new Vector2(3.25f, 3.20f),
                Color.white,
                6,
                aimPivot.transform);
            cone.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            cone.GetComponent<SpriteRenderer>().sharedMaterial =
                GetIndicatorUnlitMaterial();
            var flashlight = cone.AddComponent<Light2D>();
            flashlight.lightType = Light2D.LightType.Point;
            flashlight.pointLightInnerRadius = 0.9f;
            flashlight.pointLightOuterRadius = 8f;
            flashlight.pointLightInnerAngle = 28f;
            flashlight.pointLightOuterAngle = 54f;
            flashlight.color = new Color(0.56f, 0.84f, 0.92f);
            flashlight.intensity = 1.1f;
            flashlightController =
                parent.gameObject.AddComponent<FlashlightController>();
            flashlightController.Configure(
                input,
                parent.GetComponent<PlayerAimController>(),
                aimPivot.transform,
                cone,
                true);
            flashlightController.BindStealthVisibility(
                personalGlow,
                parent.GetComponent<MonsterTarget>());
            var motionFeel =
                parent.GetComponent<PlayerMotionFeel>() ??
                parent.gameObject.AddComponent<PlayerMotionFeel>();
            motionFeel.Configure(parent, bodyObject.transform);
            return visualRoot;
        }

        private static void ConfigureCamera(Transform player)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.008f, 0.016f, 0.026f);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 9f;
            mainCamera.transform.rotation = Quaternion.identity;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                mainCamera.gameObject);

            var follow = mainCamera.GetComponent<TopDownCamera>() ??
                         mainCamera.gameObject.AddComponent<TopDownCamera>();
            follow.Configure(player, 9f, 0.12f);

            var aim = player.GetComponent<PlayerAimController>();
            aim.Configure(
                player.GetComponent<PlayerInputReader>(),
                mainCamera,
                AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                    MovementConfigPath));
        }

        private static FuseStationPrototype CreateFuseStation(
            Transform parent,
            RoomDefinition room,
            string stationName,
            Vector2 localOffset,
            MissionPrototypeKind kind)
        {
            var missionConfig = AssetDatabase.LoadAssetAtPath<FuseMissionConfig>(
                FuseMissionConfigPath);
            if (missionConfig == null)
            {
                missionConfig = ScriptableObject.CreateInstance<FuseMissionConfig>();
                missionConfig.name = "SO_FuseMission_Default";
                AssetDatabase.CreateAsset(missionConfig, FuseMissionConfigPath);
            }

            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.1f, 1.75f),
                GetMissionStationColor(kind),
                30,
                parent);
            var beacon = CreateSpriteObject(
                stationName + "_Beacon",
                LoadSprite(CircleSpritePath),
                room.Position + localOffset + new Vector2(0.72f, 0.54f),
                new Vector2(0.22f, 0.22f),
                new Color(0.20f, 0.82f, 0.86f, 0.62f),
                32,
                parent);
            beacon.GetComponent<SpriteRenderer>().sharedMaterial =
                GetIndicatorUnlitMaterial();
            var collider = station.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var fuseStation = station.AddComponent<FuseStationPrototype>();
            fuseStation.Configure(
                station.GetComponent<SpriteRenderer>(),
                null,
                missionConfig,
                kind);
            station.AddComponent<NetworkObject>();
            var authority =
                station.AddComponent<NetworkFuseStationAuthority>();
            authority.Configure(
                fuseStation,
                EnsureInteractionBalanceConfig());
            station.AddComponent<MissionStationNetworkPresenter>()
                .Configure(
                    authority,
                    station.GetComponent<SpriteRenderer>());
            return fuseStation;
        }

        private static Color GetMissionStationColor(
            MissionPrototypeKind kind)
        {
            return kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    new Color(0.96f, 0.42f, 0.08f),
                MissionPrototypeKind.BreakerSequence =>
                    new Color(0.94f, 0.72f, 0.12f),
                MissionPrototypeKind.CctvReboot =>
                    new Color(0.10f, 0.72f, 0.86f),
                MissionPrototypeKind.SampleSorting =>
                    new Color(0.48f, 0.78f, 0.30f),
                MissionPrototypeKind.BatteryTransport =>
                    new Color(0.92f, 0.58f, 0.08f),
                MissionPrototypeKind.PressureValves =>
                    new Color(0.18f, 0.50f, 0.82f),
                MissionPrototypeKind.SecurityCircuit =>
                    new Color(0.68f, 0.32f, 0.88f),
                MissionPrototypeKind.AntennaAlignment =>
                    new Color(0.14f, 0.70f, 0.58f),
                MissionPrototypeKind.ServerLogRecovery =>
                    new Color(0.24f, 0.82f, 0.42f),
                _ => Color.white
            };
        }

        private static BatteryReceiverPrototype CreateBatteryReceiver(
            Transform parent,
            RoomDefinition room,
            string receiverName,
            Vector2 localOffset,
            FuseStationPrototype sourceStation)
        {
            var receiver = CreateSpriteObject(
                receiverName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.3f, 1.9f),
                new Color(0.20f, 0.72f, 0.34f),
                30,
                parent);
            var collider = receiver.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var batteryReceiver =
                receiver.AddComponent<BatteryReceiverPrototype>();
            batteryReceiver.Configure(sourceStation);
            return batteryReceiver;
        }

        private static void ConfigureFuseStationFeedback(
            FuseStationPrototype station,
            NoiseService noiseService,
            string roomId)
        {
            station.gameObject.AddComponent<FuseFailureNoiseEmitter>()
                .Configure(station, noiseService, roomId);
            var audioSource = station.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.65f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 32f;
            audioSource.volume = 0.9f;
            station.gameObject.AddComponent<SettingsAudioSource>()
                .Configure(AudioCategory.Danger, audioSource.volume);
            station.gameObject.AddComponent<FuseFailureFeedback>()
                .Configure(station, audioSource);
            station.gameObject.AddComponent<MissionAssetFeedbackPresenter>()
                .Configure(
                    station,
                    audioSource,
                    station.transform,
                    EnsurePresentationAssetCatalog());
        }

        private static PresentationAssetCatalog
            EnsurePresentationAssetCatalog()
        {
            var catalog =
                AssetDatabase.LoadAssetAtPath<PresentationAssetCatalog>(
                    PresentationAssetCatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolder("Assets/_Project/Data", "Catalogs");
            catalog = ScriptableObject.CreateInstance<
                PresentationAssetCatalog>();
            catalog.name = "SO_PresentationAssetCatalog_Default";
            AssetDatabase.CreateAsset(
                catalog,
                PresentationAssetCatalogPath);
            return catalog;
        }

        private static InteractionBalanceConfig
            EnsureInteractionBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<InteractionBalanceConfig>(
                    InteractionBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config =
                ScriptableObject.CreateInstance<InteractionBalanceConfig>();
            config.name = "SO_InteractionBalance_Default";
            AssetDatabase.CreateAsset(
                config,
                InteractionBalanceConfigPath);
            return config;
        }

        private static DoorBalanceConfig EnsureDoorBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<DoorBalanceConfig>(
                    DoorBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<DoorBalanceConfig>();
            config.name = "SO_DoorBalance_Default";
            AssetDatabase.CreateAsset(config, DoorBalanceConfigPath);
            return config;
        }

        private static WorldLightingBalanceConfig
            EnsureWorldLightingBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<WorldLightingBalanceConfig>(
                    WorldLightingBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config =
                ScriptableObject.CreateInstance<WorldLightingBalanceConfig>();
            config.name = "SO_WorldLightingBalance_Default";
            AssetDatabase.CreateAsset(
                config,
                WorldLightingBalanceConfigPath);
            return config;
        }

        /// <summary>
        /// 현장 단서 마커를 배치한다. 강화는 축마다 2회까지 가능하므로
        /// 종류마다 마커를 2개씩 두고, 두 번째 강화는 다른 위치에 흔적을 남긴다(SDD §14.2).
        /// 마커는 비활성 상태로 시작해 강화 성공 시 서버가 켠다.
        /// </summary>
        private static ClueMarker[] CreateClueSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var clueRoot = new GameObject("[Clue] SceneClues").transform;
            clueRoot.SetParent(parent);

            var definitions = new[]
            {
                // 후각 강화 → 해당 실험실 환풍구의 붉은 연기
                (ClueKind.VentRedSmoke, "LabB", new Vector2(-3.2f, 5.2f)),
                (ClueKind.VentRedSmoke, "LabA", new Vector2(3.2f, 5.2f)),
                // 개체 강화 → 격리실 A·B의 파손된 잠금장치
                (ClueKind.BrokenQuarantineLock, "QuarantineA", new Vector2(0f, 5.2f)),
                (ClueKind.BrokenQuarantineLock, "QuarantineB", new Vector2(0f, 5.2f)),
                // 독성 강화 → 백신실 바닥의 빈 주사기
                (ClueKind.EmptySyringe, "VaccineB", new Vector2(3.2f, 1.2f)),
                (ClueKind.EmptySyringe, "VaccineA", new Vector2(-3.2f, 1.2f))
            };

            var markers = new ClueMarker[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var (kind, roomId, offset) = definitions[index];
                var marker = CreateClueMarker(
                    clueRoot,
                    kind,
                    clueId: index + 1,
                    roomId,
                    rooms[roomId].Position + offset);
                markers[index] = marker;
            }

            return markers;
        }

        private static ClueMarker CreateClueMarker(
            Transform parent,
            ClueKind kind,
            int clueId,
            string roomId,
            Vector2 position)
        {
            var (sprite, size, color) = GetClueVisual(kind);
            var markerObject = CreateSpriteObject(
                $"Clue_{kind}_{clueId:00}",
                LoadSprite(sprite),
                position,
                size,
                color,
                33,
                parent);
            var marker = markerObject.AddComponent<ClueMarker>();
            marker.Configure(
                markerObject.GetComponent<SpriteRenderer>(),
                kind,
                clueId,
                roomId);
            // 생성 전에는 보이지 않는다. 서버가 활성화할 때 켜진다.
            markerObject.GetComponent<SpriteRenderer>().enabled = false;
            return marker;
        }

        private static (string sprite, Vector2 size, Color color) GetClueVisual(
            ClueKind kind)
        {
            return kind switch
            {
                ClueKind.VentRedSmoke => (
                    CircleSpritePath,
                    new Vector2(1.9f, 1.9f),
                    new Color(0.95f, 0.2f, 0.15f, 0.7f)),
                ClueKind.BrokenQuarantineLock => (
                    PanelSpritePath,
                    new Vector2(1.5f, 1.1f),
                    new Color(1f, 0.7f, 0.1f, 0.9f)),
                ClueKind.EmptySyringe => (
                    PanelSpritePath,
                    new Vector2(1.2f, 0.5f),
                    new Color(0.85f, 0.95f, 1f, 0.95f)),
                ClueKind.SpeakerRedLed => (
                    CircleSpritePath,
                    new Vector2(0.42f, 0.42f),
                    new Color(1f, 0.12f, 0.12f, 1f)),
                _ => (
                    CircleSpritePath,
                    new Vector2(1f, 1f),
                    new Color(0.95f, 0.2f, 0.15f, 0.8f))
            };
        }

        private static SpeakerBalanceConfig EnsureSpeakerBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SpeakerBalanceConfig>(
                SpeakerBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SpeakerBalanceConfig>();
                config.name = "SO_SpeakerBalance_Default";
                AssetDatabase.CreateAsset(config, SpeakerBalanceConfigPath);
            }

            return config;
        }

        /// <summary>
        /// 방마다 스피커 하나와 그 스피커의 붉은 LED 단서 마커를 배치한다.
        /// LED는 방마다 따로 남아야 어느 방에서 울렸는지 추리할 수 있다(GDD §13.1).
        /// </summary>
        private static void CreateSpeakerSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            NoiseService noiseService,
            ClueMarker[] upgradeClueMarkers,
            out ClueMarker[] allClueMarkers)
        {
            var speakerRoot = new GameObject("[Speaker] RoomSpeakers").transform;
            speakerRoot.SetParent(parent);

            var speakers = new SpeakerPlacement[RoomOrder.Length];
            var ledMarkers = new ClueMarker[RoomOrder.Length];
            // 강화 단서 ID와 겹치지 않도록 뒤 번호를 쓴다.
            var nextClueId = upgradeClueMarkers.Length + 1;
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                var roomId = RoomOrder[index];
                var room = rooms[roomId];
                var speakerPosition = room.Position + new Vector2(0f, -4.6f);

                var speakerObject = CreateSpriteObject(
                    $"Speaker_{roomId}",
                    LoadSprite(PanelSpritePath),
                    speakerPosition,
                    new Vector2(1.1f, 0.8f),
                    new Color(0.55f, 0.58f, 0.62f, 1f),
                    31,
                    speakerRoot);
                var placement = speakerObject.AddComponent<SpeakerPlacement>();
                placement.Configure(
                    speakerObject.GetComponent<SpriteRenderer>(),
                    roomId,
                    room.DisplayName);
                speakers[index] = placement;

                ledMarkers[index] = CreateClueMarker(
                    speakerRoot,
                    ClueKind.SpeakerRedLed,
                    nextClueId++,
                    roomId,
                    speakerPosition + new Vector2(0.42f, 0.28f));
            }

            var authorityObject = new GameObject("[Network] SpeakerAuthority");
            authorityObject.transform.SetParent(parent);
            authorityObject.AddComponent<NetworkObject>();
            authorityObject.AddComponent<NetworkSpeakerAuthority>().Configure(
                EnsureSpeakerBalanceConfig(),
                noiseService,
                speakers);
            authorityObject.AddComponent<SpeakerActivationPresenter>()
                .Configure(speakers);

            var viewObject = new GameObject("[UI] SpeakerRemote");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<SpeakerRemoteView>();

            var meetingAuthorityObject =
                new GameObject("[Network] MeetingAuthority");
            meetingAuthorityObject.transform.SetParent(parent);
            meetingAuthorityObject.AddComponent<NetworkObject>();
            meetingAuthorityObject.AddComponent<NetworkMeetingAuthority>();
            // 토론 채팅은 살아 있는 참가자에게만 중계한다(SDD §11.5).
            meetingAuthorityObject
                .AddComponent<NetworkMeetingChatAuthority>()
                .Configure(EnsureRoundBalanceConfig());
            meetingAuthorityObject
                .AddComponent<NetworkGhostChatAuthority>()
                .Configure(EnsureRoundBalanceConfig());

            var meetingViewObject = new GameObject("[UI] Meeting");
            meetingViewObject.transform.SetParent(parent);
            meetingViewObject.AddComponent<MeetingView>();

            var ghostChatViewObject = new GameObject("[UI] GhostChat");
            ghostChatViewObject.transform.SetParent(parent);
            ghostChatViewObject.AddComponent<GhostChatView>();

            // CCTV·서버 로그는 프로젝트 50% 이후에 열린다(SDD §14.3).
            var roomDisplayNames = new string[RoomOrder.Length];
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                roomDisplayNames[index] = rooms[RoomOrder[index]].DisplayName;
            }

            var terminalObject =
                new GameObject("[Network] SecurityTerminalAuthority");
            terminalObject.transform.SetParent(parent);
            terminalObject.AddComponent<NetworkObject>();
            var terminalPosition =
                rooms["Security"].Position + new Vector2(0f, 3.5f);
            terminalObject.AddComponent<NetworkSecurityTerminalAuthority>()
                .Configure(
                    RoomOrder,
                    roomDisplayNames,
                    terminalPosition,
                    2.5f);

            var terminalWorldObject = CreateSpriteObject(
                "Security_CctvTerminal",
                LoadSprite(PanelSpritePath),
                terminalPosition,
                new Vector2(2.2f, 1.4f),
                new Color(0.22f, 0.25f, 0.3f, 1f),
                32,
                parent);
            var terminalCollider =
                terminalWorldObject.AddComponent<BoxCollider2D>();
            terminalCollider.isTrigger = true;
            terminalCollider.size = Vector2.one;
            var terminalPrototype = terminalWorldObject
                .AddComponent<SecurityTerminalPrototype>();
            terminalPrototype.Configure(
                terminalWorldObject.GetComponent<SpriteRenderer>());

            var feedController = CreateCctvFeedController(parent, rooms);

            var terminalViewObject = new GameObject("[UI] SecurityTerminal");
            terminalViewObject.transform.SetParent(parent);
            terminalViewObject.AddComponent<SecurityTerminalView>()
                .Configure(terminalPrototype, feedController);

            var combined =
                new ClueMarker[upgradeClueMarkers.Length + ledMarkers.Length];
            upgradeClueMarkers.CopyTo(combined, 0);
            ledMarkers.CopyTo(combined, upgradeClueMarkers.Length);
            allClueMarkers = combined;
        }

        private static CctvFeedController CreateCctvFeedController(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var controllerObject = new GameObject("[CCTV] LiveFeeds");
            controllerObject.transform.SetParent(parent);
            var definitions = new[]
            {
                new CctvDefinition(
                    "북쪽 루프 복도",
                    rooms["LabA"].Position),
                new CctvDefinition(
                    "남쪽 루프 복도",
                    rooms["LabB"].Position),
                new CctvDefinition(
                    "중앙 교차로",
                    rooms["Security"].Position),
                new CctvDefinition(
                    "백신실 A 앞",
                    rooms["VaccineA"].Position + new Vector2(5f, 0f)),
                new CctvDefinition(
                    "백신실 B 앞",
                    rooms["VaccineB"].Position + new Vector2(-5f, 0f)),
                new CctvDefinition(
                    "격리실 A 앞",
                    rooms["QuarantineA"].Position + new Vector2(0f, -6f)),
                new CctvDefinition(
                    "격리실 B 앞",
                    rooms["QuarantineB"].Position + new Vector2(0f, 6f))
            };
            var feeds = new CctvFeedCamera[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var cameraObject = new GameObject(
                    $"CCTV_Camera_{index + 1:00}");
                cameraObject.transform.SetParent(controllerObject.transform);
                cameraObject.transform.position = new Vector3(
                    definition.Position.x,
                    definition.Position.y,
                    -10f);
                cameraObject.transform.rotation = Quaternion.identity;
                var feedCamera = cameraObject.AddComponent<Camera>();
                feedCamera.clearFlags = CameraClearFlags.SolidColor;
                feedCamera.backgroundColor =
                    new Color(0.008f, 0.016f, 0.026f);
                feedCamera.orthographic = true;
                feedCamera.orthographicSize = 7.5f;
                feedCamera.depth = -20f;
                feedCamera.enabled = false;
                feeds[index] = cameraObject.AddComponent<CctvFeedCamera>();
                feeds[index].Configure(
                    feedCamera,
                    definition.DisplayName,
                    640,
                    360);
            }

            var controller =
                controllerObject.AddComponent<CctvFeedController>();
            controller.Configure(feeds);
            return controller;
        }

        private static MonsterBalanceConfig EnsureMonsterBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterBalanceConfig>(
                MonsterBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterBalanceConfig>();
                config.name = "SO_MonsterBalance_Default";
                AssetDatabase.CreateAsset(config, MonsterBalanceConfigPath);
            }

            return config;
        }

        private static UpgradeBalanceConfig EnsureUpgradeBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<UpgradeBalanceConfig>(
                UpgradeBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UpgradeBalanceConfig>();
                config.name = "SO_UpgradeBalance_Default";
                AssetDatabase.CreateAsset(config, UpgradeBalanceConfigPath);
            }

            return config;
        }

        /// <summary>
        /// 빌런 전용 강화 스테이션을 만든다. 축마다 하나씩 배치한다.
        /// </summary>
        private static UpgradeStationPrototype CreateUpgradeStation(
            Transform parent,
            RoomDefinition room,
            string stationName,
            Vector2 localOffset,
            UpgradeAxis axis,
            string roomId)
        {
            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.1f, 1.75f),
                new Color(0.65f, 0.2f, 0.85f, 1f),
                30,
                parent);
            var collider = station.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var upgradeStation =
                station.AddComponent<UpgradeStationPrototype>();
            upgradeStation.Configure(
                station.GetComponent<SpriteRenderer>(),
                EnsureUpgradeBalanceConfig(),
                axis,
                roomId);
            station.AddComponent<NetworkObject>();
            station.AddComponent<NetworkUpgradeStationAuthority>().Configure(
                upgradeStation,
                EnsureInteractionBalanceConfig());
            return upgradeStation;
        }

        /// <summary>
        /// 개체 강화로 추가되는 괴물과 강화 권위 오브젝트를 만든다.
        /// 1단계는 격리실 A, 2단계는 격리실 B에서 두 마리씩 활성화한다.
        /// </summary>
        private static void CreateUpgradeSystem(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target,
            NetworkMonsterAuthority[] baseMonsters)
        {
            // 위치는 GDD §13.2~13.4를 따른다.
            // 개체 강화 패널은 격리실과 떨어진 보안실에 둔다(§13.3).
            CreateUpgradeStation(
                parent,
                rooms["LabB"],
                "UpgradeStation_Scent",
                new Vector2(-3.2f, 3.4f),
                UpgradeAxis.Scent,
                "LabB");
            CreateUpgradeStation(
                parent,
                rooms["Security"],
                "UpgradeStation_Population",
                new Vector2(-3.2f, -3.4f),
                UpgradeAxis.Population,
                "Security");
            CreateUpgradeStation(
                parent,
                rooms["VaccineB"],
                "UpgradeStation_Toxicity",
                new Vector2(3.2f, 3.4f),
                UpgradeAxis.Toxicity,
                "VaccineB");
            var upgradeClueMarkers = CreateClueSystem(parent, rooms);
            CreateSpeakerSystem(
                parent,
                rooms,
                noiseService,
                upgradeClueMarkers,
                out var allClueMarkers);

            var clueAuthorityObject = new GameObject("[Network] ClueAuthority");
            clueAuthorityObject.transform.SetParent(parent);
            clueAuthorityObject.AddComponent<NetworkObject>();
            clueAuthorityObject.AddComponent<NetworkClueAuthority>()
                .Configure(allClueMarkers);

            var config = EnsureMonsterBalanceConfig();
            var reinforcementRoot =
                new GameObject("[AI] MonsterReinforcements").transform;
            reinforcementRoot.SetParent(parent);
            var patrolRoutes =
                CreateReinforcementPatrolRoutes(reinforcementRoot, rooms);
            var tierOne = CreateReinforcementWave(
                reinforcementRoot,
                rooms["QuarantineA"].Position,
                patrolRoutes[0],
                waveIndex: 1,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                monsterTierRuntime,
                target);
            var tierTwo = CreateReinforcementWave(
                reinforcementRoot,
                rooms["QuarantineB"].Position,
                patrolRoutes[1],
                waveIndex: 2,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                monsterTierRuntime,
                target);

            var spawnerObject = new GameObject("[Network] MonsterPopulationSpawner");
            spawnerObject.transform.SetParent(parent);
            spawnerObject.AddComponent<NetworkObject>();
            var spawner =
                spawnerObject.AddComponent<NetworkMonsterPopulationSpawner>();
            spawner.Configure(
                baseMonsters,
                tierOne,
                tierTwo,
                monsterTierRuntime.Config);

            var authorityObject = new GameObject("[Network] VillainUpgradeAuthority");
            authorityObject.transform.SetParent(parent);
            authorityObject.AddComponent<NetworkObject>();
            authorityObject.AddComponent<NetworkVillainUpgradeAuthority>()
                .Configure(
                    monsterTierRuntime,
                    EnsureUpgradeBalanceConfig(),
                    spawner);
        }

        private static NetworkMonsterAuthority[] CreateReinforcementWave(
            Transform parent,
            Vector2 spawnPosition,
            Transform[] patrolPoints,
            int waveIndex,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            const int monstersPerWave = 2;
            var wave = new NetworkMonsterAuthority[monstersPerWave];
            for (var index = 0; index < monstersPerWave; index++)
            {
                var offset = new Vector2(index * 2.4f - 1.2f, 0f);
                var monster = CreateMonsterInstance(
                    parent,
                    100 * waveIndex + index,
                    spawnPosition + offset,
                    patrolPoints,
                    navigationGraph,
                    noiseService,
                    config,
                    roundPhase,
                    monsterTierRuntime,
                    target);
                wave[index] = monster.GetComponent<NetworkMonsterAuthority>();
                monster.SetActive(false);
            }

            return wave;
        }

        private static Transform[][] CreateReinforcementPatrolRoutes(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var routeRoomIds = new[]
            {
                new[] { "QuarantineA", "Ward", "Security" },
                new[] { "QuarantineB", "LabB", "Power" }
            };
            var routes = new Transform[routeRoomIds.Length][];
            for (var routeIndex = 0;
                 routeIndex < routeRoomIds.Length;
                 routeIndex++)
            {
                var routeRoot =
                    new GameObject(
                        $"ReinforcementRoute_{routeIndex + 1:00}")
                        .transform;
                routeRoot.SetParent(parent);
                var roomIds = routeRoomIds[routeIndex];
                routes[routeIndex] = new Transform[roomIds.Length];
                for (var pointIndex = 0;
                     pointIndex < roomIds.Length;
                     pointIndex++)
                {
                    var point =
                        new GameObject(
                            $"Patrol_{pointIndex + 1:00}_{roomIds[pointIndex]}");
                    point.transform.SetParent(routeRoot);
                    point.transform.position =
                        rooms[roomIds[pointIndex]].Position;
                    routes[routeIndex][pointIndex] = point.transform;
                }
            }

            return routes;
        }

        private static NetworkMonsterAuthority[] CreateMonsters(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            var config = EnsureMonsterBalanceConfig();
            var patrolRoutes = CreateMonsterPatrolRoutes(parent, rooms);
            var baseMonsters =
                new NetworkMonsterAuthority[MonsterSpawnRoomIds.Length];
            for (var index = 0; index < MonsterSpawnRoomIds.Length; index++)
            {
                var monster = CreateMonsterInstance(
                    parent,
                    index,
                    rooms[MonsterSpawnRoomIds[index]].Position,
                    patrolRoutes[index],
                    navigationGraph,
                    noiseService,
                    config,
                    roundPhase,
                    monsterTierRuntime,
                    target);
                baseMonsters[index] =
                    monster.GetComponent<NetworkMonsterAuthority>();
            }

            return baseMonsters;
        }

        private static GameObject CreateMonsterInstance(
            Transform parent,
            int monsterIndex,
            Vector2 spawnPosition,
            Transform[] patrolPoints,
            TopDownNavigationGraph navigationGraph,
            NoiseService noiseService,
            MonsterBalanceConfig config,
            LocalRoundPhasePrototype roundPhase,
            MonsterTierRuntime monsterTierRuntime,
            MonsterTarget target)
        {
            var monster = new GameObject($"P_Monster_{monsterIndex + 1:00}");
            monster.transform.SetParent(parent);
            monster.transform.position = spawnPosition;
            var body = monster.AddComponent<Rigidbody2D>();
            ConfigureDynamicBody(body);
            var collider = monster.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(1.65f, 1.7f);

            var visual = CreateSpriteObject(
                "Visual",
                LoadSprite(MonsterSpritePath),
                Vector2.zero,
                new Vector2(2.3f, 2.3f),
                Color.white,
                41,
                monster.transform);
            visual.transform.localPosition = Vector3.zero;
            var eye = CreateSpriteObject(
                "RX9Eye",
                LoadSprite(CircleSpritePath),
                new Vector2(0f, 0.32f),
                new Vector2(0.26f, 0.16f),
                new Color(1f, 0.16f, 0.2f),
                43,
                monster.transform);
            eye.transform.localPosition = new Vector3(0f, 0.32f, 0f);

            var senses = monster.AddComponent<MonsterSenses>();
            senses.Configure(
                config,
                monsterTierRuntime,
                target,
                Physics2D.DefaultRaycastLayers,
                navigationGraph);
            var biteController = monster.AddComponent<MonsterBiteController>();
            biteController.Configure(config, senses, target);
            var brain = monster.AddComponent<MonsterBrain>();
            brain.Configure(
                body,
                navigationGraph,
                noiseService,
                config,
                roundPhase,
                senses,
                biteController,
                patrolPoints);
            monster.AddComponent<MonsterPrototypePresenter>()
                .Configure(brain, eye.GetComponent<SpriteRenderer>(), null);
            var networkObject = monster.AddComponent<NetworkObject>();
            networkObject.ActiveSceneSynchronization = true;
            var networkTransform = monster.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode =
                NetworkTransform.AuthorityModes.Server;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = false;
            networkTransform.SyncRotAngleZ = false;
            networkTransform.SyncPositionZ = false;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;
            networkTransform.UseUnreliableDeltas = true;
            monster.AddComponent<NetworkMonsterAuthority>().Configure(
                brain,
                body,
                networkTransform);
            return monster;
        }

        private static Transform[][] CreateMonsterPatrolRoutes(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var root = new GameObject("[AI] MonsterPatrolRoutes").transform;
            root.SetParent(parent);
            var routeRoomIds = new[]
            {
                new[] { "VaccineA", "LabA", "Storage" },
                new[] { "VaccineB", "Ward", "Power" },
                new[] { "LabB", "Storage", "Security" },
                new[] { "QuarantineB", "Power", "VaccineB" }
            };
            var routes = new Transform[routeRoomIds.Length][];
            for (var routeIndex = 0;
                 routeIndex < routeRoomIds.Length;
                 routeIndex++)
            {
                var routeRoot =
                    new GameObject(
                        $"MonsterPatrolRoute_{routeIndex + 1:00}")
                        .transform;
                routeRoot.SetParent(root);
                var roomIds = routeRoomIds[routeIndex];
                routes[routeIndex] = new Transform[roomIds.Length];
                for (var pointIndex = 0;
                     pointIndex < roomIds.Length;
                     pointIndex++)
                {
                    var point =
                        new GameObject(
                            $"Patrol_{pointIndex + 1:00}_{roomIds[pointIndex]}");
                    point.transform.SetParent(routeRoot);
                    point.transform.position =
                        rooms[roomIds[pointIndex]].Position;
                    routes[routeIndex][pointIndex] = point.transform;
                }
            }

            return routes;
        }

        private static void CreateInfectionPrototype(
            Transform parent,
            GameObject player,
            MonsterTarget target,
            MonsterTierRuntime monsterTierRuntime)
        {
            var config = EnsureAntidoteBalanceConfig();
            var infectionService = player.AddComponent<InfectionService>();
            infectionService.Configure(target, monsterTierRuntime);
            var antidoteService = player.AddComponent<AntidoteService>();
            antidoteService.Configure(
                config,
                infectionService,
                player.GetComponent<PlayerInputReader>(),
                player.GetComponent<PlayerMotor>());
            var hudObject = new GameObject("[UI] InfectionHud");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<InfectionHudView>()
                .Configure(infectionService, antidoteService);
        }

        /// <summary>
        /// 제작기 수는 밸런스 표(§8)의 2대와, 레시피 후보는 맵 설계 §7.2의 8곳과 맞춘다.
        /// 후보가 생존자 5명보다 적으면 라운드 시작 시 배정이 실패한다.
        /// </summary>
        private static void ValidateAntidoteEconomy(List<string> failures)
        {
            var antidoteConfig = EnsureAntidoteBalanceConfig();
            var fabricators =
                UnityEngine.Object.FindObjectsByType<
                    AntidoteFabricatorPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (fabricators.Length != antidoteConfig.FabricatorCount ||
                Array.Exists(
                    fabricators,
                    item =>
                        item.Config == null ||
                        item.GetComponent<Collider2D>() == null ||
                        item.GetComponent<
                            NetworkAntidoteFabricatorAuthority>() == null) ||
                !Array.Exists(fabricators, item => item.RoomId == "VaccineA") ||
                !Array.Exists(fabricators, item => item.RoomId == "VaccineB"))
            {
                failures.Add(
                    "The vaccine room fabricators are incomplete. " +
                    "Both vaccine rooms need one networked fabricator.");
            }

            var lockers =
                UnityEngine.Object.FindObjectsByType<AntidoteStorageLocker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (lockers.Length < 1 ||
                Array.Exists(
                    lockers,
                    item =>
                        item.SlotCapacity !=
                            antidoteConfig.StorageLockerSlotCount ||
                        item.GetComponent<NetworkStorageLockerAuthority>() ==
                            null))
            {
                failures.Add("The antidote storage lockers are incomplete.");
            }

            var recipeAuthority =
                GameObject.Find("[Network] RecipeAuthority")?
                    .GetComponent<NetworkRecipeAuthority>();
            var notes =
                UnityEngine.Object.FindObjectsByType<RecipeNotePrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            const int survivorCount = 5;
            const int expectedCandidateCount = 8;
            var candidateIndices = new HashSet<int>();
            foreach (var note in notes)
            {
                candidateIndices.Add(note.CandidateIndex);
            }

            if (recipeAuthority == null ||
                recipeAuthority.CandidateCount != notes.Length ||
                notes.Length != expectedCandidateCount ||
                candidateIndices.Count != notes.Length ||
                notes.Length < survivorCount ||
                Array.Exists(
                    notes,
                    note =>
                        note.RoomId is "VaccineA" or "VaccineB" ||
                        note.GetComponent<Collider2D>() == null))
            {
                failures.Add(
                    "The recipe candidate setup is incomplete. " +
                    "Expected 8 uniquely indexed notes outside the vaccine rooms.");
            }

            var localEconomy = GameObject.Find("P_Player_Local")?
                .GetComponent<LocalAntidoteEconomyPrototype>();
            if (localEconomy == null ||
                localEconomy.RecipeNoteCount != notes.Length ||
                localEconomy.FabricatorCount != fabricators.Length ||
                localEconomy.LockerCount != lockers.Length)
            {
                failures.Add(
                    "The local antidote economy prototype is not fully connected.");
            }
        }

        private static AntidoteBalanceConfig EnsureAntidoteBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AntidoteBalanceConfig>(
                AntidoteBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<AntidoteBalanceConfig>();
            config.name = "SO_AntidoteBalance_Default";
            AssetDatabase.CreateAsset(config, AntidoteBalanceConfigPath);
            return config;
        }

        /// <summary>
        /// 백신실 제작기 2대와 보관 칸, 개인 레시피 후보 8곳을 배치한다.
        /// 제작기·보관함 위치는 docs/map-level-design.md §4.1, §4.10을,
        /// 레시피 후보는 §7.2를 따른다. 백신실에는 레시피를 두지 않는다.
        /// </summary>
        private static void CreateAntidoteEconomy(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            GameObject localPlayer)
        {
            var antidoteConfig = EnsureAntidoteBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var economyRoot =
                new GameObject("[Gameplay] AntidoteEconomy").transform;
            economyRoot.SetParent(parent);

            var fabricators = new[]
            {
                CreateFabricator(
                    economyRoot,
                    rooms["VaccineA"],
                    "AntidoteFabricator_A",
                    new Vector2(-3f, 3.5f),
                    "VaccineA",
                    "백신 제작기 A",
                    antidoteConfig,
                    interactionConfig),
                CreateFabricator(
                    economyRoot,
                    rooms["VaccineB"],
                    "AntidoteFabricator_B",
                    new Vector2(-3f, -3.5f),
                    "VaccineB",
                    "백신 제작기 B",
                    antidoteConfig,
                    interactionConfig)
            };

            var lockers = new[]
            {
                CreateStorageLocker(
                    economyRoot,
                    rooms["VaccineA"],
                    "AntidoteLocker_A",
                    new Vector2(3f, 3.5f),
                    "VaccineA",
                    antidoteConfig,
                    interactionConfig),
                CreateStorageLocker(
                    economyRoot,
                    rooms["VaccineB"],
                    "AntidoteLocker_B",
                    new Vector2(3f, -3.5f),
                    "VaccineB",
                    antidoteConfig,
                    interactionConfig)
            };

            var candidates = new[]
            {
                CreateRecipeNote(
                    economyRoot, rooms["Storage"], 0,
                    new Vector2(3.5f, -4f), "Storage"),
                CreateRecipeNote(
                    economyRoot, rooms["Storage"], 1,
                    new Vector2(-3.5f, -4f), "Storage"),
                CreateRecipeNote(
                    economyRoot, rooms["Ward"], 2,
                    new Vector2(-3.5f, 3.5f), "Ward"),
                CreateRecipeNote(
                    economyRoot, rooms["Ward"], 3,
                    new Vector2(3.5f, -3.5f), "Ward"),
                CreateRecipeNote(
                    economyRoot, rooms["LabA"], 4,
                    new Vector2(-4.5f, 4.5f), "LabA"),
                CreateRecipeNote(
                    economyRoot, rooms["LabB"], 5,
                    new Vector2(4.5f, -4.5f), "LabB"),
                CreateRecipeNote(
                    economyRoot, rooms["Power"], 6,
                    new Vector2(0f, -4.5f), "Power"),
                CreateRecipeNote(
                    economyRoot, rooms["Security"], 7,
                    new Vector2(-4.5f, 4.5f), "Security")
            };

            var recipeAuthorityObject =
                new GameObject("[Network] RecipeAuthority");
            recipeAuthorityObject.transform.SetParent(parent);
            recipeAuthorityObject.AddComponent<NetworkObject>();
            recipeAuthorityObject.AddComponent<NetworkRecipeAuthority>()
                .Configure(candidates, interactionConfig);

            localPlayer.AddComponent<LocalAntidoteEconomyPrototype>()
                .Configure(
                    localPlayer.GetComponent<AntidoteService>(),
                    localPlayer.GetComponent<InfectionService>(),
                    candidates,
                    fabricators,
                    lockers);
        }

        private static AntidoteFabricatorPrototype CreateFabricator(
            Transform parent,
            RoomDefinition room,
            string objectName,
            Vector2 localOffset,
            string roomId,
            string displayName,
            AntidoteBalanceConfig antidoteConfig,
            InteractionBalanceConfig interactionConfig)
        {
            var instance = CreateSpriteObject(
                objectName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(2.1f, 1.75f),
                new Color(0.2f, 0.6f, 0.8f, 1f),
                30,
                parent);
            var collider = instance.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var fabricator =
                instance.AddComponent<AntidoteFabricatorPrototype>();
            fabricator.Configure(
                instance.GetComponent<SpriteRenderer>(),
                antidoteConfig,
                roomId,
                displayName);
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkAntidoteFabricatorAuthority>()
                .Configure(fabricator, antidoteConfig, interactionConfig);
            return fabricator;
        }

        private static AntidoteStorageLocker CreateStorageLocker(
            Transform parent,
            RoomDefinition room,
            string objectName,
            Vector2 localOffset,
            string roomId,
            AntidoteBalanceConfig antidoteConfig,
            InteractionBalanceConfig interactionConfig)
        {
            var instance = CreateSpriteObject(
                objectName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(1.6f, 1.6f),
                new Color(0.4f, 0.4f, 0.48f, 1f),
                30,
                parent);
            var collider = instance.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var locker = instance.AddComponent<AntidoteStorageLocker>();
            locker.Configure(
                instance.GetComponent<SpriteRenderer>(),
                antidoteConfig,
                roomId);
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkStorageLockerAuthority>()
                .Configure(locker, antidoteConfig, interactionConfig);
            return locker;
        }

        private static RecipeNotePrototype CreateRecipeNote(
            Transform parent,
            RoomDefinition room,
            int candidateIndex,
            Vector2 localOffset,
            string roomId)
        {
            var instance = CreateSpriteObject(
                $"RecipeNote_{candidateIndex:00}",
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                new Vector2(0.9f, 1.1f),
                new Color(0.85f, 0.82f, 0.6f, 1f),
                30,
                parent);
            var collider = instance.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var note = instance.AddComponent<RecipeNotePrototype>();
            note.Configure(
                instance.GetComponent<SpriteRenderer>(),
                candidateIndex,
                roomId);
            return note;
        }

        private static NoiseService CreateNoiseService(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<NoiseBalanceConfig>(
                NoiseBalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<NoiseBalanceConfig>();
                config.name = "SO_NoiseBalance_Default";
                AssetDatabase.CreateAsset(config, NoiseBalanceConfigPath);
            }

            var serviceObject = new GameObject("[Gameplay] NoiseService");
            serviceObject.transform.SetParent(parent);
            var service = serviceObject.AddComponent<NoiseService>();
            service.Configure(config);
            return service;
        }

        private static RoundBalanceConfig EnsureRoundBalanceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<RoundBalanceConfig>(
                RoundBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            config.name = "SO_RoundBalance_Default";
            AssetDatabase.CreateAsset(config, RoundBalanceConfigPath);
            return config;
        }

        private static LocalRoundPhasePrototype CreateRoundPhase(Transform parent)
        {
            var config = EnsureRoundBalanceConfig();
            EditorUtility.SetDirty(config);
            var roundObject = new GameObject("[Gameplay] LocalRoundPhase");
            roundObject.transform.SetParent(parent);
            var round = roundObject.AddComponent<LocalRoundPhasePrototype>();
            round.Configure(config);
            return round;
        }

        private static NetworkRoundState CreateNetworkRoundState(
            Transform parent,
            LocalRoundPhasePrototype localRoundPhase,
            NetworkFuseStationAuthority[] missionStations)
        {
            var roundObject = new GameObject("[Network] RoundState");
            roundObject.transform.SetParent(parent);
            roundObject.AddComponent<NetworkObject>();
            var networkRound =
                roundObject.AddComponent<NetworkRoundState>();
            networkRound.Configure(
                localRoundPhase.Config,
                localRoundPhase,
                missionStations);

            // 결과 화면 공개용 요약이다. 라운드 중에는 비어 있다(GDD §20).
            var summaryObject = new GameObject("[Network] RoundSummary");
            summaryObject.transform.SetParent(parent);
            summaryObject.AddComponent<NetworkObject>();
            summaryObject.AddComponent<NetworkRoundSummaryAuthority>();

            // 연결 종료 처리다(GDD §19).
            var disconnectObject = new GameObject("[Network] DisconnectPolicy");
            disconnectObject.transform.SetParent(parent);
            disconnectObject.AddComponent<NetworkObject>();
            disconnectObject
                .AddComponent<NetworkDisconnectPolicyAuthority>()
                .Configure(localRoundPhase.Config);

            // 세션이 끊겼을 때 로비로 되돌리는 감시자는 NGO 수명주기 밖에 둔다.
            var watchdogObject = new GameObject("[Network] SessionWatchdog");
            watchdogObject.transform.SetParent(parent);
            watchdogObject.AddComponent<NetworkSessionWatchdog>();

            // 색상·닉네임 이름표다(mvp-scope §3.3).
            var nameTagObject = new GameObject("[UI] PlayerNameTags");
            nameTagObject.transform.SetParent(parent);
            nameTagObject.AddComponent<PlayerNameTagView>();

            // Tab 미션 목록과 전자지도다(GDD §7.2). 방 좌표는 미리 채워 넣는다.
            var roomMarkers = new MapRoomMarker[RoomDefinitions.Length];
            for (var index = 0; index < RoomDefinitions.Length; index++)
            {
                roomMarkers[index] = new MapRoomMarker(
                    RoomDefinitions[index].DisplayName,
                    RoomDefinitions[index].Position);
            }

            var journalObject = new GameObject("[UI] MissionJournal");
            journalObject.transform.SetParent(parent);
            journalObject.AddComponent<MissionJournalView>()
                .Configure(
                    roomMarkers,
                    new[]
                    {
                        new MapRoomMarker(
                            "옥상 헬기 진입로",
                            worldPosition: RoomDefinitions[1].Position +
                                new Vector2(0f, 7.2f)),
                        new MapRoomMarker(
                            "동쪽 비상문",
                            worldPosition: RoomDefinitions[^1].Position +
                                new Vector2(5.1f, 0f))
                    });
            return networkRound;
        }

        private static void CreateMilestoneWorldPresentation(
            Transform parent,
            Transform mapRoot,
            IReadOnlyDictionary<string, RoomDefinition> rooms)
        {
            var presentationRoot =
                new GameObject("[World] ProjectMilestones").transform;
            presentationRoot.SetParent(parent);
            var unitSprite = LoadSprite(UnitSpritePath);
            var globalLightObject = new GameObject("Light_GlobalEmergency");
            globalLightObject.transform.SetParent(presentationRoot);
            var globalLight = globalLightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.color = new Color(0.10f, 0.16f, 0.22f);
            globalLight.intensity = 0f;
            var guideLights = new Light2D[RoomOrder.Length];
            var guideIndicators = new SpriteRenderer[RoomOrder.Length];
            for (var index = 0; index < RoomOrder.Length; index++)
            {
                var room = rooms[RoomOrder[index]];
                var position = room.Position +
                               new Vector2(0f, -room.Size.y * 0.34f);
                var indicator = CreateSpriteObject(
                    $"GuideLight_{room.Id}",
                    unitSprite,
                    position,
                    new Vector2(1.4f, 0.18f),
                    new Color(0.1f, 0.3f, 0.34f, 0.35f),
                    4,
                    presentationRoot);
                guideIndicators[index] =
                    indicator.GetComponent<SpriteRenderer>();
                guideIndicators[index].sharedMaterial =
                    GetIndicatorUnlitMaterial();
                var lightObject = new GameObject($"Light_{room.Id}");
                lightObject.transform.SetParent(presentationRoot);
                lightObject.transform.position = position;
                var light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.pointLightInnerRadius = 0.18f;
                light.pointLightOuterRadius = 1.35f;
                light.color = new Color(0.32f, 0.82f, 1f);
                light.intensity = 0f;
                guideLights[index] = light;
            }

            var securityLightObject = new GameObject("Light_SecurityTerminal");
            securityLightObject.transform.SetParent(presentationRoot);
            securityLightObject.transform.position = rooms["Security"].Position;
            var securityLight = securityLightObject.AddComponent<Light2D>();
            securityLight.lightType = Light2D.LightType.Point;
            securityLight.pointLightInnerRadius = 0.8f;
            securityLight.pointLightOuterRadius = 3f;
            securityLight.color = new Color(0.28f, 0.7f, 1f);
            securityLight.intensity = 0f;

            var exitPositions = new[]
            {
                rooms["LabA"].Position + new Vector2(0f, 7.2f),
                rooms["VaccineB"].Position + new Vector2(5.1f, 0f)
            };
            var exitMarkers = new SpriteRenderer[exitPositions.Length];
            var exitLights = new Light2D[exitPositions.Length];
            for (var index = 0; index < exitPositions.Length; index++)
            {
                var marker = CreateSpriteObject(
                    $"ExitRoute_{index + 1:00}",
                    unitSprite,
                    exitPositions[index],
                    new Vector2(2.8f, 0.65f),
                    new Color(0.18f, 1f, 0.55f, 0.9f),
                    36,
                    presentationRoot);
                exitMarkers[index] = marker.GetComponent<SpriteRenderer>();
                exitMarkers[index].sharedMaterial =
                    GetIndicatorUnlitMaterial();
                exitMarkers[index].enabled = false;

                var lightObject = new GameObject($"ExitLight_{index + 1:00}");
                lightObject.transform.SetParent(presentationRoot);
                lightObject.transform.position = exitPositions[index];
                var light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.pointLightInnerRadius = 0.65f;
                light.pointLightOuterRadius = 2.5f;
                light.color = new Color(0.2f, 1f, 0.55f);
                light.intensity = 0f;
                exitLights[index] = light;
            }

            var audioSource = presentationRoot.gameObject
                .AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            presentationRoot.gameObject.AddComponent<SettingsAudioSource>()
                .Configure(AudioCategory.Effects, audioSource.volume);
            var labels = mapRoot.GetComponentsInChildren<TextMesh>(true);
            presentationRoot.gameObject
                .AddComponent<ProjectMilestoneWorldPresenter>()
                .Configure(
                    guideLights,
                    new[] { securityLight },
                    exitLights,
                    guideIndicators,
                    exitMarkers,
                    labels,
                    audioSource,
                    EnsurePresentationAssetCatalog(),
                    globalLight,
                    EnsureWorldLightingBalanceConfig());
        }

        private static void CreateEndingWorldPresentation(
            Transform parent,
            IReadOnlyDictionary<string, RoomDefinition> rooms,
            TopDownCamera worldCamera)
        {
            var endingRoot = new GameObject("[World] RoundEnding").transform;
            endingRoot.SetParent(parent);
            var unitSprite = LoadSprite(UnitSpritePath);
            var landingPosition =
                rooms["LabA"].Position + new Vector2(0f, 9.5f);
            var approachPosition = landingPosition + new Vector2(-26f, 18f);
            var departurePosition = landingPosition + new Vector2(32f, 22f);

            var helicopterRoot = new GameObject("Helicopter_Prototype").transform;
            helicopterRoot.SetParent(endingRoot);
            helicopterRoot.position = approachPosition;
            CreateSpriteObject(
                "Helicopter_Body",
                unitSprite,
                Vector2.zero,
                new Vector2(5.5f, 2.3f),
                new Color(0.23f, 0.32f, 0.36f, 1f),
                42,
                helicopterRoot).transform.localPosition = Vector3.zero;
            CreateSpriteObject(
                "Helicopter_Rotor",
                unitSprite,
                Vector2.zero,
                new Vector2(8f, 0.18f),
                new Color(0.65f, 0.82f, 0.86f, 0.9f),
                43,
                helicopterRoot).transform.localPosition =
                new Vector3(0f, 1.25f, 0f);
            helicopterRoot.gameObject.SetActive(false);

            var gasPositions = new[]
            {
                rooms["LabA"].Position + new Vector2(0f, 7.2f),
                rooms["VaccineB"].Position + new Vector2(5.1f, 0f)
            };
            var gasRenderers = new SpriteRenderer[gasPositions.Length];
            for (var index = 0; index < gasPositions.Length; index++)
            {
                var gasObject = CreateSpriteObject(
                    $"RX9_Gas_{index + 1:00}",
                    unitSprite,
                    gasPositions[index],
                    new Vector2(5f, 5f),
                    new Color(0.55f, 0.95f, 0.18f, 0f),
                    40,
                    endingRoot);
                gasRenderers[index] =
                    gasObject.GetComponent<SpriteRenderer>();
                gasRenderers[index].enabled = false;
            }

            var audioSource = endingRoot.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            endingRoot.gameObject.AddComponent<SettingsAudioSource>()
                .Configure(AudioCategory.Effects, audioSource.volume);
            endingRoot.gameObject
                .AddComponent<RoundEndingSequencePresenter>()
                .Configure(
                    helicopterRoot,
                    gasRenderers,
                    audioSource,
                    worldCamera,
                    approachPosition,
                    landingPosition,
                    departurePosition,
                    EnsurePresentationAssetCatalog());
        }

        private static MonsterTierRuntime CreateMonsterTierRuntime(Transform parent)
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterTierConfig>(
                MonsterTierConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterTierConfig>();
                config.name = "SO_MonsterTier_Default";
                AssetDatabase.CreateAsset(config, MonsterTierConfigPath);
            }

            var runtimeObject = new GameObject("[Gameplay] MonsterTierRuntime");
            runtimeObject.transform.SetParent(parent);
            var runtime = runtimeObject.AddComponent<MonsterTierRuntime>();
            runtime.Configure(config);
            return runtime;
        }

        private static void CreateGracePeriodView(
            Transform parent,
            LocalRoundPhasePrototype roundPhase)
        {
            var viewObject = new GameObject("[UI] GracePeriod");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<GracePeriodView>().Configure(roundPhase);
        }

        private static void CreateRoundHudView(Transform parent)
        {
            var viewObject = new GameObject("[UI] RoundHud");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<RoundHudView>();
        }

        private static void CreateVillainUpgradeHudView(Transform parent)
        {
            var viewObject = new GameObject("[UI] VillainUpgradeHud");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<VillainUpgradeHudView>();
        }

        private static void CreateFuseMissionView(
            Transform parent,
            FuseStationPrototype station,
            string viewName)
        {
            var viewObject = new GameObject(viewName);
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<FuseMissionView>().Configure(station);
        }

        private static void CreateNoiseAlertView(
            Transform parent,
            NoiseService noiseService)
        {
            var viewObject = new GameObject("[UI] NoiseAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<NoiseAlertView>().Configure(noiseService);
        }

        private static void CreateMonsterBiteAlertView(
            Transform parent,
            MonsterTarget target)
        {
            var viewObject = new GameObject("[UI] MonsterBiteAlert");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<MonsterBiteAlertView>().Configure(target);
        }

        private static void CreateGameplayFeelView(
            Transform parent,
            TopDownCamera worldCamera,
            GameObject localPlayer,
            FuseStationPrototype[] stations,
            MonsterBrain[] monsters)
        {
            var roomZones = new RoomPresentationZone[
                RoomDefinitions.Length];
            for (var index = 0; index < RoomDefinitions.Length; index++)
            {
                var room = RoomDefinitions[index];
                roomZones[index] = new RoomPresentationZone(
                    room.DisplayName,
                    room.Position,
                    room.Size);
            }

            var viewObject = new GameObject("[UI] GameplayFeel");
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<GameplayFeelView>().Configure(
                worldCamera,
                localPlayer.GetComponent<MonsterTarget>(),
                localPlayer.GetComponent<PlayerInteractor>(),
                localPlayer.GetComponent<PlayerInputReader>(),
                stations,
                monsters,
                roomZones);
        }

        private static GameObject CreateSpriteObject(
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent);
            instance.transform.position = position;
            instance.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = GetWorldSpriteLitMaterial();
            return instance;
        }

        private static void ConfigureDynamicBody(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 8f;
            body.angularDamping = 8f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private static void ClearOldLaboratoryObjects()
        {
            foreach (var objectName in new[]
                     {
                         "[Prototype] FirstPlayable", "[Map] LaboratoryBlockout",
                         "[Map] Laboratory2D", "[Map] RoomWalls",
                         "[Network] GameplayScene", "Directional Light",
                         "[UI] SceneInfo"
                     })
            {
                var target = GameObject.Find(objectName);
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void EnsureLightingMaterials()
        {
            EnsureFolder("Assets/_Project/Art", "Materials");
            _worldSpriteLitMaterial = EnsureSpriteMaterial(
                WorldSpriteLitMaterialPath,
                "M_WorldSpriteLit",
                "Universal Render Pipeline/2D/Sprite-Lit-Default");
            _indicatorUnlitMaterial = EnsureSpriteMaterial(
                IndicatorUnlitMaterialPath,
                "M_IndicatorUnlit",
                "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        private static Material EnsureSpriteMaterial(
            string path,
            string materialName,
            string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Required URP 2D shader is missing: {shaderName}.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = materialName
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Material GetWorldSpriteLitMaterial()
        {
            if (_worldSpriteLitMaterial == null)
            {
                EnsureLightingMaterials();
            }

            return _worldSpriteLitMaterial;
        }

        private static Material GetIndicatorUnlitMaterial()
        {
            if (_indicatorUnlitMaterial == null)
            {
                EnsureLightingMaterials();
            }

            return _indicatorUnlitMaterial;
        }

        private static void EnsureSpriteAssets()
        {
            EnsureFolder("Assets/_Project/Art", "Sprites");
            EnsureFolder("Assets/_Project/Art/Sprites", "Generated");
            EnsureFolder("Assets/_Project/Art/Sprites", "Characters");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImportedSprite(PlayerSpritePath, 1024f);
            ConfigureImportedSprite(MonsterSpritePath, 1024f);
            EnsureSprite(
                UnitSpritePath,
                "S_UnitSquare",
                8,
                8,
                (_, _) => new Color32(255, 255, 255, 255),
                new Vector2(0.5f, 0.5f),
                8f);
            EnsureSprite(
                VisorSpritePath,
                "S_Player_Visor",
                64,
                32,
                (x, y) => IsRoundedRect(x, y, 64, 32, 12)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                64f);
            EnsureSprite(
                CircleSpritePath,
                "S_StatusCircle",
                32,
                32,
                (x, y) => IsInsideEllipse(x, y, 15.5f, 15.5f, 14f, 14f)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                32f);
            EnsureSprite(
                PanelSpritePath,
                "S_MissionPanel",
                64,
                52,
                (x, y) => IsRoundedRect(x, y, 64, 52, 8)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0),
                new Vector2(0.5f, 0.5f),
                64f);
            EnsureSprite(
                FlashlightSpritePath,
                "S_FlashlightCone",
                128,
                160,
                CreateFlashlightPixel,
                new Vector2(0.5f, 0f),
                64f);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureImportedSprite(
            string path,
            float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    "Character sprite texture is missing: " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Color32 CreateFlashlightPixel(int x, int y)
        {
            var normalizedY = y / 159f;
            var halfWidth = Mathf.Lerp(4f, 62f, normalizedY);
            var distanceFromCenter = Mathf.Abs(x - 63.5f);
            if (distanceFromCenter > halfWidth)
            {
                return new Color32(0, 0, 0, 0);
            }

            var edgeFade = 1f - Mathf.Clamp01(
                distanceFromCenter / Mathf.Max(halfWidth, 1f));
            var distanceFade = 1f - normalizedY * 0.72f;
            var alpha = (byte)Mathf.RoundToInt(
                62f * edgeFade * distanceFade);
            return new Color32(118, 225, 255, alpha);
        }

        private static Sprite EnsureSprite(
            string path,
            string spriteName,
            int width,
            int height,
            Func<int, int, Color32> pixelFactory,
            Vector2 pivot,
            float pixelsPerUnit)
        {
            var existing = LoadSprite(path, false);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "T_" + spriteName[2..],
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixelFactory(x, y);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }

        private static Sprite LoadSprite(string path, bool throwIfMissing = true)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }

            if (throwIfMissing)
            {
                throw new InvalidOperationException(
                    "Generated sprite is missing: " + path);
            }

            return null;
        }

        private static bool IsRoundedRect(
            int x,
            int y,
            int width,
            int height,
            int radius)
        {
            var clampedX = Mathf.Clamp(x, radius, width - radius - 1);
            var clampedY = Mathf.Clamp(y, radius, height - radius - 1);
            var dx = x - clampedX;
            var dy = y - clampedY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool IsInsideEllipse(
            float x,
            float y,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY)
        {
            var dx = (x - centerX) / radiusX;
            var dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy <= 1f;
        }

        private static float DistanceToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                                  lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            var path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Color GetRoomColor(string roomId)
        {
            return roomId switch
            {
                "VaccineA" or "VaccineB" => new Color(0.14f, 0.30f, 0.31f),
                "QuarantineA" or "QuarantineB" => new Color(0.28f, 0.16f, 0.20f),
                "Power" => new Color(0.30f, 0.25f, 0.12f),
                "Security" => new Color(0.13f, 0.22f, 0.31f),
                "Ward" => new Color(0.20f, 0.27f, 0.28f),
                _ => new Color(0.15f, 0.23f, 0.27f)
            };
        }

        private static void ValidateCorridorLayout(List<string> failures)
        {
            foreach (var corridor in CorridorDefinitions)
            {
                if (!TryGetRoomDefinition(corridor.A, out var roomA) ||
                    !TryGetRoomDefinition(corridor.B, out var roomB))
                {
                    failures.Add(
                        $"Corridor {corridor.A}-{corridor.B} references an unknown room.");
                    continue;
                }

                if (corridor.PathPoints.Count < 2 ||
                    !IsEndpointOnRoomSide(
                        roomA,
                        corridor.Start,
                        corridor.SideA) ||
                    !IsEndpointOnRoomSide(
                        roomB,
                        corridor.End,
                        corridor.SideB))
                {
                    failures.Add(
                        $"Corridor {corridor.A}-{corridor.B} has an invalid room connection.");
                    continue;
                }

                for (var index = 1;
                     index < corridor.PathPoints.Count;
                     index++)
                {
                    var start = corridor.PathPoints[index - 1];
                    var end = corridor.PathPoints[index];
                    var delta = end - start;
                    if (Mathf.Abs(delta.x) > 0.001f &&
                        Mathf.Abs(delta.y) > 0.001f)
                    {
                        failures.Add(
                            $"Corridor {corridor.A}-{corridor.B} contains a diagonal segment.");
                        break;
                    }

                    var size = Mathf.Abs(delta.x) > 0.001f
                        ? new Vector2(Mathf.Abs(delta.x), CorridorWidth)
                        : new Vector2(CorridorWidth, Mathf.Abs(delta.y));
                    var segmentArea = CreateRect(
                        (start + end) * 0.5f,
                        size);
                    foreach (var room in RoomDefinitions)
                    {
                        if (room.Id == corridor.A ||
                            room.Id == corridor.B)
                        {
                            continue;
                        }

                        if (segmentArea.Overlaps(
                                CreateRect(room.Position, room.Size)))
                        {
                            failures.Add(
                                $"Corridor {corridor.A}-{corridor.B} crosses room {room.Id}.");
                            break;
                        }
                    }
                }
            }
        }

        private static void ValidateEnvironmentPropDefinitions(
            List<string> failures)
        {
            foreach (var definition in EnvironmentPropDefinitions)
            {
                if (!TryGetRoomDefinition(
                        definition.RoomId,
                        out var room))
                {
                    failures.Add(
                        $"Environment prop {definition.AssetKey} references unknown room {definition.RoomId}.");
                    continue;
                }

                var roomBounds = CreateRect(
                    room.Position,
                    room.Size - new Vector2(0.3f, 0.3f));
                var propBounds = CreateRect(
                    room.Position + definition.LocalPosition,
                    definition.Footprint * 0.88f);
                if (propBounds.xMin < roomBounds.xMin ||
                    propBounds.xMax > roomBounds.xMax ||
                    propBounds.yMin < roomBounds.yMin ||
                    propBounds.yMax > roomBounds.yMax)
                {
                    failures.Add(
                        $"Environment prop {definition.AssetKey} is outside room {definition.RoomId}.");
                    continue;
                }

                if (!definition.IsObstacle)
                {
                    continue;
                }

                foreach (var corridor in CorridorDefinitions)
                {
                    if (corridor.A == definition.RoomId)
                    {
                        ValidatePropDoorClearance(
                            definition,
                            room,
                            corridor.SideA,
                            corridor.Start,
                            propBounds,
                            failures);
                        ValidatePropNavigationClearance(
                            definition,
                            room,
                            corridor.SideA,
                            corridor.Start,
                            propBounds,
                            failures);
                    }

                    if (corridor.B == definition.RoomId)
                    {
                        ValidatePropDoorClearance(
                            definition,
                            room,
                            corridor.SideB,
                            corridor.End,
                            propBounds,
                            failures);
                        ValidatePropNavigationClearance(
                            definition,
                            room,
                            corridor.SideB,
                            corridor.End,
                            propBounds,
                            failures);
                    }
                }
            }

            for (var leftIndex = 0;
                 leftIndex < EnvironmentPropDefinitions.Length;
                 leftIndex++)
            {
                var left = EnvironmentPropDefinitions[leftIndex];
                if (left.MountKind == EnvironmentPropMountKind.FloorDecal &&
                    left.IsObstacle)
                {
                    failures.Add(
                        $"Floor decal {left.AssetKey} cannot be a blocking prop.");
                }

                if (!left.IsObstacle ||
                    !TryGetRoomDefinition(left.RoomId, out var room))
                {
                    continue;
                }

                var leftBounds = CreateRect(
                    room.Position + left.LocalPosition,
                    left.Footprint * 0.88f);
                for (var rightIndex = leftIndex + 1;
                     rightIndex < EnvironmentPropDefinitions.Length;
                     rightIndex++)
                {
                    var right = EnvironmentPropDefinitions[rightIndex];
                    if (!right.IsObstacle || right.RoomId != left.RoomId)
                    {
                        continue;
                    }

                    var rightBounds = CreateRect(
                        room.Position + right.LocalPosition,
                        right.Footprint * 0.88f);
                    if (leftBounds.Overlaps(rightBounds))
                    {
                        failures.Add(
                            $"Environment props {left.AssetKey} and {right.AssetKey} overlap in {left.RoomId}.");
                    }
                }
            }
        }

        private static void ValidateLightingPresentation(
            List<string> failures)
        {
            var litMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                WorldSpriteLitMaterialPath);
            var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                IndicatorUnlitMaterialPath);
            if (litMaterial == null || unlitMaterial == null)
            {
                failures.Add("The URP 2D lighting materials are missing.");
                return;
            }

            var globalLight = GameObject.Find("Light_GlobalEmergency")?
                .GetComponent<Light2D>();
            if (globalLight == null ||
                globalLight.lightType != Light2D.LightType.Global ||
                globalLight.intensity > 0.0001f)
            {
                failures.Add(
                    "The near-dark global emergency light is not configured.");
            }

            var player = GameObject.Find("P_Player_Local");
            var personalGlow = player?.transform.Find(
                "VisualRoot/PersonalGlow")?.GetComponent<Light2D>();
            var flashlight = player?.transform.Find(
                "VisualRoot/AimPivot/FlashlightCone")?
                .GetComponent<Light2D>();
            if (personalGlow == null || flashlight == null ||
                personalGlow.pointLightOuterRadius > 0.55f ||
                flashlight.pointLightOuterAngle > 60f)
            {
                failures.Add(
                    "The local personal glow or directional flashlight is invalid.");
            }

            var mapRoot = GameObject.Find("[Map] Laboratory2D");
            var litSpriteCount = 0;
            if (mapRoot != null)
            {
                foreach (var renderer in
                         mapRoot.GetComponentsInChildren<SpriteRenderer>(
                             includeInactive: true))
                {
                    if (renderer.sharedMaterial == litMaterial)
                    {
                        litSpriteCount++;
                    }
                }
            }

            if (litSpriteCount < 50)
            {
                failures.Add(
                    "The map sprites are not using the 2D lit world material.");
            }
        }

        private static void ValidatePropDoorClearance(
            EnvironmentPropDefinition definition,
            RoomDefinition room,
            WallSide wallSide,
            Vector2 doorwayPosition,
            Rect propBounds,
            List<string> failures)
        {
            const float interiorClearanceDepth = 2.5f;
            var inward = GetRoomInwardDirection(wallSide);
            var clearanceCenter = doorwayPosition +
                inward * (interiorClearanceDepth * 0.5f);
            var clearanceSize = wallSide is
                WallSide.North or WallSide.South
                ? new Vector2(CorridorWidth, interiorClearanceDepth)
                : new Vector2(interiorClearanceDepth, CorridorWidth);
            var clearance = CreateRect(
                clearanceCenter,
                clearanceSize);
            if (propBounds.Overlaps(clearance))
            {
                failures.Add(
                    $"Environment prop {definition.AssetKey} blocks the {wallSide} doorway of {room.Id}.");
            }
        }

        private static void ValidatePropNavigationClearance(
            EnvironmentPropDefinition definition,
            RoomDefinition room,
            WallSide wallSide,
            Vector2 doorwayPosition,
            Rect propBounds,
            List<string> failures)
        {
            const float approachDepth = 1.7f;
            const float laneWidth = 1.4f;
            var approachPosition = doorwayPosition +
                GetRoomInwardDirection(wallSide) * approachDepth;
            var lanePosition = wallSide is WallSide.North or WallSide.South
                ? new Vector2(doorwayPosition.x, room.Position.y)
                : new Vector2(room.Position.x, doorwayPosition.y);
            var centerLane = CreateAxisAlignedClearance(
                room.Position,
                lanePosition,
                laneWidth);
            var approachLane = CreateAxisAlignedClearance(
                lanePosition,
                approachPosition,
                laneWidth);
            if (propBounds.Overlaps(centerLane) ||
                propBounds.Overlaps(approachLane))
            {
                failures.Add(
                    $"Environment prop {definition.AssetKey} blocks the navigation lane to the {wallSide} doorway of {room.Id}.");
            }
        }

        private static Rect CreateAxisAlignedClearance(
            Vector2 start,
            Vector2 end,
            float width)
        {
            var delta = end - start;
            var size = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Abs(delta.x) + width, width)
                : new Vector2(width, Mathf.Abs(delta.y) + width);
            return CreateRect((start + end) * 0.5f, size);
        }

        private static bool TryGetRoomDefinition(
            string roomId,
            out RoomDefinition definition)
        {
            foreach (var room in RoomDefinitions)
            {
                if (room.Id != roomId)
                {
                    continue;
                }

                definition = room;
                return true;
            }

            definition = default;
            return false;
        }

        private static bool IsEndpointOnRoomSide(
            RoomDefinition room,
            Vector2 endpoint,
            WallSide side)
        {
            var halfSize = room.Size * 0.5f;
            return side switch
            {
                WallSide.North =>
                    Mathf.Approximately(
                        endpoint.y,
                        room.Position.y + halfSize.y) &&
                    Mathf.Abs(endpoint.x - room.Position.x) <= halfSize.x,
                WallSide.South =>
                    Mathf.Approximately(
                        endpoint.y,
                        room.Position.y - halfSize.y) &&
                    Mathf.Abs(endpoint.x - room.Position.x) <= halfSize.x,
                WallSide.East =>
                    Mathf.Approximately(
                        endpoint.x,
                        room.Position.x + halfSize.x) &&
                    Mathf.Abs(endpoint.y - room.Position.y) <= halfSize.y,
                WallSide.West =>
                    Mathf.Approximately(
                        endpoint.x,
                        room.Position.x - halfSize.x) &&
                    Mathf.Abs(endpoint.y - room.Position.y) <= halfSize.y,
                _ => false
            };
        }

        private static void RequireComponent<T>(
            GameObject source,
            List<string> failures)
            where T : Component
        {
            if (source == null || source.GetComponent<T>() == null)
            {
                failures.Add(
                    "P_Player_Local is missing " + typeof(T).Name + ".");
            }
        }

        private static void MonitorRuntimeMonsterTest()
        {
            if (!EditorApplication.isPlaying || _runtimeTestMonster == null ||
                _runtimeTestTarget == null || _runtimeTestInfection == null)
            {
                StopRuntimeMonsterTest();
                return;
            }

            if (_runtimeTestMonster.State is MonsterState.Chase or MonsterState.Bite)
            {
                _runtimeTestObservedChase = true;
            }

            if (_runtimeTestTarget.BiteCount > _runtimeTestInitialBiteCount)
            {
                if (!_runtimeTestObservedChase ||
                    !_runtimeTestInfection.IsInfected ||
                    !Mathf.Approximately(
                        _runtimeTestInfection.DurationAtBiteSeconds,
                        90f))
                {
                    StopRuntimeMonsterTest();
                    throw new InvalidOperationException(
                        "The 2D chase, bite or infection result was invalid.");
                }

                if (_runtimeTestMonster.State == MonsterState.Patrol)
                {
                    _runtimeTestObservedPatrolAfterBite = true;
                }

                if (_runtimeTestObservedPatrolAfterBite)
                {
                    Debug.Log(
                        "[MonkeyLab] 2D monster chase, bite, infection and patrol release passed.");
                    StopRuntimeMonsterTest();
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - _runtimeTestStartedAt >
                RuntimeMonsterTestTimeoutSeconds)
            {
                var state = _runtimeTestMonster.State;
                var canDetect = _runtimeTestMonster.Senses.TryDetectTarget(
                    out var detectionType);
                var monsterCollider =
                    _runtimeTestMonster.GetComponent<Collider2D>();
                var targetCollider =
                    _runtimeTestTarget.GetComponent<Collider2D>();
                var surfaceDistance =
                    monsterCollider != null && targetCollider != null
                        ? monsterCollider.Distance(targetCollider).distance
                        : float.NaN;
                var hasClearPath =
                    _runtimeTestMonster.Senses.HasClearPathToTarget();
                var isInBiteRange = _runtimeTestMonster.Senses
                    .IsTargetInBiteRange();
                var biteController = _runtimeTestMonster.BiteController;
                var biteTargetMatches =
                    biteController.Target == _runtimeTestTarget;
                var isBiteProtected =
                    _runtimeTestTarget.IsBiteProtected(Time.time);
                var monsterPosition = _runtimeTestMonster.transform.position;
                var targetPosition = _runtimeTestTarget.transform.position;
                StopRuntimeMonsterTest();
                throw new InvalidOperationException(
                    $"2D monster chase and bite timed out. State={state}, " +
                    $"canDetect={canDetect}, detection={detectionType}, " +
                    $"pathClear={hasClearPath}, " +
                    $"biteRange={isInBiteRange}, " +
                    $"bitePending={biteController.IsPending}, " +
                    $"biteTargetMatches={biteTargetMatches}, " +
                    $"biteProtected={isBiteProtected}, " +
                    $"surfaceDistance={surfaceDistance:0.00}, " +
                    $"monster={monsterPosition}, target={targetPosition}.");
            }
        }

        private static void HandleRuntimeMonsterStateChanged(
            MonsterBrain monster,
            MonsterState state)
        {
            Debug.Log(
                $"[MonkeyLab] Runtime monster test state={state}, " +
                $"position={monster.transform.position}.");
        }

        private static void StopRuntimeMonsterTest()
        {
            EditorApplication.update -= MonitorRuntimeMonsterTest;
            if (_runtimeTestMonster != null)
            {
                _runtimeTestMonster.StateChanged -=
                    HandleRuntimeMonsterStateChanged;
            }

            _runtimeTestMonster = null;
            _runtimeTestTarget = null;
            _runtimeTestInfection = null;
        }

        private static void MonitorRuntimeAntidoteTest()
        {
            if (!EditorApplication.isPlaying ||
                _runtimeAntidoteTestInfection == null ||
                _runtimeAntidoteTestService == null)
            {
                StopRuntimeAntidoteTest();
                return;
            }

            if (_runtimeAntidoteTestInfection.State ==
                    PlayerLifeState.AliveHealthy &&
                !_runtimeAntidoteTestService.HasAntidote &&
                !_runtimeAntidoteTestService.IsUsing)
            {
                Debug.Log(
                    "[MonkeyLab] 2D infection and antidote validation passed.");
                StopRuntimeAntidoteTest();
                return;
            }

            if (EditorApplication.timeSinceStartup -
                _runtimeAntidoteTestStartedAt >
                RuntimeAntidoteTestTimeoutSeconds)
            {
                StopRuntimeAntidoteTest();
                throw new InvalidOperationException(
                    "Infection and antidote validation timed out.");
            }
        }

        private static void StopRuntimeAntidoteTest()
        {
            EditorApplication.update -= MonitorRuntimeAntidoteTest;
            _runtimeAntidoteTestInfection = null;
            _runtimeAntidoteTestService = null;
        }

        private readonly struct RoomDefinition
        {
            public RoomDefinition(
                string id,
                Vector2 position,
                Vector2 size,
                string displayName)
            {
                Id = id;
                Position = position;
                Size = size;
                DisplayName = displayName;
            }

            public string Id { get; }
            public Vector2 Position { get; }
            public Vector2 Size { get; }
            public string DisplayName { get; }
        }

        private readonly struct CctvDefinition
        {
            public CctvDefinition(string displayName, Vector2 position)
            {
                DisplayName = displayName;
                Position = position;
            }

            public string DisplayName { get; }
            public Vector2 Position { get; }
        }

        private readonly struct EnvironmentPropDefinition
        {
            public EnvironmentPropDefinition(
                string roomId,
                string assetKey,
                Vector2 localPosition,
                Vector2 footprint,
                EnvironmentPropCategory category,
                bool isObstacle,
                EnvironmentPropMountKind mountKind =
                    EnvironmentPropMountKind.FloorStanding,
                int sortingOrder = 8,
                bool hasStatusIndicator = false)
            {
                RoomId = roomId;
                AssetKey = assetKey;
                LocalPosition = localPosition;
                Footprint = footprint;
                Category = category;
                IsObstacle = isObstacle;
                MountKind = mountKind;
                SortingOrder = sortingOrder;
                HasStatusIndicator = hasStatusIndicator;
            }

            public string RoomId { get; }
            public string AssetKey { get; }
            public Vector2 LocalPosition { get; }
            public Vector2 Footprint { get; }
            public EnvironmentPropCategory Category { get; }
            public bool IsObstacle { get; }
            public EnvironmentPropMountKind MountKind { get; }
            public int SortingOrder { get; }
            public bool HasStatusIndicator { get; }
        }

        private readonly struct CorridorDefinition
        {
            public CorridorDefinition(
                string a,
                WallSide sideA,
                string b,
                WallSide sideB,
                params Vector2[] pathPoints)
            {
                A = a;
                SideA = sideA;
                B = b;
                SideB = sideB;
                PathPoints = pathPoints;
            }

            public string A { get; }
            public WallSide SideA { get; }
            public string B { get; }
            public WallSide SideB { get; }
            public IReadOnlyList<Vector2> PathPoints { get; }
            public Vector2 Start => PathPoints[0];
            public Vector2 End => PathPoints[^1];
        }

        private readonly struct BoundaryEdge
        {
            public BoundaryEdge(
                bool isHorizontal,
                float fixedCoordinate,
                float start,
                float end)
            {
                IsHorizontal = isHorizontal;
                FixedCoordinate = fixedCoordinate;
                Start = start;
                End = end;
            }

            public bool IsHorizontal { get; }
            public float FixedCoordinate { get; }
            public float Start { get; }
            public float End { get; }
        }

        private enum WallSide
        {
            North,
            South,
            East,
            West
        }

        private enum EnvironmentPropCategory
        {
            Common,
            Laboratory,
            Medical,
            Storage,
            Security,
            Power,
            Quarantine,
            Utility,
            Hazard
        }
    }
}
