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
using MonkeyLab.Presentation.Characters;
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
        private const string SurvivorMissionBalanceConfigPath =
            "Assets/_Project/Data/Balance/SO_SurvivorMissionBalance_Default.asset";
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
        private const string EnvironmentSpriteRoot =
            "Assets/_Project/Art/Sprites/Environment";
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

        // 걷기 프레임은 정지 그림과 달리 없어도 빌드를 막지 않는다.
        // 세 장이 모두 들어오면 접지A → 모음 → 접지B → 모음으로 순환한다(아트 가이드 §2.2).
        private const string PlayerWalkContactASpritePath =
            CharacterSpriteRoot + "/S_Player_Survivor_WalkA.png";
        private const string PlayerWalkPassSpritePath =
            CharacterSpriteRoot + "/S_Player_Survivor_WalkPass.png";
        private const string PlayerWalkContactBSpritePath =
            CharacterSpriteRoot + "/S_Player_Survivor_WalkB.png";
        private const string MonsterWalkContactASpritePath =
            CharacterSpriteRoot + "/S_Monkey_Mutant_WalkA.png";
        private const string MonsterWalkPassSpritePath =
            CharacterSpriteRoot + "/S_Monkey_Mutant_WalkPass.png";
        private const string MonsterWalkContactBSpritePath =
            CharacterSpriteRoot + "/S_Monkey_Mutant_WalkB.png";
        private const string CircleSpritePath = SpriteRoot + "/S_StatusCircle.asset";
        private const string FlashlightSpritePath = SpriteRoot + "/S_FlashlightCone.asset";
        private const string PanelSpritePath = SpriteRoot + "/S_MissionPanel.asset";

        // 회색상자 표현용 절차적 스프라이트. 늘린 흰 사각형 하나로 모든 것을 그리면
        // 형태가 구분되지 않으므로, 바닥은 타일 반복으로 벽과 프롭은 9-slice로 그려
        // 크기와 무관하게 이음새·외곽선 두께를 일정하게 유지한다(아트 가이드 §1.1).
        private const string RoomFloorTileSpritePath =
            SpriteRoot + "/S_FloorTile_Room.asset";
        private const string CorridorFloorTileSpritePath =
            SpriteRoot + "/S_FloorTile_Corridor.asset";
        private const string RoomFloorTileFinalSpritePath =
            EnvironmentSpriteRoot + "/T_FloorTile_Room.png";
        private const string CorridorFloorTileFinalSpritePath =
            EnvironmentSpriteRoot + "/T_FloorTile_Corridor.png";
        private const string WallSectionSpritePath =
            SpriteRoot + "/S_WallSection.asset";
        private const string WallFaceSpritePath =
            SpriteRoot + "/S_WallFace.asset";
        private const string WallSectionFinalSpritePath =
            EnvironmentSpriteRoot + "/T_WallSection.png";
        private const string WallFaceFinalSpritePath =
            EnvironmentSpriteRoot + "/T_WallFace.png";
        private const string DoorPanelFinalSpritePath =
            EnvironmentSpriteRoot + "/T_DoorPanel.png";
        private const string DoorFrameFinalSpritePath =
            EnvironmentSpriteRoot + "/T_DoorFrame.png";
        private const string RoomSignFinalSpritePath =
            EnvironmentSpriteRoot + "/T_RoomSignPanel.png";
        private const string FloorGuideFinalSpritePath =
            EnvironmentSpriteRoot + "/S_FloorGuideDecal.png";
        private const string CeilingLightFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CeilingLightPanel.png";
        private const string EmergencyBeaconFinalSpritePath =
            EnvironmentSpriteRoot + "/S_EmergencyBeacon.png";
        private const string WallMonitorFinalSpritePath =
            EnvironmentSpriteRoot + "/S_WallMonitor.png";
        private const string FireExtinguisherFinalSpritePath =
            EnvironmentSpriteRoot + "/S_FireExtinguisher.png";
        private const string TrashBinFinalSpritePath =
            EnvironmentSpriteRoot + "/S_TrashBin.png";
        private const string EmergencyPhoneFinalSpritePath =
            EnvironmentSpriteRoot + "/S_EmergencyPhone.png";
        private const string LabWorkbenchFinalSpritePath =
            EnvironmentSpriteRoot + "/S_LabWorkbench.png";
        private const string StorageCabinetFinalSpritePath =
            EnvironmentSpriteRoot + "/S_StorageCabinet.png";
        private const string ReagentShelfFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ReagentShelf.png";
        private const string RollingCartFinalSpritePath =
            EnvironmentSpriteRoot + "/S_RollingCart.png";
        private const string CentrifugeFinalSpritePath =
            EnvironmentSpriteRoot + "/S_Centrifuge.png";
        private const string MicroscopeFinalSpritePath =
            EnvironmentSpriteRoot + "/S_Microscope.png";
        private const string PharmaFridgeFinalSpritePath =
            EnvironmentSpriteRoot + "/S_PharmaFridge.png";
        private const string BiosafetyHoodFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BiosafetyHood.png";
        private const string ServerRackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ServerRack.png";
        private const string CctvMonitorWallFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CctvMonitorWall.png";
        private const string ElectronicMapTableFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ElectronicMapTable.png";
        private const string OperatorConsoleFinalSpritePath =
            EnvironmentSpriteRoot + "/S_OperatorConsole.png";
        private const string GeneratorFinalSpritePath =
            EnvironmentSpriteRoot + "/S_Generator.png";
        private const string BreakerBankFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BreakerBank.png";
        private const string CableReelFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CableReel.png";
        private const string BackupCellRackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BackupCellRack.png";
        private const string HospitalBedFinalSpritePath =
            EnvironmentSpriteRoot + "/S_HospitalBed.png";
        private const string CurtainRailFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CurtainRail.png";
        private const string IvStandFinalSpritePath =
            EnvironmentSpriteRoot + "/S_IvStand.png";
        private const string MedicalMonitorFinalSpritePath =
            EnvironmentSpriteRoot + "/S_MedicalMonitor.png";
        private const string MedicineCartFinalSpritePath =
            EnvironmentSpriteRoot + "/S_MedicineCart.png";
        private const string NurseStationFinalSpritePath =
            EnvironmentSpriteRoot + "/S_NurseStation.png";
        private const string OxygenPortsFinalSpritePath =
            EnvironmentSpriteRoot + "/S_OxygenPorts.png";
        private const string MedicineCabinetFinalSpritePath =
            EnvironmentSpriteRoot + "/S_MedicineCabinet.png";
        private const string BloodStainAFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BloodStain_A.png";
        private const string BloodStainBFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BloodStain_B.png";
        private const string TriageFloorNumbersFinalSpritePath =
            EnvironmentSpriteRoot + "/S_TriageFloorNumbers.png";
        private const string GlassCellWideFinalSpritePath =
            EnvironmentSpriteRoot + "/S_GlassCellWide.png";
        private const string GlassCellFinalSpritePath =
            EnvironmentSpriteRoot + "/S_GlassCell.png";
        private const string CagePodFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CagePod.png";
        private const string DeconUnitFinalSpritePath =
            EnvironmentSpriteRoot + "/S_DeconUnit.png";
        private const string ContainmentLockFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ContainmentLock.png";
        private const string ObservationConsoleFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ObservationConsole.png";
        private const string DeconShowerFinalSpritePath =
            EnvironmentSpriteRoot + "/S_DeconShower.png";
        private const string RestraintRailFinalSpritePath =
            EnvironmentSpriteRoot + "/S_RestraintRail.png";
        private const string RestraintControllerFinalSpritePath =
            EnvironmentSpriteRoot + "/S_RestraintController.png";
        private const string QuarantineWarningBeaconFinalSpritePath =
            EnvironmentSpriteRoot + "/S_QuarantineWarningBeacon.png";
        private const string ContainmentFloorGridFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ContainmentFloorGrid.png";
        private const string BrokenGlassAFinalSpritePath =
            EnvironmentSpriteRoot + "/S_BrokenGlass_A.png";
        private const string ContainmentFloorNumbersFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ContainmentFloorNumbers.png";
        private const string CryoTankFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CryoTank.png";
        private const string SampleDrumFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SampleDrum.png";
        private const string FrozenPipeFinalSpritePath =
            EnvironmentSpriteRoot + "/S_FrozenPipe.png";
        private const string TemperatureTerminalFinalSpritePath =
            EnvironmentSpriteRoot + "/S_TemperatureTerminal.png";
        private const string CoolantManifoldFinalSpritePath =
            EnvironmentSpriteRoot + "/S_CoolantManifold.png";
        private const string ColdShelfFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ColdShelf.png";
        private const string InsulatedPalletFinalSpritePath =
            EnvironmentSpriteRoot + "/S_InsulatedPallet.png";
        private const string FrostDrainFinalSpritePath =
            EnvironmentSpriteRoot + "/S_FrostDrain.png";
        private const string VialRackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_VialRack.png";
        private const string SterileBenchFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SterileBench.png";
        private const string DeconSinkFinalSpritePath =
            EnvironmentSpriteRoot + "/S_DeconSink.png";
        private const string PpeDispenserFinalSpritePath =
            EnvironmentSpriteRoot + "/S_PpeDispenser.png";
        private const string SterileFloorZoneFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SterileFloorZone.png";
        private const string InjectorTesterFinalSpritePath =
            EnvironmentSpriteRoot + "/S_InjectorTester.png";
        private const string MixingBenchFinalSpritePath =
            EnvironmentSpriteRoot + "/S_MixingBench.png";
        private const string SampleRackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SampleRack.png";
        private const string VentOutletFinalSpritePath =
            EnvironmentSpriteRoot + "/S_VentOutlet.png";
        private const string SpecimenScannerFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SpecimenScanner.png";
        private const string EyeWashStationFinalSpritePath =
            EnvironmentSpriteRoot + "/S_EyeWashStation.png";
        private const string OverheadServiceRailFinalSpritePath =
            EnvironmentSpriteRoot + "/S_OverheadServiceRail.png";
        private const string ChemicalSpillMarkFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ChemicalSpillMark.png";
        private const string ServerBackupRackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_ServerBackupRack.png";
        private const string SampleSealerFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SampleSealer.png";
        private const string PackageScannerFinalSpritePath =
            EnvironmentSpriteRoot + "/S_PackageScanner.png";
        private const string SealedCrateStackFinalSpritePath =
            EnvironmentSpriteRoot + "/S_SealedCrateStack.png";
        private const string WorldSignFontPath =
            "Assets/_Project/Art/Fonts/SCDream6.otf";

        /// <summary>
        /// 카메라를 향한 벽의 정면 높이(m). 어몽어스식 혼합 시점을 만드는 값이다.
        /// 바닥은 위에서 보고 벽은 정면으로 본다(아트 가이드 §1.1).
        /// 너무 높이면 위쪽 방 바닥을 가리고 너무 낮으면 탑뷰로 읽힌다.
        /// </summary>
        private const float WallFaceHeight =
            MixedPerspectiveSceneStyler.WallFaceHeight;
        private const string PropBodySpritePath =
            SpriteRoot + "/S_PropBody.asset";

        /// <summary>바닥 타일 한 장이 덮는 월드 크기(m). 64px / PPU 32 = 2m.</summary>
        private const float FloorTileWorldSize = 2f;

        private const int FloorTilePixels = 64;
        private const float FloorTilePixelsPerUnit =
            FloorTilePixels / FloorTileWorldSize;

        /// <summary>벽·프롭 9-slice 테두리. 이 폭만큼은 늘어나지 않는다.</summary>
        private const int SlicedSpriteBorderPixels = 8;

        private const float SlicedSpritePixelsPerUnit = 64f;

        /// <summary>프롭 카테고리 표식 스프라이트의 본래 월드 크기(32px / PPU 64).</summary>
        private const float PropIconSpriteWorldSize =
            32f / SlicedSpritePixelsPerUnit;

        /// <summary>
        /// 이 두께 미만의 설치물은 선으로 읽혀야 하므로 9-slice 몸체와 카테고리 표식을
        /// 붙이지 않는다. 9-slice 테두리 양쪽 합(0.25m)보다 커야 형태가 뭉개지지 않는다.
        /// </summary>
        private const float RuntimeMonsterTestTimeoutSeconds = 5f;
        private const float RuntimeAntidoteTestTimeoutSeconds = 3f;
        private const float CorridorWidth = 4.5f;
        private const float WallThickness = 0.32f;

        private static readonly string[] RoomOrder =
        {
            "VaccineA", "LabA", "QuarantineA", "Ward", "VaccineB",
            "Power", "Security", "QuarantineB", "LabB", "Storage"
        };

        /// <summary>방 바닥 타일 네 모서리의 리벳 중심(px).</summary>
        private static readonly Vector2[] FloorTileRivetCenters =
        {
            new(7f, 7f),
            new(FloorTilePixels - 8f, 7f),
            new(7f, FloorTilePixels - 8f),
            new(FloorTilePixels - 8f, FloorTilePixels - 8f)
        };

        /// <summary>분류가 없는 프롭 표식의 점 네 개 위치(px, 아이콘 중심 기준).</summary>
        private static readonly Vector2[] PropIconDotOffsets =
        {
            new(-5f, -5f),
            new(5f, -5f),
            new(-5f, 5f),
            new(5f, 5f)
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
            // 원래 (2.5, 4.1)이었으나 East 출입구 통행 구역(로컬 x 2~4.5, y -0.25~4.25)을
            // 막아 씬 생성이 실패했다. 냉장 선반 A와 B 사이의 빈 자리로 내린다.
            new("Storage", "SM_InsulatedPallet", new Vector2(2.9f, -3.4f), new Vector2(1.5f, 1.2f), EnvironmentPropCategory.Storage, true),
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
            new("Ward", "VFX_BloodStain_A", new Vector2(0.2f, 3.4f), new Vector2(1.8f, 0.8f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),
            new("Ward", "VFX_BloodStain_B", new Vector2(-0.8f, -3.2f), new Vector2(1.3f, 0.7f), EnvironmentPropCategory.Hazard, false, EnvironmentPropMountKind.FloorDecal, 2),
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
            // 원래 (3.4, -3.2)였으나 East 출입구 접근 통로(로컬 y -2.7~-1.3)를 0.16 침범해
            // 씬 생성이 실패했다. 세척 싱크와 겹치지 않는 선까지 내린다.
            new("LabB", "SM_PackageScanner", new Vector2(3.4f, -3.7f), new Vector2(0.9f, 1.5f), EnvironmentPropCategory.Security, true, hasStatusIndicator: true),
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
                baseMonsters,
                fuseStations);
            CreateAntidoteEconomy(prototypeRoot.transform, rooms, player);
            CreateVaccineARoomMissions(
                prototypeRoot.transform,
                rooms["VaccineA"],
                player);
            CreateLabARoomMissions(
                prototypeRoot.transform,
                rooms["LabA"],
                player);
            CreateQuarantineARoomMissions(
                prototypeRoot.transform,
                rooms["QuarantineA"],
                player);
            CreateQuarantineBRoomMissions(
                prototypeRoot.transform,
                rooms["QuarantineB"],
                player);
            CreateWardRoomMissions(
                prototypeRoot.transform,
                rooms["Ward"],
                player);
            CreateStorageRoomMissions(
                prototypeRoot.transform,
                rooms["Storage"],
                player);
            CreateSecurityRoomMissions(
                prototypeRoot.transform,
                rooms["Security"],
                player);
            CreatePowerRoomMissions(
                prototypeRoot.transform,
                rooms["Power"],
                player);
            CreateLabBRoomMissions(
                prototypeRoot.transform,
                rooms["LabB"],
                player);
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

        /// <summary>
        /// 절차적 스프라이트를 지우고 다시 만든다. <see cref="EnsureSprite"/>가 이미 있는
        /// 에셋을 그대로 돌려주므로, 픽셀 생성 함수를 고쳐도 이걸 거치지 않으면 화면이
        /// 그대로다. 에셋을 새로 만들면 GUID가 바뀌어 기존 참조가 끊기니, 곧바로
        /// <see cref="BuildCompleteTopDown"/>로 씬을 다시 생성해야 한다.
        /// </summary>
        [MenuItem("Tools/Monkey Lab/Regenerate Procedural Sprites")]
        public static void RegenerateProceduralSprites()
        {
            var deleted = 0;
            foreach (var path in ProceduralSpritePaths)
            {
                if (AssetDatabase.DeleteAsset(path))
                {
                    deleted++;
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureSpriteAssets();
            Debug.Log(
                $"[MonkeyLab] Regenerated procedural sprites ({deleted} replaced). " +
                "Run Build Complete 2D Top Down next so the scene picks them up.");
        }

        /// <summary>
        /// <see cref="RegenerateProceduralSprites"/>가 지우는 대상. 카테고리 표식은
        /// 열거형에서 만들어지므로 여기에 함께 모은다.
        /// </summary>
        private static IEnumerable<string> ProceduralSpritePaths
        {
            get
            {
                yield return UnitSpritePath;
                yield return VisorSpritePath;
                yield return CircleSpritePath;
                yield return PanelSpritePath;
                yield return FlashlightSpritePath;
                yield return RoomFloorTileSpritePath;
                yield return CorridorFloorTileSpritePath;
                yield return WallSectionSpritePath;
                yield return PropBodySpritePath;
                foreach (EnvironmentPropCategory category in
                         Enum.GetValues(typeof(EnvironmentPropCategory)))
                {
                    yield return GetPropIconSpritePath(category);
                }
            }
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
                GameObject.Find("[UI] InfectionHud")?.GetComponent<InfectionHudView>() == null ||
                GameObject.Find("[UI] AntidoteTerminal_01")?
                    .GetComponent<AntidoteTerminalView>() == null ||
                GameObject.Find("[UI] AntidoteKeypad_01")?
                    .GetComponent<AntidoteKeypadView>() == null)
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

            // 축 선택형 강화 스테이션(UpgradeStationPrototype)은 GDD 1.9에서
            // 빌런 전용 미션 6종 + 스택형 강화로 대체됐다(§13.2~13.3). 방별 위장
            // 미션 배치는 ValidateLabARoomMissions 등 방별 검증 함수가 담당한다.
            var stackAuthority =
                GameObject.Find("[Network] VillainMissionStackAuthority")?
                    .GetComponent<NetworkVillainMissionStackAuthority>();
            var populationSpawner =
                GameObject.Find("[Network] MonsterPopulationSpawner")?
                    .GetComponent<NetworkMonsterPopulationSpawner>();
            if (stackAuthority == null ||
                populationSpawner == null ||
                populationSpawner.TierConfig == null ||
                !populationSpawner.MatchesBalanceTable(0) ||
                !populationSpawner.MatchesBalanceTable(1) ||
                !populationSpawner.MatchesBalanceTable(2))
            {
                failures.Add(
                    "The villain mission stack authority setup does not match the monster tier table.");
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
                clueAuthority.UpgradeConfig == null ||
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
            ValidateVaccineARoomMissions(failures);
            ValidateLabARoomMissions(failures);
            ValidateQuarantineARoomMissions(failures);
            ValidateQuarantineBRoomMissions(failures);
            ValidateWardRoomMissions(failures);
            ValidateStorageRoomMissions(failures);
            ValidateSecurityRoomMissions(failures);
            ValidatePowerRoomMissions(failures);
            ValidateLabBRoomMissions(failures);

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
                CreateTiledSpriteObject(
                    "Room_" + room.Id,
                    LoadPreferredSprite(
                        RoomFloorTileFinalSpritePath,
                        RoomFloorTileSpritePath),
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
                LoadPreferredSprite(
                    WallSectionFinalSpritePath,
                    WallSectionSpritePath),
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
            CreateTiledSpriteObject(
                $"Corridor_{name}_{segmentIndex:00}",
                LoadPreferredSprite(
                    CorridorFloorTileFinalSpritePath,
                    CorridorFloorTileSpritePath),
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
            Sprite wallSprite,
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
                        // 바닥이 이 벽 아래에 있으므로 정면이 카메라를 향한다.
                        edges.Add(new BoundaryEdge(
                            true,
                            yMax,
                            xMin,
                            xMax,
                            hasVisibleFace: true));
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
                    wallSprite,
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
            if (fixedComparison != 0)
            {
                return fixedComparison;
            }

            // 같은 좌표라도 입면 유무가 다르면 하나로 합치면 안 된다.
            var faceComparison =
                left.HasVisibleFace.CompareTo(right.HasVisibleFace);
            return faceComparison != 0
                ? faceComparison
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
                    previous.HasVisibleFace == edge.HasVisibleFace &&
                    Mathf.Approximately(
                        previous.FixedCoordinate,
                        edge.FixedCoordinate) &&
                    edge.Start <= previous.End + 0.001f)
                {
                    merged[^1] = new BoundaryEdge(
                        previous.IsHorizontal,
                        previous.FixedCoordinate,
                        previous.Start,
                        Mathf.Max(previous.End, edge.End),
                        previous.HasVisibleFace);
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

            if (!edge.HasVisibleFace)
            {
                return;
            }

            // 벽 정면은 방 안쪽(아래)으로 내려온다. 바닥 위·프롭 아래에 두어
            // 방에 서 있는 것들이 항상 벽 앞에 그려지게 한다.
            CreateSlicedSpriteObject(
                $"WallFace_{index:000}",
                LoadPreferredSprite(
                    WallFaceFinalSpritePath,
                    WallFaceSpritePath),
                new Vector2(
                    position.x,
                    edge.FixedCoordinate - WallFaceHeight * 0.5f),
                new Vector2(length + WallThickness, WallFaceHeight),
                Color.white,
                2,
                parent);
        }

        private static GameObject CreateWall(
            string name,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Transform parent)
        {
            // 벽 스프라이트는 최종 색을 이미 굽고 있으므로 틴트는 흰색으로 둔다.
            var wall = CreateSlicedSpriteObject(
                name,
                sprite,
                position,
                size,
                Color.white,
                20,
                parent);
            var collider = wall.AddComponent<BoxCollider2D>();

            // 9-slice는 localScale을 1로 두므로 콜라이더에 실제 크기를 직접 준다.
            // 예전처럼 Vector2.one을 주면 스케일 배율이 사라져 벽이 통과된다.
            collider.size = size;
            return wall;
        }

        private static void CreateRoomLabel(
            RoomDefinition room,
            Transform parent)
        {
            CreateWorldSign(
                "Label_" + room.Id,
                room.DisplayName,
                new Vector2(
                    room.Position.x,
                    room.Position.y + room.Size.y * 0.36f),
                new Vector2(3.8f, 0.92f),
                parent,
                panelSortingOrder: 3,
                textSortingOrder: 4,
                characterSize: 0.072f);
        }

        private static void CreateWorldSign(
            string name,
            string text,
            Vector2 position,
            Vector2 size,
            Transform parent,
            int panelSortingOrder,
            int textSortingOrder,
            float characterSize)
        {
            var finalPanelSprite = LoadSprite(
                RoomSignFinalSpritePath,
                throwIfMissing: false);
            var panel = CreateSlicedSpriteObject(
                name,
                finalPanelSprite != null
                    ? finalPanelSprite
                    : LoadSprite(PanelSpritePath),
                position,
                size,
                finalPanelSprite != null
                    ? Color.white
                    : new Color(0.08f, 0.14f, 0.18f, 0.92f),
                panelSortingOrder,
                parent);

            var font = AssetDatabase.LoadAssetAtPath<Font>(WorldSignFontPath) ??
                       Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException(
                    "The RX-9 world-sign font could not be loaded.");
            }

            var labelObject = new GameObject("DynamicRoomName");
            labelObject.transform.SetParent(panel.transform);
            labelObject.transform.localPosition = Vector3.zero;
            var label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.text = text;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.93f, 0.97f, 0.98f, 1f);
            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = textSortingOrder;
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

                var prop = CreateEnvironmentProp(
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
                    definition.HasStatusIndicator,
                    definition.Category);
                var finalSpritePath = GetLabBCoreFinalSpritePath(
                    definition.AssetKey) ??
                    GetLabASupportFinalSpritePath(
                    definition.AssetKey) ??
                    GetVaccineSupportFinalSpritePath(
                    definition.AssetKey) ??
                    GetRoomFurnitureFinalSpritePath(
                    definition.AssetKey) ??
                    GetLabEquipmentFinalSpritePath(definition.AssetKey) ??
                    GetSecurityEquipmentFinalSpritePath(definition.AssetKey) ??
                    GetPowerEquipmentFinalSpritePath(definition.AssetKey) ??
                    GetWardEquipmentFinalSpritePath(definition.AssetKey) ??
                    GetStorageEquipmentFinalSpritePath(definition.AssetKey) ??
                    GetQuarantineEquipmentFinalSpritePath(
                        definition.AssetKey);
                if (!string.IsNullOrEmpty(finalSpritePath))
                {
                    var currentBounds = prop.PlaceholderRenderer.bounds.size;
                    ApplyFinalFixtureSprite(
                        prop,
                        finalSpritePath,
                        new Vector2(currentBounds.x, currentBounds.y),
                        useUnlitMaterial:
                            UsesUnlitFinalRoomProp(definition.AssetKey),
                        hideSecondaryPlaceholders: true);
                }
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
            var mountKinds = new[]
            {
                EnvironmentPropMountKind.WallMounted,
                EnvironmentPropMountKind.WallMounted,
                EnvironmentPropMountKind.FloorStanding,
                EnvironmentPropMountKind.WallMounted
            };
            var finalSpritePaths = new[]
            {
                WallMonitorFinalSpritePath,
                FireExtinguisherFinalSpritePath,
                TrashBinFinalSpritePath,
                EmergencyPhoneFinalSpritePath
            };
            for (var index = 0; index < assetKeys.Length; index++)
            {
                var fixture = CreateEnvironmentProp(
                    parent,
                    room.Id,
                    assetKeys[index],
                    index,
                    positions[index],
                    sizes[index],
                    GetEnvironmentPropColor(categories[index]),
                    isObstacle: false,
                    unitSprite,
                    showLabel: false,
                    mountKinds[index],
                    category: categories[index]);
                ApplyFinalFixtureSprite(
                    fixture,
                    finalSpritePaths[index],
                    sizes[index],
                    useUnlitMaterial: index == 0,
                    hideSecondaryPlaceholders: true);
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

            var lightOffsetX = Mathf.Min(2.8f, room.Size.x * 0.22f);
            for (var index = 0; index < 2; index++)
            {
                var lightSize = new Vector2(1.8f, 0.48f);
                var light = CreateEnvironmentProp(
                    architectureRoot,
                    room.Id,
                    "VFX_CeilingLightPanel",
                    index,
                    room.Position + new Vector2(
                        index == 0 ? -lightOffsetX : lightOffsetX,
                        room.Size.y * 0.18f),
                    lightSize,
                    new Color(0.52f, 0.86f, 0.90f, 0.82f),
                    isObstacle: false,
                    unitSprite,
                    showLabel: false,
                    EnvironmentPropMountKind.Overhead,
                    sortingOrder: 6);
                ApplyFinalFixtureSprite(
                    light,
                    CeilingLightFinalSpritePath,
                    lightSize);
            }

            var beaconSize = new Vector2(0.52f, 0.52f);
            var beacon = CreateEnvironmentProp(
                architectureRoot,
                room.Id,
                "VFX_EmergencyBeacon",
                0,
                room.Position + new Vector2(
                    0f,
                    halfSize.y - 0.62f),
                beaconSize,
                new Color(0.90f, 0.28f, 0.16f, 0.92f),
                isObstacle: false,
                unitSprite,
                showLabel: false,
                EnvironmentPropMountKind.Overhead,
                sortingOrder: 7);
            ApplyFinalFixtureSprite(
                beacon,
                EmergencyBeaconFinalSpritePath,
                beaconSize);

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
            bool hasStatusIndicator = false,
            EnvironmentPropCategory category = EnvironmentPropCategory.Common)
        {
            var root = new GameObject(
                $"PROP_{roomId}_{assetKey}_{instanceIndex:00}");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var placeholderRenderers = new List<SpriteRenderer>(4);

            // 벽 몰딩·배선 덕트·바닥 유도선처럼 얇은 장식은 선으로 읽혀야 한다.
            // 여기에 둥근 외곽선 몸체나 표식을 얹으면 오히려 형태가 뭉개지므로,
            // 두께가 충분한 설치물에만 상세 표현을 준다.
            var hasDetailedBody =
                (mountKind is EnvironmentPropMountKind.FloorStanding or
                    EnvironmentPropMountKind.WallMounted) &&
                Mathf.Min(footprint.x, footprint.y) >=
                EnvironmentPropSlot.DetailedPropMinimumExtent;

            if (mountKind == EnvironmentPropMountKind.FloorStanding &&
                hasDetailedBody)
            {
                // 세워 그린 프롭은 그림자가 발밑에 남아 접지면을 만든다.
                // 이게 없으면 물체가 공중에 떠 보인다.
                var shadowGroundY =
                    position.y - footprint.y * 0.5f;
                var shadowOrder = YSortedRenderer.GetSortingOrder(
                    shadowGroundY) - 1;
                var shadow = CreateSpriteObject(
                    "PlaceholderShadow",
                    unitSprite,
                    new Vector2(
                        position.x +
                        EnvironmentPropSlot.ShadowHorizontalOffset,
                        shadowGroundY +
                        EnvironmentPropSlot.ShadowGroundOffset),
                    new Vector2(
                        footprint.x +
                        EnvironmentPropSlot.ShadowWidthPadding,
                        Mathf.Max(
                            EnvironmentPropSlot.ShadowMinimumDepth,
                            footprint.y *
                            EnvironmentPropSlot.ShadowDepthScale)),
                    new Color(0f, 0f, 0f, 0.26f),
                    shadowOrder,
                    root.transform);
                placeholderRenderers.Add(shadow.GetComponent<SpriteRenderer>());
            }

            // 세워 그리는 프롭은 footprint를 발이 닿는 자리로 보고 위로 올라간다.
            // 눕혀 그리면 캐릭터·벽만 서 있고 프롭만 바닥에 붙어 시점이 어긋난다
            // (아트 가이드 §1.1 혼합 시점).
            var isElevated = hasDetailedBody &&
                mountKind == EnvironmentPropMountKind.FloorStanding;
            var groundY = position.y - footprint.y * 0.5f;
            var visualHeight = isElevated
                ? EnvironmentPropSlot.GetMixedPerspectiveVisualHeight(
                    footprint)
                : footprint.y;
            var visualPosition = isElevated
                ? new Vector2(position.x, groundY + visualHeight * 0.5f)
                : position;
            var visualSize = new Vector2(footprint.x, visualHeight);
            var visualOrder = isElevated
                ? YSortedRenderer.GetSortingOrder(groundY)
                : sortingOrder;

            var visual = hasDetailedBody
                ? CreateSlicedSpriteObject(
                    "PlaceholderVisual",
                    LoadSprite(PropBodySpritePath),
                    visualPosition,
                    visualSize,
                    color,
                    visualOrder,
                    root.transform)
                : CreateSpriteObject(
                    "PlaceholderVisual",
                    unitSprite,
                    visualPosition,
                    visualSize,
                    color,
                    visualOrder,
                    root.transform);
            var mainRenderer = visual.GetComponent<SpriteRenderer>();
            placeholderRenderers.Add(mainRenderer);

            if (hasDetailedBody)
            {
                // 카테고리 표식. 프롭이 커도 작아도 읽혀야 하므로 footprint에 비례시키되
                // 상·하한을 둔다. CreateSpriteObject는 size를 localScale로 쓰므로
                // 원하는 월드 크기를 스프라이트 본래 크기로 나눠 배율을 만든다.
                var iconWorldExtent = Mathf.Clamp(
                    Mathf.Min(footprint.x, footprint.y) * 0.55f,
                    0.18f,
                    0.52f);
                var iconScale = iconWorldExtent / PropIconSpriteWorldSize;
                var icon = CreateSpriteObject(
                    "PlaceholderCategoryIcon",
                    LoadSprite(GetPropIconSpritePath(category)),
                    visualPosition,
                    new Vector2(iconScale, iconScale),
                    Color.white,
                    visualOrder + 1,
                    root.transform);
                placeholderRenderers.Add(icon.GetComponent<SpriteRenderer>());
            }

            // 상세 몸체는 스프라이트가 이미 상단 하이라이트를 갖고 있어 덧칠하지 않는다.
            if (!hasDetailedBody &&
                (mountKind is EnvironmentPropMountKind.FloorStanding or
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
                    visualPosition + new Vector2(
                        footprint.x * 0.32f,
                        visualSize.y * 0.28f),
                    new Vector2(0.20f, 0.20f),
                    new Color(0.20f, 0.95f, 0.76f, 1f),
                    visualOrder + 2,
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

        private static void ApplyFinalFixtureSprite(
            EnvironmentPropSlot slot,
            string spritePath,
            Vector2 visualSize,
            bool useUnlitMaterial = true,
            bool hideSecondaryPlaceholders = false)
        {
            var sprite = LoadSprite(spritePath, throwIfMissing: false);
            if (sprite == null)
            {
                return;
            }

            var renderer = slot.PlaceholderRenderer;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            if (useUnlitMaterial)
            {
                renderer.sharedMaterial = GetIndicatorUnlitMaterial();
            }
            renderer.transform.localScale = new Vector3(
                visualSize.x / sprite.bounds.size.x,
                visualSize.y / sprite.bounds.size.y,
                1f);
            slot.ApplyMixedPerspectivePresentation();
            if (!hideSecondaryPlaceholders)
            {
                return;
            }

            foreach (var placeholder in slot.PlaceholderRenderers)
            {
                if (placeholder != null &&
                    placeholder != renderer &&
                    placeholder.gameObject.name != "PlaceholderShadow")
                {
                    placeholder.enabled = false;
                }
            }

            var placeholderLabel = slot.transform.Find("PlaceholderLabel");
            if (placeholderLabel != null &&
                placeholderLabel.TryGetComponent<Renderer>(out var labelRenderer))
            {
                labelRenderer.enabled = false;
            }
        }

        private static string GetRoomFurnitureFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_SterileBench" => SterileBenchFinalSpritePath,
                "SM_LabBench_Long" or
                "SM_PackagingBench" => LabWorkbenchFinalSpritePath,
                "SM_ColdCabinet" or
                "SM_ColdCabinet_B" or
                "SM_ToolCabinet" => StorageCabinetFinalSpritePath,
                "SM_VialRack" or
                "SM_VialRack_B" => VialRackFinalSpritePath,
                "SM_ChemicalShelf" or
                "SM_ChemicalShelf_B" => ReagentShelfFinalSpritePath,
                "SM_ColdShelf_A" or
                "SM_ColdShelf_B" => ColdShelfFinalSpritePath,
                "SM_VialCart" or
                "SM_VialCart_B" => RollingCartFinalSpritePath,
                _ => null
            };
        }

        private static string GetLabEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_Centrifuge" or
                "SM_Centrifuge_Industrial" => CentrifugeFinalSpritePath,
                "SM_Microscope" => MicroscopeFinalSpritePath,
                "SM_PharmaFridge" or
                "SM_PharmaFridge_B" => PharmaFridgeFinalSpritePath,
                "SM_BiosafetyHood" or
                "SM_BiosafetyHood_B" => BiosafetyHoodFinalSpritePath,
                _ => null
            };
        }

        private static string GetSecurityEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_ServerRack_A" or
                "SM_ServerRack_B" => ServerRackFinalSpritePath,
                "SM_CctvMonitorWall" => CctvMonitorWallFinalSpritePath,
                "SM_ElectronicMapTable" => ElectronicMapTableFinalSpritePath,
                "SM_OperatorConsole_A" or
                "SM_OperatorConsole_B" => OperatorConsoleFinalSpritePath,
                _ => null
            };
        }

        private static string GetPowerEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_Generator" => GeneratorFinalSpritePath,
                "SM_BreakerBank" => BreakerBankFinalSpritePath,
                "SM_CableReel_A" or
                "SM_CableReel_B" => CableReelFinalSpritePath,
                "SM_BackupCellRack" => BackupCellRackFinalSpritePath,
                _ => null
            };
        }

        private static string GetWardEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_HospitalBed_A" or
                "SM_HospitalBed_B" or
                "SM_HospitalBed_C" or
                "SM_HospitalBed_D" => HospitalBedFinalSpritePath,
                "SM_CurtainRail_A" or
                "SM_CurtainRail_B" => CurtainRailFinalSpritePath,
                "SM_IvStand_A" or
                "SM_IvStand_B" => IvStandFinalSpritePath,
                "SM_MedicalMonitor_A" or
                "SM_MedicalMonitor_B" => MedicalMonitorFinalSpritePath,
                "SM_MedicineCart" => MedicineCartFinalSpritePath,
                "SM_NurseStation" => NurseStationFinalSpritePath,
                "SM_OxygenPorts_A" or
                "SM_OxygenPorts_B" => OxygenPortsFinalSpritePath,
                "SM_MedicineCabinet" => MedicineCabinetFinalSpritePath,
                "VFX_BloodStain_A" => BloodStainAFinalSpritePath,
                "VFX_BloodStain_B" => BloodStainBFinalSpritePath,
                "VFX_TriageFloorNumbers" =>
                    TriageFloorNumbersFinalSpritePath,
                _ => null
            };
        }

        private static string GetQuarantineEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_GlassCell_Wide" => GlassCellWideFinalSpritePath,
                "SM_GlassCell_A" or
                "SM_GlassCell_B" or
                "SM_GlassCell_C" => GlassCellFinalSpritePath,
                "SM_CagePod_A" or
                "SM_CagePod_B" => CagePodFinalSpritePath,
                "SM_DeconUnit_A" or
                "SM_DeconUnit_B" => DeconUnitFinalSpritePath,
                "SM_ContainmentLock" or
                "SM_ContainmentLock_B" => ContainmentLockFinalSpritePath,
                "SM_ObservationConsole" or
                "SM_ObservationConsole_B" =>
                    ObservationConsoleFinalSpritePath,
                "SM_DeconShower" or
                "SM_DeconShower_B" => DeconShowerFinalSpritePath,
                "SM_RestraintRail" => RestraintRailFinalSpritePath,
                "SM_RestraintController" =>
                    RestraintControllerFinalSpritePath,
                "VFX_WarningBeacon_A" or
                "VFX_WarningBeacon_B" or
                "VFX_QuarantineWarning" =>
                    QuarantineWarningBeaconFinalSpritePath,
                "VFX_ContainmentFloorGrid" =>
                    ContainmentFloorGridFinalSpritePath,
                "VFX_BrokenGlass_A" => BrokenGlassAFinalSpritePath,
                "VFX_ContainmentFloorNumbers" =>
                    ContainmentFloorNumbersFinalSpritePath,
                _ => null
            };
        }

        private static string GetStorageEquipmentFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_CryoTank_A" or
                "SM_CryoTank_B" or
                "SM_CryoTank_C" => CryoTankFinalSpritePath,
                "SM_SampleDrum" => SampleDrumFinalSpritePath,
                "SM_FrozenPipe" => FrozenPipeFinalSpritePath,
                "SM_TemperatureTerminal" =>
                    TemperatureTerminalFinalSpritePath,
                "SM_CoolantManifold" => CoolantManifoldFinalSpritePath,
                "SM_InsulatedPallet" => InsulatedPalletFinalSpritePath,
                "VFX_FrostDrain" => FrostDrainFinalSpritePath,
                _ => null
            };
        }

        private static string GetVaccineSupportFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_DeconSink" or
                "SM_DeconSink_B" => DeconSinkFinalSpritePath,
                "SM_PpeDispenser" or
                "SM_PpeDispenser_B" => PpeDispenserFinalSpritePath,
                "VFX_SterileFloorZone" or
                "VFX_SterileFloorZone_B" =>
                    SterileFloorZoneFinalSpritePath,
                "SM_InjectorTester" => InjectorTesterFinalSpritePath,
                "SM_MixingBench" => MixingBenchFinalSpritePath,
                _ => null
            };
        }

        private static string GetLabASupportFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "SM_SampleRack" => SampleRackFinalSpritePath,
                "SM_VentOutlet" => VentOutletFinalSpritePath,
                "SM_SpecimenScanner" => SpecimenScannerFinalSpritePath,
                "SM_EyeWashStation" => EyeWashStationFinalSpritePath,
                "SM_OverheadServiceRail" =>
                    OverheadServiceRailFinalSpritePath,
                _ => null
            };
        }

        private static string GetLabBCoreFinalSpritePath(
            string assetKey)
        {
            return assetKey switch
            {
                "VFX_ChemicalSpillMark" =>
                    ChemicalSpillMarkFinalSpritePath,
                "SM_ServerBackupRack" => ServerBackupRackFinalSpritePath,
                "SM_SampleSealer" => SampleSealerFinalSpritePath,
                "SM_PackageScanner" => PackageScannerFinalSpritePath,
                "SM_SealedCrateStack" => SealedCrateStackFinalSpritePath,
                _ => null
            };
        }

        private static bool UsesUnlitFinalRoomProp(string assetKey)
        {
            return assetKey switch
            {
                "SM_ServerRack_A" or
                "SM_ServerRack_B" or
                "SM_CctvMonitorWall" or
                "SM_ElectronicMapTable" or
                "SM_OperatorConsole_A" or
                "SM_OperatorConsole_B" or
                "SM_MedicalMonitor_A" or
                "SM_MedicalMonitor_B" or
                "SM_ObservationConsole" or
                "SM_ObservationConsole_B" or
                "VFX_WarningBeacon_A" or
                "VFX_WarningBeacon_B" or
                "VFX_QuarantineWarning" or
                "VFX_ContainmentFloorGrid" or
                "VFX_ContainmentFloorNumbers" or
                "VFX_SterileFloorZone" or
                "VFX_SterileFloorZone_B" or
                "SM_InjectorTester" or
                "SM_SpecimenScanner" or
                "SM_ServerBackupRack" or
                "SM_PackageScanner" or
                "SM_TemperatureTerminal" => true,
                _ => false
            };
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
            var finalGuideSprite = LoadSprite(
                FloorGuideFinalSpritePath,
                throwIfMissing: false);
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
                        var guideSize = finalGuideSprite != null
                            ? new Vector2(0.95f, 0.24f)
                            : isHorizontal
                                ? new Vector2(0.9f, 0.18f)
                                : new Vector2(0.18f, 0.9f);
                        var guideFixture = CreateEnvironmentProp(
                            root,
                            "Corridor",
                            "VFX_FloorGuideLight",
                            fixtureIndex++,
                            Vector2.Lerp(start, end, normalized),
                            guideSize,
                            finalGuideSprite != null
                                ? Color.white
                                : new Color(0.06f, 0.22f, 0.24f, 0.28f),
                            isObstacle: false,
                            unitSprite,
                            showLabel: false,
                            EnvironmentPropMountKind.FloorDecal,
                            sortingOrder: 2);
                        var guideRenderer = guideFixture.PlaceholderRenderer;
                        guideRenderer.sharedMaterial =
                            GetIndicatorUnlitMaterial();
                        if (finalGuideSprite != null)
                        {
                            guideRenderer.sprite = finalGuideSprite;
                            guideRenderer.color = Color.white;
                            guideRenderer.transform.localScale = new Vector3(
                                guideSize.x / finalGuideSprite.bounds.size.x,
                                guideSize.y / finalGuideSprite.bounds.size.y,
                                1f);
                            guideRenderer.flipX = isHorizontal
                                ? delta.x < 0f
                                : delta.y < 0f;
                            guideFixture.transform.rotation = Quaternion.Euler(
                                0f,
                                0f,
                                isHorizontal ? 0f : 90f);
                        }
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
            var visualCenter = isHorizontalWall
                ? position + Vector2.up *
                    (EnvironmentPropSlot.DoorFrontTopOffset -
                     EnvironmentPropSlot.DoorFrontFaceHeight * 0.5f)
                : position;
            var panelSpan = (CorridorWidth - 0.24f) * 0.5f;
            var panelSize = isHorizontalWall
                ? new Vector2(
                    panelSpan,
                    EnvironmentPropSlot.DoorFrontFaceHeight)
                : new Vector2(
                    EnvironmentPropSlot.DoorPanelDepth,
                    panelSpan);
            var panelOffset = isHorizontalWall
                ? new Vector2(panelSpan * 0.5f, 0f)
                : new Vector2(0f, panelSpan * 0.5f);
            var finalPanelSprite = LoadSprite(
                DoorPanelFinalSpritePath,
                throwIfMissing: false);
            var panelSprite = finalPanelSprite != null
                ? finalPanelSprite
                : unitSprite;
            var panelColor = finalPanelSprite != null
                ? Color.white
                : new Color(0.26f, 0.62f, 0.70f);
            var panelA = CreateSlicedSpriteObject(
                "Panel_A",
                panelSprite,
                visualCenter - panelOffset,
                panelSize,
                panelColor,
                35,
                root.transform);
            var panelB = CreateSlicedSpriteObject(
                "Panel_B",
                panelSprite,
                visualCenter + panelOffset,
                panelSize,
                panelColor,
                35,
                root.transform);

            var frameAxis = isHorizontalWall
                ? Vector2.right
                : Vector2.up;
            var frameOffset = frameAxis *
                              (CorridorWidth * 0.5f + 0.16f);
            var frameSize = isHorizontalWall
                ? new Vector2(
                    EnvironmentPropSlot.DoorFrameThickness,
                    EnvironmentPropSlot.DoorFrontFrameHeight)
                : new Vector2(
                    EnvironmentPropSlot.DoorFrameSpan,
                    EnvironmentPropSlot.DoorFrameThickness);
            var finalFrameSprite = LoadSprite(
                DoorFrameFinalSpritePath,
                throwIfMissing: false);
            var frameSprite = finalFrameSprite != null
                ? finalFrameSprite
                : unitSprite;
            var frameColor = finalFrameSprite != null
                ? Color.white
                : new Color(0.08f, 0.12f, 0.15f);
            var frameA = CreateSlicedSpriteObject(
                "Frame_A",
                frameSprite,
                visualCenter - frameOffset,
                frameSize,
                frameColor,
                36,
                root.transform);
            var frameB = CreateSlicedSpriteObject(
                "Frame_B",
                frameSprite,
                visualCenter + frameOffset,
                frameSize,
                frameColor,
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
                    visualCenter - frameOffset,
                    indicatorSize,
                    new Color(0.10f, 0.62f, 0.72f),
                    37,
                    root.transform).GetComponent<SpriteRenderer>(),
                CreateSpriteObject(
                    "Status_B",
                    unitSprite,
                    visualCenter + frameOffset,
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
            if (TryGetRoomDefinition(roomId, out var room))
            {
                CreateWorldSign(
                    "Nameplate_" + roomId,
                    room.DisplayName,
                    position + GetRoomInwardDirection(wallSide) * 0.92f,
                    new Vector2(2.65f, 0.68f),
                    root.transform,
                    panelSortingOrder: 38,
                    textSortingOrder: 39,
                    characterSize: 0.050f);
            }

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
            player.AddComponent<InteractableHighlightDriver>()
                .Configure(interactionConfig.GeneralInteractionRangeMeters);
            var monsterTarget = player.AddComponent<MonsterTarget>();
            monsterTarget.Configure(true, true);

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
            flashlightController.BindStealthVisibility(personalGlow);
            var motionFeel =
                parent.GetComponent<PlayerMotionFeel>() ??
                parent.gameObject.AddComponent<PlayerMotionFeel>();
            // 측면 프로필 캐릭터는 이동 방향으로 뒤집는다(아트 가이드 §1.1).
            // 손전등 원뿔은 AimPivot이 회전시키므로 플립 대상이 아니다.
            motionFeel.Configure(
                parent,
                bodyObject.transform,
                bodyObject.GetComponentsInChildren<SpriteRenderer>(true));

            // 세워 그린 프롭과 앞뒤가 맞으려면 캐릭터도 같은 Y 정렬 대역을 쓴다.
            // 발이 닿는 지점은 몸통 중심보다 아래이므로 그만큼 내려 잡는다.
            var ySort =
                parent.GetComponent<YSortedRenderer>() ??
                parent.gameObject.AddComponent<YSortedRenderer>();
            ySort.Configure(
                new[] { bodyObject.GetComponent<SpriteRenderer>() },
                groundOffsetY: -0.6f);

            // 걷기 프레임은 그림이 들어오는 순간 자동으로 붙는다. 좌우 플립은
            // PlayerMotionFeel이 이미 맡고 있으므로 이 컴포넌트는 프레임만 넘긴다.
            var bodyRenderer = bodyObject.GetComponent<SpriteRenderer>();
            var walkAnimator =
                parent.GetComponent<CharacterWalkAnimator>() ??
                parent.gameObject.AddComponent<CharacterWalkAnimator>();
            walkAnimator.Configure(
                parent,
                bodyRenderer,
                bodyRenderer.sprite,
                LoadWalkCycle(
                    PlayerWalkContactASpritePath,
                    PlayerWalkPassSpritePath,
                    PlayerWalkContactBSpritePath),
                shouldControlFacing: false);
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
            follow.Configure(
                player,
                TopDownCamera.DefaultOrthographicSize);

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

            var stationSize = new Vector2(2.1f, 1.75f);
            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                room.Position + localOffset,
                stationSize,
                GetMissionStationColor(kind),
                30,
                parent);
            AttachInteractableHighlight(station, stationSize, 29);
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
                (ClueKind.EmptySyringe, "VaccineA", new Vector2(-3.2f, 1.2f)),
                // 투약 기록 삭제 → 입원실 파쇄기 옆 종이 조각
                (ClueKind.ShreddedMedicationRecord, "Ward", new Vector2(2f, -4f)),
                // 보안 카메라 선 꼬기 → 중앙 보안 광장의 꺼진 CCTV 채널
                (ClueKind.SeveredCameraFeed, "Security", new Vector2(4f, 5.2f)),
                // 메인 전력선 절단 → 전력 복구실의 잘린 전선 다발
                (ClueKind.CutPowerLine, "Power", new Vector2(-3f, 4.2f))
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
        /// 일반 미션 패널과 정확히 겹치는 빌런 미션 프록시를 만든다.
        /// 자체 렌더러는 숨기며 별도 점유를 사용해 생존자 미션과 동시에 수행할 수 있다.
        /// </summary>
        private static UpgradeStationPrototype CreateUpgradeStation(
            Transform parent,
            FuseStationPrototype disguiseStation,
            string stationName,
            UpgradeAxis axis,
            string roomId)
        {
            var station = CreateSpriteObject(
                stationName,
                LoadSprite(PanelSpritePath),
                disguiseStation.transform.position,
                new Vector2(2.1f, 1.75f),
                Color.clear,
                30,
                parent);
            station.GetComponent<SpriteRenderer>().enabled = false;
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
            NetworkMonsterAuthority[] baseMonsters,
            IReadOnlyList<FuseStationPrototype> missionStations)
        {
            // GDD §13.2: 빌런 전용 미션은 같은 방 생존자 미션과 같은 자리·외형을
            // 공유하는 위장 오브젝트다. 방별 개별 구현은 순차 진행 중이며,
            // 현재는 실험실 A(배양액 오염시키기)만 CreateLabARoomMissions에서 배치한다.
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
                .Configure(
                    allClueMarkers,
                    EnsureUpgradeBalanceConfig());

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

            var authorityObject =
                new GameObject("[Network] VillainMissionStackAuthority");
            authorityObject.transform.SetParent(parent);
            authorityObject.AddComponent<NetworkObject>();
            authorityObject.AddComponent<NetworkVillainMissionStackAuthority>()
                .Configure(monsterTierRuntime, spawner);
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

            // 측면 프로필 원숭이는 몸통 회전이 없으므로 이동 방향으로 뒤집는다.
            // 이마 RX-9 표식은 가로 중앙에 있어 플립해도 어긋나지 않는다.
            var monsterRenderer = visual.GetComponent<SpriteRenderer>();
            monster.AddComponent<CharacterWalkAnimator>().Configure(
                monster.transform,
                monsterRenderer,
                monsterRenderer.sprite,
                LoadWalkCycle(
                    MonsterWalkContactASpritePath,
                    MonsterWalkPassSpritePath,
                    MonsterWalkContactBSpritePath),
                shouldControlFacing: true);

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
            var motor = player.GetComponent<PlayerMotor>();
            antidoteService.Configure(
                config,
                infectionService,
                player.GetComponent<PlayerInputReader>(),
                motor);

            // 로컬 씬에도 유령 전환을 붙인다. 이게 없으면 감염 타이머가 0에 닿아
            // DeadGhost가 되어도 화면에서는 아무 일도 일어나지 않아 "죽지 않는다"로 보인다
            // (온라인 경로는 ProjectBootstrap이 같은 컴포넌트를 붙인다).
            var ghostMovement = player.AddComponent<GhostMovementController>();
            ghostMovement.Configure(
                infectionService,
                player.GetComponent<Rigidbody2D>(),
                player.GetComponent<Collider2D>(),
                AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                    MovementConfigPath),
                ProjectBootstrap.LaboratoryMapBounds);
            motor.SetGhostMovement(ghostMovement);
            motor.SetInfectionService(infectionService);
            var hudObject = new GameObject("[UI] InfectionHud");
            hudObject.transform.SetParent(parent);
            hudObject.AddComponent<InfectionHudView>()
                .Configure(infectionService, antidoteService);
        }

        /// <summary>
        /// 백신실 A/B가 각각 중앙 제어 PC 1대와 제작대 1대를 갖는지 밸런스 표(§8)
        /// 기준으로 확인한다. 개인 레시피 후보는 더 이상 존재하지 않는다(GDD §14.2).
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

            var terminals =
                UnityEngine.Object.FindObjectsByType<
                    AntidoteTerminalPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (terminals.Length != antidoteConfig.FabricatorCount ||
                Array.Exists(
                    terminals,
                    item =>
                        item.Config == null ||
                        item.GetComponent<Collider2D>() == null ||
                        item.GetComponent<
                            NetworkAntidoteTerminalAuthority>() == null) ||
                !Array.Exists(terminals, item => item.RoomId == "VaccineA") ||
                !Array.Exists(terminals, item => item.RoomId == "VaccineB"))
            {
                failures.Add(
                    "The vaccine room terminals are incomplete. " +
                    "Both vaccine rooms need one networked terminal.");
            }

            var localEconomy = GameObject.Find("P_Player_Local")?
                .GetComponent<LocalAntidoteEconomyPrototype>();
            if (localEconomy == null ||
                localEconomy.TerminalCount != terminals.Length ||
                localEconomy.FabricatorCount != fabricators.Length)
            {
                failures.Add(
                    "The local antidote economy prototype is not fully connected.");
            }
        }

        /// <summary>
        /// 백신실 A의 생존자 미션 2종이 배치·연결됐는지 확인한다(GDD §10.2).
        /// </summary>
        private static void ValidateVaccineARoomMissions(List<string> failures)
        {
            var download = GameObject.Find("VaccineDataDownload")?
                .GetComponent<VaccineDataDownloadStation>();
            if (download == null ||
                download.GetComponent<NetworkVaccineDataDownloadAuthority>() ==
                    null ||
                download.RoomId != "VaccineA")
            {
                failures.Add(
                    "The vaccine data download mission is incomplete.");
            }

            var syringes = GameObject.Find("ContaminatedSyringes")?
                .GetComponent<ContaminatedSyringeStation>();
            if (syringes == null ||
                syringes.GetComponent<NetworkContaminatedSyringeAuthority>() ==
                    null ||
                syringes.RoomId != "VaccineA")
            {
                failures.Add(
                    "The contaminated syringe mission is incomplete.");
            }

            if (GameObject.Find("[UI] VaccineDataDownload")?
                    .GetComponent<VaccineDataDownloadView>() == null ||
                GameObject.Find("[UI] ContaminatedSyringe")?
                    .GetComponent<ContaminatedSyringeView>() == null)
            {
                failures.Add(
                    "The vaccine room A mission views are missing.");
            }
        }

        private static InteractionBalanceConfig EnsureInteractionBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<InteractionBalanceConfig>(
                    InteractionBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<InteractionBalanceConfig>();
            config.name = "SO_InteractionBalance_Default";
            AssetDatabase.CreateAsset(config, InteractionBalanceConfigPath);
            return config;
        }

        /// <summary>
        /// 실험실 A의 생존자 미션 2종과 빌런 위장 미션 1종이 배치·연결됐는지
        /// 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidateLabARoomMissions(List<string> failures)
        {
            var slideGlass = GameObject.Find("SlideGlassCleaning")?
                .GetComponent<SlideGlassStation>();
            if (slideGlass == null ||
                slideGlass.GetComponent<NetworkSlideGlassAuthority>() == null ||
                slideGlass.RoomId != "LabA")
            {
                failures.Add("The slide glass cleaning mission is incomplete.");
            }

            var reagent = GameObject.Find("ReagentSorting")?
                .GetComponent<ReagentSortingStation>();
            if (reagent == null ||
                reagent.GetComponent<NetworkReagentSortingAuthority>() == null ||
                reagent.RoomId != "LabA")
            {
                failures.Add("The reagent sorting mission is incomplete.");
            }

            var villain = GameObject
                .Find("MissionVariant_LabA_CultureContamination")?
                .GetComponent<VillainHoldButtonStation>();
            if (villain == null ||
                villain.GetComponent<NetworkVillainHoldButtonAuthority>() ==
                    null ||
                villain.Kind != VillainMissionKind.CultureContamination ||
                villain.RoomId != "LabA")
            {
                failures.Add(
                    "The culture contamination villain mission is incomplete.");
            }

            if (GameObject.Find("[UI] SlideGlassCleaning")?
                    .GetComponent<SlideGlassView>() == null ||
                GameObject.Find("[UI] ReagentSorting")?
                    .GetComponent<ReagentSortingView>() == null ||
                GameObject.Find("[UI] VillainHoldButton_LabA")?
                    .GetComponent<VillainHoldButtonView>() == null)
            {
                failures.Add("The lab room A mission views are missing.");
            }
        }

        /// <summary>
        /// 격리실 A의 생존자 미션 3종이 배치·연결됐는지 확인한다(GDD §10.2).
        /// </summary>
        private static void ValidateQuarantineARoomMissions(
            List<string> failures)
        {
            var wire = GameObject.Find("WireConnect_QuarantineA")?
                .GetComponent<WireConnectStation>();
            if (wire == null ||
                wire.GetComponent<NetworkWireConnectAuthority>() == null ||
                wire.RoomId != "QuarantineA")
            {
                failures.Add(
                    "The quarantine A wire connect mission is incomplete.");
            }

            var dial = GameObject.Find("AirlockDial")?
                .GetComponent<AirlockDialStation>();
            if (dial == null ||
                dial.GetComponent<NetworkAirlockDialAuthority>() == null ||
                dial.RoomId != "QuarantineA")
            {
                failures.Add("The airlock dial mission is incomplete.");
            }

            var hazmat = GameObject.Find("HazmatDecontamination")?
                .GetComponent<HazmatDecontaminationStation>();
            if (hazmat == null ||
                hazmat.GetComponent<
                    NetworkHazmatDecontaminationAuthority>() == null ||
                hazmat.RoomId != "QuarantineA")
            {
                failures.Add(
                    "The hazmat decontamination mission is incomplete.");
            }

            if (GameObject.Find("[UI] WireConnect_QuarantineA")?
                    .GetComponent<WireConnectView>() == null ||
                GameObject.Find("[UI] AirlockDial")?
                    .GetComponent<AirlockDialView>() == null ||
                GameObject.Find("[UI] HazmatDecontamination")?
                    .GetComponent<HazmatDecontaminationView>() == null)
            {
                failures.Add("The quarantine room A mission views are missing.");
            }
        }

        /// <summary>
        /// 격리실 B의 생존자 미션 2종과 빌런 위장 미션 1종이 배치·연결됐는지
        /// 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidateQuarantineBRoomMissions(
            List<string> failures)
        {
            var wire = GameObject.Find("WireConnect_QuarantineB")?
                .GetComponent<WireConnectStation>();
            if (wire == null ||
                wire.GetComponent<NetworkWireConnectAuthority>() == null ||
                wire.RoomId != "QuarantineB")
            {
                failures.Add(
                    "The quarantine B wire connect mission is incomplete.");
            }

            var filter = GameObject.Find("SwapFilter")?
                .GetComponent<SwapFilterStation>();
            if (filter == null ||
                filter.GetComponent<NetworkSwapFilterAuthority>() == null ||
                filter.RoomId != "QuarantineB")
            {
                failures.Add("The swap filter mission is incomplete.");
            }

            var villain = GameObject
                .Find("MissionVariant_QuarantineB_VentBackflow")?
                .GetComponent<VillainHoldButtonStation>();
            if (villain == null ||
                villain.GetComponent<NetworkVillainHoldButtonAuthority>() ==
                    null ||
                villain.Kind != VillainMissionKind.VentBackflow ||
                villain.RoomId != "QuarantineB")
            {
                failures.Add(
                    "The vent backflow villain mission is incomplete.");
            }

            if (GameObject.Find("[UI] WireConnect_QuarantineB")?
                    .GetComponent<WireConnectView>() == null ||
                GameObject.Find("[UI] SwapFilter")?
                    .GetComponent<SwapFilterView>() == null ||
                GameObject.Find("[UI] VillainHoldButton_QuarantineB")?
                    .GetComponent<VillainHoldButtonView>() == null)
            {
                failures.Add("The quarantine room B mission views are missing.");
            }
        }

        /// <summary>
        /// 입원실의 생존자 미션 2종과 빌런 위장 미션 1종이 배치·연결됐는지
        /// 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidateWardRoomMissions(List<string> failures)
        {
            var drip = GameObject.Find("IvDrip")?
                .GetComponent<IvDripStation>();
            if (drip == null ||
                drip.GetComponent<NetworkIvDripAuthority>() == null ||
                drip.RoomId != "Ward")
            {
                failures.Add("The IV drip mission is incomplete.");
            }

            var vitals = GameObject.Find("PatientVitals")?
                .GetComponent<PatientVitalsStation>();
            if (vitals == null ||
                vitals.GetComponent<NetworkPatientVitalsAuthority>() == null ||
                vitals.RoomId != "Ward")
            {
                failures.Add("The patient vitals mission is incomplete.");
            }

            var villain = GameObject
                .Find("MissionVariant_Ward_MedicationRecordWipe")?
                .GetComponent<VillainDragItemsStation>();
            if (villain == null ||
                villain.GetComponent<NetworkVillainDragItemsAuthority>() ==
                    null ||
                villain.Kind != VillainMissionKind.MedicationRecordWipe ||
                villain.RoomId != "Ward")
            {
                failures.Add(
                    "The medication record wipe villain mission is incomplete.");
            }

            if (GameObject.Find("[UI] IvDrip")?
                    .GetComponent<IvDripView>() == null ||
                GameObject.Find("[UI] PatientVitals")?
                    .GetComponent<PatientVitalsView>() == null ||
                GameObject.Find("[UI] VillainDragItems_Ward")?
                    .GetComponent<VillainDragItemsView>() == null)
            {
                failures.Add("The ward room mission views are missing.");
            }
        }

        /// <summary>
        /// 액체 보관실의 생존자 미션 2종과 빌런 위장 조작(같은 밸브)이
        /// 배치·연결됐는지 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidateStorageRoomMissions(List<string> failures)
        {
            var valve = GameObject.Find("RotateValve")?
                .GetComponent<RotateValveStation>();
            if (valve == null ||
                valve.GetComponent<NetworkRotateValveAuthority>() == null ||
                valve.RoomId != "Storage")
            {
                failures.Add("The rotate valve mission is incomplete.");
            }

            var compactor = GameObject.Find("WasteCompactor")?
                .GetComponent<WasteCompactorStation>();
            if (compactor == null ||
                compactor.GetComponent<NetworkWasteCompactorAuthority>() ==
                    null ||
                compactor.RoomId != "Storage")
            {
                failures.Add("The waste compactor mission is incomplete.");
            }

            if (GameObject.Find("[UI] RotateValve")?
                    .GetComponent<RotateValveView>() == null ||
                GameObject.Find("[UI] WasteCompactor")?
                    .GetComponent<WasteCompactorView>() == null)
            {
                failures.Add("The storage room mission views are missing.");
            }
        }

        /// <summary>
        /// 중앙 보안 광장의 생존자 미션 2종과 빌런 위장 미션 1종이 배치·연결됐는지
        /// 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidateSecurityRoomMissions(
            List<string> failures)
        {
            var card = GameObject.Find("IdCardSwipe")?
                .GetComponent<IdCardSwipeStation>();
            if (card == null ||
                card.GetComponent<NetworkIdCardSwipeAuthority>() == null ||
                card.RoomId != "Security")
            {
                failures.Add("The ID card swipe mission is incomplete.");
            }

            var cctv = GameObject.Find("CctvScreenCleaning")?
                .GetComponent<CctvScreenCleaningStation>();
            if (cctv == null ||
                cctv.GetComponent<NetworkCctvScreenCleaningAuthority>() ==
                    null ||
                cctv.RoomId != "Security")
            {
                failures.Add("The CCTV screen cleaning mission is incomplete.");
            }

            var villain = GameObject
                .Find("MissionVariant_Security_WireTangle")?
                .GetComponent<TangleWiresStation>();
            if (villain == null ||
                villain.GetComponent<NetworkTangleWiresAuthority>() == null ||
                villain.RoomId != "Security")
            {
                failures.Add(
                    "The security wire tangle villain mission is incomplete.");
            }

            if (GameObject.Find("[UI] IdCardSwipe")?
                    .GetComponent<IdCardSwipeView>() == null ||
                GameObject.Find("[UI] CctvScreenCleaning")?
                    .GetComponent<CctvScreenCleaningView>() == null ||
                GameObject.Find("[UI] TangleWires_Security")?
                    .GetComponent<TangleWiresView>() == null)
            {
                failures.Add("The security room mission views are missing.");
            }
        }

        /// <summary>
        /// 전력 복구실의 생존자 미션 2종과 빌런 위장 미션 1종이 배치·연결됐는지
        /// 확인한다(GDD §10.2, §13.2).
        /// </summary>
        private static void ValidatePowerRoomMissions(List<string> failures)
        {
            var breaker = GameObject.Find("CircuitBreaker")?
                .GetComponent<CircuitBreakerStation>();
            if (breaker == null ||
                breaker.GetComponent<NetworkCircuitBreakerAuthority>() ==
                    null ||
                breaker.RoomId != "Power")
            {
                failures.Add("The circuit breaker mission is incomplete.");
            }

            var fuse = GameObject.Find("FuseSwap")?
                .GetComponent<FuseSwapStation>();
            if (fuse == null ||
                fuse.GetComponent<NetworkFuseSwapAuthority>() == null ||
                fuse.RoomId != "Power")
            {
                failures.Add("The fuse swap mission is incomplete.");
            }

            var villain = GameObject.Find("MissionVariant_Power_LineCut")?
                .GetComponent<PowerLineCutStation>();
            if (villain == null ||
                villain.GetComponent<NetworkPowerLineCutAuthority>() ==
                    null ||
                villain.Kind != VillainMissionKind.MainPowerLineCut ||
                villain.RoomId != "Power")
            {
                failures.Add(
                    "The main power line cut villain mission is incomplete.");
            }

            if (GameObject.Find("[UI] CircuitBreaker")?
                    .GetComponent<CircuitBreakerView>() == null ||
                GameObject.Find("[UI] FuseSwap")?
                    .GetComponent<FuseSwapView>() == null ||
                GameObject.Find("[UI] PowerLineCut")?
                    .GetComponent<PowerLineCutView>() == null)
            {
                failures.Add("The power room mission views are missing.");
            }
        }

        /// <summary>
        /// 실험실 B의 생존자 미션 3종이 배치·연결됐는지 확인한다(GDD §10.2).
        /// </summary>
        private static void ValidateLabBRoomMissions(List<string> failures)
        {
            var microscope = GameObject.Find("MicroscopeFocus")?
                .GetComponent<MicroscopeFocusStation>();
            if (microscope == null ||
                microscope.GetComponent<NetworkMicroscopeFocusAuthority>() ==
                    null ||
                microscope.RoomId != "LabB")
            {
                failures.Add("The microscope focus mission is incomplete.");
            }

            var flask = GameObject.Find("FlaskFill")?
                .GetComponent<FlaskFillStation>();
            if (flask == null ||
                flask.GetComponent<NetworkFlaskFillAuthority>() == null ||
                flask.RoomId != "LabB")
            {
                failures.Add("The flask fill mission is incomplete.");
            }

            var cage = GameObject.Find("RatCageLock")?
                .GetComponent<RatCageLockStation>();
            if (cage == null ||
                cage.GetComponent<NetworkRatCageLockAuthority>() == null ||
                cage.RoomId != "LabB")
            {
                failures.Add("The rat cage lock mission is incomplete.");
            }

            if (GameObject.Find("[UI] MicroscopeFocus")?
                    .GetComponent<MicroscopeFocusView>() == null ||
                GameObject.Find("[UI] FlaskFill")?
                    .GetComponent<FlaskFillView>() == null ||
                GameObject.Find("[UI] RatCageLock")?
                    .GetComponent<RatCageLockView>() == null)
            {
                failures.Add("The lab room B mission views are missing.");
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

        private static SurvivorMissionBalanceConfig
            EnsureSurvivorMissionBalanceConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<SurvivorMissionBalanceConfig>(
                    SurvivorMissionBalanceConfigPath);
            if (config != null)
            {
                return config;
            }

            config =
                ScriptableObject.CreateInstance<SurvivorMissionBalanceConfig>();
            config.name = "SO_SurvivorMissionBalance_Default";
            AssetDatabase.CreateAsset(config, SurvivorMissionBalanceConfigPath);
            return config;
        }

        /// <summary>
        /// 백신실 A의 백신 데이터 다운로드·오염된 주사기 폐기 미션을 배치한다(GDD §10.2).
        /// </summary>
        private static void CreateVaccineARoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] VaccineARoomMissions").transform;
            missionRoot.SetParent(parent);

            var downloadInstance = CreateSpriteObject(
                "VaccineDataDownload",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(3f, -3f),
                new Vector2(1.8f, 1.4f),
                new Color(0.3f, 0.5f, 0.7f, 1f),
                30,
                missionRoot);
            var downloadCollider =
                downloadInstance.AddComponent<BoxCollider2D>();
            downloadCollider.isTrigger = true;
            downloadCollider.size = Vector2.one;
            var downloadStation =
                downloadInstance.AddComponent<VaccineDataDownloadStation>();
            downloadStation.Configure(
                downloadInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "VaccineA");
            downloadInstance.AddComponent<NetworkObject>();
            downloadInstance
                .AddComponent<NetworkVaccineDataDownloadAuthority>()
                .Configure(downloadStation, missionConfig, interactionConfig);
            var downloadView = new GameObject("[UI] VaccineDataDownload")
                .AddComponent<VaccineDataDownloadView>();
            downloadView.transform.SetParent(missionRoot);
            downloadView.Configure(downloadStation, missionConfig, localPlayer);

            var syringeInstance = CreateSpriteObject(
                "ContaminatedSyringes",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-3f, -3f),
                new Vector2(1.8f, 1.4f),
                new Color(0.6f, 0.2f, 0.2f, 1f),
                30,
                missionRoot);
            var syringeCollider =
                syringeInstance.AddComponent<BoxCollider2D>();
            syringeCollider.isTrigger = true;
            syringeCollider.size = Vector2.one;
            var syringeStation =
                syringeInstance.AddComponent<ContaminatedSyringeStation>();
            syringeStation.Configure(
                syringeInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "VaccineA");
            syringeInstance.AddComponent<NetworkObject>();
            syringeInstance
                .AddComponent<NetworkContaminatedSyringeAuthority>()
                .Configure(syringeStation, missionConfig, interactionConfig);
            var syringeView = new GameObject("[UI] ContaminatedSyringe")
                .AddComponent<ContaminatedSyringeView>();
            syringeView.transform.SetParent(missionRoot);
            syringeView.Configure(syringeStation, missionConfig, localPlayer);
        }

        /// <summary>
        /// 실험실 A의 슬라이드 글라스 닦기·시약병 분류(생존자)와 배양액 오염시키기
        /// (빌런 위장 미션)를 배치한다(GDD §10.2, §13.2).
        /// </summary>
        private static void CreateLabARoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] LabARoomMissions").transform;
            missionRoot.SetParent(parent);

            var slideGlassInstance = CreateSpriteObject(
                "SlideGlassCleaning",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, -3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.7f, 0.75f, 0.8f, 1f),
                30,
                missionRoot);
            var slideGlassCollider =
                slideGlassInstance.AddComponent<BoxCollider2D>();
            slideGlassCollider.isTrigger = true;
            slideGlassCollider.size = Vector2.one;
            var slideGlassStation =
                slideGlassInstance.AddComponent<SlideGlassStation>();
            slideGlassStation.Configure(
                slideGlassInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "LabA");
            slideGlassInstance.AddComponent<NetworkObject>();
            slideGlassInstance.AddComponent<NetworkSlideGlassAuthority>()
                .Configure(slideGlassStation, missionConfig, interactionConfig);
            var slideGlassView = new GameObject("[UI] SlideGlassCleaning")
                .AddComponent<SlideGlassView>();
            slideGlassView.transform.SetParent(missionRoot);
            slideGlassView.Configure(
                slideGlassStation,
                missionConfig,
                localPlayer);

            var reagentInstance = CreateSpriteObject(
                "ReagentSorting",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, -3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.5f, 0.4f, 0.6f, 1f),
                30,
                missionRoot);
            var reagentCollider =
                reagentInstance.AddComponent<BoxCollider2D>();
            reagentCollider.isTrigger = true;
            reagentCollider.size = Vector2.one;
            var reagentStation =
                reagentInstance.AddComponent<ReagentSortingStation>();
            reagentStation.Configure(
                reagentInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "LabA");
            reagentInstance.AddComponent<NetworkObject>();
            reagentInstance.AddComponent<NetworkReagentSortingAuthority>()
                .Configure(reagentStation, interactionConfig);
            var reagentView = new GameObject("[UI] ReagentSorting")
                .AddComponent<ReagentSortingView>();
            reagentView.transform.SetParent(missionRoot);
            reagentView.Configure(reagentStation, missionConfig, localPlayer);

            // 빌런 위장 미션은 생존자 미션과 같은 좌표·외형을 공유하지 않고
            // 별도 오브젝트로 두되(GDD §13.2 "같은 자리"는 화면상 근접 배치를 뜻함),
            // 실제 위장은 상호작용 시 서버가 역할별로 다른 오브젝트를 노출하는 대신
            // 여기서는 같은 방 안 별도 지점에 둔다.
            var villainInstance = CreateSpriteObject(
                "MissionVariant_LabA_CultureContamination",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(0f, -3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.3f, 0.5f, 0.7f, 1f),
                30,
                missionRoot);
            var villainCollider =
                villainInstance.AddComponent<BoxCollider2D>();
            villainCollider.isTrigger = true;
            villainCollider.size = Vector2.one;
            var villainStation =
                villainInstance.AddComponent<VillainHoldButtonStation>();
            villainStation.Configure(
                villainInstance.GetComponent<SpriteRenderer>(),
                8f,
                VillainMissionKind.CultureContamination,
                "LabA");
            villainInstance.AddComponent<NetworkObject>();
            villainInstance.AddComponent<NetworkVillainHoldButtonAuthority>()
                .Configure(villainStation, interactionConfig, ClueKind.VentRedSmoke);
            var villainView = new GameObject("[UI] VillainHoldButton_LabA")
                .AddComponent<VillainHoldButtonView>();
            villainView.transform.SetParent(missionRoot);
            villainView.Configure(villainStation, localPlayer);
        }

        /// <summary>
        /// 격리실 A의 배선 복구·에어록 압력 조절·방호복 소독 미션을 배치한다
        /// (GDD §10.2).
        /// </summary>
        private static void CreateQuarantineARoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] QuarantineARoomMissions").transform;
            missionRoot.SetParent(parent);

            var wireInstance = CreateSpriteObject(
                "WireConnect_QuarantineA",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, 3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.3f, 0.3f, 0.35f, 1f),
                30,
                missionRoot);
            var wireCollider = wireInstance.AddComponent<BoxCollider2D>();
            wireCollider.isTrigger = true;
            wireCollider.size = Vector2.one;
            var wireStation = wireInstance.AddComponent<WireConnectStation>();
            wireStation.Configure(
                wireInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "QuarantineA");
            wireInstance.AddComponent<NetworkObject>();
            wireInstance.AddComponent<NetworkWireConnectAuthority>()
                .Configure(wireStation, interactionConfig);
            var wireView = new GameObject("[UI] WireConnect_QuarantineA")
                .AddComponent<WireConnectView>();
            wireView.transform.SetParent(missionRoot);
            wireView.Configure(wireStation, localPlayer);

            var dialInstance = CreateSpriteObject(
                "AirlockDial",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, 3.5f),
                new Vector2(1.6f, 1.4f),
                new Color(0.4f, 0.5f, 0.55f, 1f),
                30,
                missionRoot);
            var dialCollider = dialInstance.AddComponent<BoxCollider2D>();
            dialCollider.isTrigger = true;
            dialCollider.size = Vector2.one;
            var dialStation = dialInstance.AddComponent<AirlockDialStation>();
            dialStation.Configure(
                dialInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "QuarantineA");
            dialInstance.AddComponent<NetworkObject>();
            dialInstance.AddComponent<NetworkAirlockDialAuthority>()
                .Configure(dialStation, missionConfig, interactionConfig);
            var dialView = new GameObject("[UI] AirlockDial")
                .AddComponent<AirlockDialView>();
            dialView.transform.SetParent(missionRoot);
            dialView.Configure(dialStation, localPlayer);

            var hazmatInstance = CreateSpriteObject(
                "HazmatDecontamination",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(0f, -3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.5f, 0.55f, 0.6f, 1f),
                30,
                missionRoot);
            var hazmatCollider =
                hazmatInstance.AddComponent<BoxCollider2D>();
            hazmatCollider.isTrigger = true;
            hazmatCollider.size = Vector2.one;
            var hazmatStation =
                hazmatInstance.AddComponent<HazmatDecontaminationStation>();
            hazmatStation.Configure(
                hazmatInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "QuarantineA");
            hazmatInstance.AddComponent<NetworkObject>();
            hazmatInstance
                .AddComponent<NetworkHazmatDecontaminationAuthority>()
                .Configure(hazmatStation, missionConfig, interactionConfig);
            var hazmatView = new GameObject("[UI] HazmatDecontamination")
                .AddComponent<HazmatDecontaminationView>();
            hazmatView.transform.SetParent(missionRoot);
            hazmatView.Configure(hazmatStation, localPlayer);
        }

        /// <summary>
        /// 격리실 B의 배선 복구(격리실 A와 동일 조작)·공기 필터 교체(생존자)와
        /// 환풍구 역류 조작(빌런 위장 미션)을 배치한다(GDD §10.2, §13.2).
        /// </summary>
        private static void CreateQuarantineBRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] QuarantineBRoomMissions").transform;
            missionRoot.SetParent(parent);

            var wireInstance = CreateSpriteObject(
                "WireConnect_QuarantineB",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, 3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.3f, 0.3f, 0.35f, 1f),
                30,
                missionRoot);
            var wireCollider = wireInstance.AddComponent<BoxCollider2D>();
            wireCollider.isTrigger = true;
            wireCollider.size = Vector2.one;
            var wireStation = wireInstance.AddComponent<WireConnectStation>();
            wireStation.Configure(
                wireInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "QuarantineB");
            wireInstance.AddComponent<NetworkObject>();
            wireInstance.AddComponent<NetworkWireConnectAuthority>()
                .Configure(wireStation, interactionConfig);
            var wireView = new GameObject("[UI] WireConnect_QuarantineB")
                .AddComponent<WireConnectView>();
            wireView.transform.SetParent(missionRoot);
            wireView.Configure(wireStation, localPlayer);

            var filterInstance = CreateSpriteObject(
                "SwapFilter",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, 3.5f),
                new Vector2(1.6f, 1.4f),
                new Color(0.3f, 0.3f, 0.32f, 1f),
                30,
                missionRoot);
            var filterCollider = filterInstance.AddComponent<BoxCollider2D>();
            filterCollider.isTrigger = true;
            filterCollider.size = Vector2.one;
            var filterStation =
                filterInstance.AddComponent<SwapFilterStation>();
            filterStation.Configure(
                filterInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "QuarantineB");
            filterInstance.AddComponent<NetworkObject>();
            filterInstance.AddComponent<NetworkSwapFilterAuthority>()
                .Configure(filterStation, interactionConfig);
            var filterView = new GameObject("[UI] SwapFilter")
                .AddComponent<SwapFilterView>();
            filterView.transform.SetParent(missionRoot);
            filterView.Configure(filterStation, localPlayer);

            // 빌런 위장 미션은 방호복 소독과 같은 위치·정지 자세 느낌을 준다
            // (GDD §13.2). 별도 오브젝트로 두되 같은 방 안 다른 지점에 배치한다.
            var villainInstance = CreateSpriteObject(
                "MissionVariant_QuarantineB_VentBackflow",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(0f, -3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.5f, 0.55f, 0.6f, 1f),
                30,
                missionRoot);
            var villainCollider =
                villainInstance.AddComponent<BoxCollider2D>();
            villainCollider.isTrigger = true;
            villainCollider.size = Vector2.one;
            var villainStation =
                villainInstance.AddComponent<VillainHoldButtonStation>();
            villainStation.Configure(
                villainInstance.GetComponent<SpriteRenderer>(),
                8f,
                VillainMissionKind.VentBackflow,
                "QuarantineB");
            villainInstance.AddComponent<NetworkObject>();
            villainInstance.AddComponent<NetworkVillainHoldButtonAuthority>()
                .Configure(
                    villainStation,
                    interactionConfig,
                    ClueKind.BrokenQuarantineLock);
            var villainView =
                new GameObject("[UI] VillainHoldButton_QuarantineB")
                    .AddComponent<VillainHoldButtonView>();
            villainView.transform.SetParent(missionRoot);
            villainView.Configure(villainStation, localPlayer);
        }

        /// <summary>
        /// 입원실의 수액 속도 조절·환자 바이탈 기록(생존자)과 투약 기록 삭제
        /// (빌런 위장 미션)를 배치한다(GDD §10.2, §13.2).
        /// </summary>
        private static void CreateWardRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] WardRoomMissions").transform;
            missionRoot.SetParent(parent);

            var dripInstance = CreateSpriteObject(
                "IvDrip",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, 3f),
                new Vector2(1.4f, 1.8f),
                new Color(0.6f, 0.7f, 0.75f, 1f),
                30,
                missionRoot);
            var dripCollider = dripInstance.AddComponent<BoxCollider2D>();
            dripCollider.isTrigger = true;
            dripCollider.size = Vector2.one;
            var dripStation = dripInstance.AddComponent<IvDripStation>();
            dripStation.Configure(
                dripInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Ward");
            dripInstance.AddComponent<NetworkObject>();
            dripInstance.AddComponent<NetworkIvDripAuthority>()
                .Configure(dripStation, missionConfig, interactionConfig);
            var dripView = new GameObject("[UI] IvDrip")
                .AddComponent<IvDripView>();
            dripView.transform.SetParent(missionRoot);
            dripView.Configure(dripStation, missionConfig, localPlayer);

            var vitalsInstance = CreateSpriteObject(
                "PatientVitals",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, 3f),
                new Vector2(1.8f, 1.4f),
                new Color(0.45f, 0.55f, 0.6f, 1f),
                30,
                missionRoot);
            var vitalsCollider =
                vitalsInstance.AddComponent<BoxCollider2D>();
            vitalsCollider.isTrigger = true;
            vitalsCollider.size = Vector2.one;
            var vitalsStation =
                vitalsInstance.AddComponent<PatientVitalsStation>();
            vitalsStation.Configure(
                vitalsInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Ward");
            vitalsInstance.AddComponent<NetworkObject>();
            vitalsInstance.AddComponent<NetworkPatientVitalsAuthority>()
                .Configure(
                    vitalsStation,
                    missionConfig,
                    interactionConfig,
                    seed: 20260808);
            var vitalsView = new GameObject("[UI] PatientVitals")
                .AddComponent<PatientVitalsView>();
            vitalsView.transform.SetParent(missionRoot);
            vitalsView.Configure(vitalsStation, missionConfig, localPlayer);

            // 빌런 위장 미션은 환자 바이탈 기록과 같은 자리·느낌을 준다(GDD §13.2).
            var villainInstance = CreateSpriteObject(
                "MissionVariant_Ward_MedicationRecordWipe",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(0f, -3f),
                new Vector2(1.8f, 1.4f),
                new Color(0.4f, 0.4f, 0.55f, 1f),
                30,
                missionRoot);
            var villainCollider =
                villainInstance.AddComponent<BoxCollider2D>();
            villainCollider.isTrigger = true;
            villainCollider.size = Vector2.one;
            var villainStation =
                villainInstance.AddComponent<VillainDragItemsStation>();
            villainStation.Configure(
                villainInstance.GetComponent<SpriteRenderer>(),
                3,
                VillainMissionKind.MedicationRecordWipe,
                "Ward");
            villainInstance.AddComponent<NetworkObject>();
            villainInstance.AddComponent<NetworkVillainDragItemsAuthority>()
                .Configure(
                    villainStation,
                    interactionConfig,
                    ClueKind.ShreddedMedicationRecord);
            var villainView = new GameObject("[UI] VillainDragItems_Ward")
                .AddComponent<VillainDragItemsView>();
            villainView.transform.SetParent(missionRoot);
            villainView.Configure(villainStation, localPlayer);
        }

        /// <summary>
        /// 액체 보관실의 밸브 잠그기·폐기물 통 압축(생존자)과 밸브 압력 풀기
        /// (빌런)를 배치한다(GDD §10.2, §13.2). 밸브 잠그기와 풀기는 유일하게
        /// 같은 오브젝트를 반대 방향으로 조작하는 미션 쌍이다.
        /// </summary>
        private static void CreateStorageRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] StorageRoomMissions").transform;
            missionRoot.SetParent(parent);

            var valveInstance = CreateSpriteObject(
                "RotateValve",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(3f, 4f),
                new Vector2(1.4f, 1.4f),
                new Color(0.35f, 0.45f, 0.5f, 1f),
                30,
                missionRoot);
            var valveCollider = valveInstance.AddComponent<BoxCollider2D>();
            valveCollider.isTrigger = true;
            valveCollider.size = Vector2.one;
            var valveStation =
                valveInstance.AddComponent<RotateValveStation>();
            valveStation.Configure(
                valveInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Storage");
            valveInstance.AddComponent<NetworkObject>();
            valveInstance.AddComponent<NetworkRotateValveAuthority>()
                .Configure(
                    valveStation,
                    interactionConfig,
                    ClueKind.LeakedCoolant);
            var valveView = new GameObject("[UI] RotateValve")
                .AddComponent<RotateValveView>();
            valveView.transform.SetParent(missionRoot);
            valveView.Configure(valveStation, localPlayer);

            var compactorInstance = CreateSpriteObject(
                "WasteCompactor",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-3f, -4f),
                new Vector2(1.8f, 1.4f),
                new Color(0.4f, 0.35f, 0.3f, 1f),
                30,
                missionRoot);
            var compactorCollider =
                compactorInstance.AddComponent<BoxCollider2D>();
            compactorCollider.isTrigger = true;
            compactorCollider.size = Vector2.one;
            var compactorStation =
                compactorInstance.AddComponent<WasteCompactorStation>();
            compactorStation.Configure(
                compactorInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Storage");
            compactorInstance.AddComponent<NetworkObject>();
            compactorInstance.AddComponent<NetworkWasteCompactorAuthority>()
                .Configure(compactorStation, missionConfig, interactionConfig);
            var compactorView = new GameObject("[UI] WasteCompactor")
                .AddComponent<WasteCompactorView>();
            compactorView.transform.SetParent(missionRoot);
            compactorView.Configure(compactorStation, localPlayer);
        }

        /// <summary>
        /// 중앙 보안 광장의 ID 카드 긁기·CCTV 화면 닦기(생존자)와 보안 카메라
        /// 선 꼬기(빌런 위장 미션)를 배치한다(GDD §10.2, §13.2).
        /// </summary>
        private static void CreateSecurityRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] SecurityRoomMissions").transform;
            missionRoot.SetParent(parent);

            var cardInstance = CreateSpriteObject(
                "IdCardSwipe",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, 4f),
                new Vector2(1.6f, 1.4f),
                new Color(0.4f, 0.45f, 0.55f, 1f),
                30,
                missionRoot);
            var cardCollider = cardInstance.AddComponent<BoxCollider2D>();
            cardCollider.isTrigger = true;
            cardCollider.size = Vector2.one;
            var cardStation =
                cardInstance.AddComponent<IdCardSwipeStation>();
            cardStation.Configure(
                cardInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Security");
            cardInstance.AddComponent<NetworkObject>();
            cardInstance.AddComponent<NetworkIdCardSwipeAuthority>()
                .Configure(cardStation, interactionConfig);
            var cardView = new GameObject("[UI] IdCardSwipe")
                .AddComponent<IdCardSwipeView>();
            cardView.transform.SetParent(missionRoot);
            cardView.Configure(cardStation, localPlayer);

            var cctvInstance = CreateSpriteObject(
                "CctvScreenCleaning",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, -4f),
                new Vector2(1.8f, 1.4f),
                new Color(0.25f, 0.25f, 0.28f, 1f),
                30,
                missionRoot);
            var cctvCollider = cctvInstance.AddComponent<BoxCollider2D>();
            cctvCollider.isTrigger = true;
            cctvCollider.size = Vector2.one;
            var cctvStation =
                cctvInstance.AddComponent<CctvScreenCleaningStation>();
            cctvStation.Configure(
                cctvInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Security");
            cctvInstance.AddComponent<NetworkObject>();
            cctvInstance.AddComponent<NetworkCctvScreenCleaningAuthority>()
                .Configure(cctvStation, interactionConfig);
            var cctvView = new GameObject("[UI] CctvScreenCleaning")
                .AddComponent<CctvScreenCleaningView>();
            cctvView.transform.SetParent(missionRoot);
            cctvView.Configure(cctvStation, localPlayer);

            // 빌런 위장 미션은 CCTV 화면 닦기와 같은 자리·느낌을 준다(GDD §13.2).
            var villainInstance = CreateSpriteObject(
                "MissionVariant_Security_WireTangle",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, -4f),
                new Vector2(1.8f, 1.4f),
                new Color(0.25f, 0.25f, 0.28f, 1f),
                30,
                missionRoot);
            var villainCollider =
                villainInstance.AddComponent<BoxCollider2D>();
            villainCollider.isTrigger = true;
            villainCollider.size = Vector2.one;
            var villainStation =
                villainInstance.AddComponent<TangleWiresStation>();
            villainStation.Configure(
                villainInstance.GetComponent<SpriteRenderer>(),
                4,
                "Security");
            villainInstance.AddComponent<NetworkObject>();
            villainInstance.AddComponent<NetworkTangleWiresAuthority>()
                .Configure(
                    villainStation,
                    interactionConfig,
                    ClueKind.SeveredCameraFeed);
            var villainView = new GameObject("[UI] TangleWires_Security")
                .AddComponent<TangleWiresView>();
            villainView.transform.SetParent(missionRoot);
            villainView.Configure(villainStation, localPlayer);
        }

        /// <summary>
        /// 전력 복구실의 차단기 올리기·퓨즈 교체(생존자)와 메인 전력선 절단
        /// (빌런 위장 미션)를 배치한다(GDD §10.2, §13.2).
        /// </summary>
        private static void CreatePowerRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] PowerRoomMissions").transform;
            missionRoot.SetParent(parent);

            var breakerInstance = CreateSpriteObject(
                "CircuitBreaker",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(3f, 3.5f),
                new Vector2(1.8f, 1.4f),
                new Color(0.3f, 0.3f, 0.35f, 1f),
                30,
                missionRoot);
            var breakerCollider =
                breakerInstance.AddComponent<BoxCollider2D>();
            breakerCollider.isTrigger = true;
            breakerCollider.size = Vector2.one;
            var breakerStation =
                breakerInstance.AddComponent<CircuitBreakerStation>();
            breakerStation.Configure(
                breakerInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Power");
            breakerInstance.AddComponent<NetworkObject>();
            breakerInstance.AddComponent<NetworkCircuitBreakerAuthority>()
                .Configure(breakerStation, interactionConfig);
            var breakerView = new GameObject("[UI] CircuitBreaker")
                .AddComponent<CircuitBreakerView>();
            breakerView.transform.SetParent(missionRoot);
            breakerView.Configure(breakerStation, localPlayer);

            var fuseInstance = CreateSpriteObject(
                "FuseSwap",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-3f, -3.5f),
                new Vector2(1.6f, 1.4f),
                new Color(0.35f, 0.3f, 0.2f, 1f),
                30,
                missionRoot);
            var fuseCollider = fuseInstance.AddComponent<BoxCollider2D>();
            fuseCollider.isTrigger = true;
            fuseCollider.size = Vector2.one;
            var fuseStation = fuseInstance.AddComponent<FuseSwapStation>();
            fuseStation.Configure(
                fuseInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "Power");
            fuseInstance.AddComponent<NetworkObject>();
            fuseInstance.AddComponent<NetworkFuseSwapAuthority>()
                .Configure(fuseStation, interactionConfig);
            var fuseView = new GameObject("[UI] FuseSwap")
                .AddComponent<FuseSwapView>();
            fuseView.transform.SetParent(missionRoot);
            fuseView.Configure(fuseStation, localPlayer);

            // 빌런 위장 미션은 퓨즈 교체와 같은 자리·느낌을 준다(GDD §13.2).
            var villainInstance = CreateSpriteObject(
                "MissionVariant_Power_LineCut",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(3f, -3.5f),
                new Vector2(1.6f, 1.4f),
                new Color(0.35f, 0.3f, 0.2f, 1f),
                30,
                missionRoot);
            var villainCollider =
                villainInstance.AddComponent<BoxCollider2D>();
            villainCollider.isTrigger = true;
            villainCollider.size = Vector2.one;
            var villainStation =
                villainInstance.AddComponent<PowerLineCutStation>();
            villainStation.Configure(
                villainInstance.GetComponent<SpriteRenderer>(),
                3,
                "Power");
            villainInstance.AddComponent<NetworkObject>();
            villainInstance.AddComponent<NetworkPowerLineCutAuthority>()
                .Configure(
                    villainStation,
                    interactionConfig,
                    ClueKind.CutPowerLine);
            var villainView = new GameObject("[UI] PowerLineCut")
                .AddComponent<PowerLineCutView>();
            villainView.transform.SetParent(missionRoot);
            villainView.Configure(villainStation, localPlayer);
        }

        /// <summary>
        /// 실험실 B의 현미경 렌즈 초점·플라스크 용액 채우기·실험용 쥐 케이지
        /// 잠그기 생존자 미션 3종을 배치한다(GDD §10.2). 이 방에는 빌런
        /// 위장 미션이 배정되지 않는다.
        /// </summary>
        private static void CreateLabBRoomMissions(
            Transform parent,
            RoomDefinition room,
            GameObject localPlayer)
        {
            var missionConfig = EnsureSurvivorMissionBalanceConfig();
            var interactionConfig = EnsureInteractionBalanceConfig();
            var missionRoot =
                new GameObject("[Gameplay] LabBRoomMissions").transform;
            missionRoot.SetParent(parent);

            var microscopeInstance = CreateSpriteObject(
                "MicroscopeFocus",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(4f, 4f),
                new Vector2(1.6f, 1.6f),
                new Color(0.3f, 0.4f, 0.45f, 1f),
                30,
                missionRoot);
            var microscopeCollider =
                microscopeInstance.AddComponent<BoxCollider2D>();
            microscopeCollider.isTrigger = true;
            microscopeCollider.size = Vector2.one;
            var microscopeStation =
                microscopeInstance.AddComponent<MicroscopeFocusStation>();
            microscopeStation.Configure(
                microscopeInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "LabB");
            microscopeInstance.AddComponent<NetworkObject>();
            microscopeInstance
                .AddComponent<NetworkMicroscopeFocusAuthority>()
                .Configure(microscopeStation, missionConfig, interactionConfig);
            var microscopeView = new GameObject("[UI] MicroscopeFocus")
                .AddComponent<MicroscopeFocusView>();
            microscopeView.transform.SetParent(missionRoot);
            microscopeView.Configure(
                microscopeStation,
                missionConfig,
                localPlayer);

            var flaskInstance = CreateSpriteObject(
                "FlaskFill",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(-4f, 4f),
                new Vector2(1.4f, 1.6f),
                new Color(0.3f, 0.4f, 0.45f, 1f),
                30,
                missionRoot);
            var flaskCollider = flaskInstance.AddComponent<BoxCollider2D>();
            flaskCollider.isTrigger = true;
            flaskCollider.size = Vector2.one;
            var flaskStation = flaskInstance.AddComponent<FlaskFillStation>();
            flaskStation.Configure(
                flaskInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "LabB");
            flaskInstance.AddComponent<NetworkObject>();
            flaskInstance.AddComponent<NetworkFlaskFillAuthority>()
                .Configure(flaskStation, missionConfig, interactionConfig);
            var flaskView = new GameObject("[UI] FlaskFill")
                .AddComponent<FlaskFillView>();
            flaskView.transform.SetParent(missionRoot);
            flaskView.Configure(flaskStation, missionConfig, localPlayer);

            var cageInstance = CreateSpriteObject(
                "RatCageLock",
                LoadSprite(PanelSpritePath),
                room.Position + new Vector2(0f, -4f),
                new Vector2(1.8f, 1.4f),
                new Color(0.4f, 0.35f, 0.3f, 1f),
                30,
                missionRoot);
            var cageCollider = cageInstance.AddComponent<BoxCollider2D>();
            cageCollider.isTrigger = true;
            cageCollider.size = Vector2.one;
            var cageStation =
                cageInstance.AddComponent<RatCageLockStation>();
            cageStation.Configure(
                cageInstance.GetComponent<SpriteRenderer>(),
                missionConfig,
                "LabB");
            cageInstance.AddComponent<NetworkObject>();
            cageInstance.AddComponent<NetworkRatCageLockAuthority>()
                .Configure(cageStation, interactionConfig);
            var cageView = new GameObject("[UI] RatCageLock")
                .AddComponent<RatCageLockView>();
            cageView.transform.SetParent(missionRoot);
            cageView.Configure(cageStation, localPlayer);
        }

        /// <summary>
        /// 백신실 A/B에 중앙 제어 PC 1대와 제작대 1대를 각각 방 반대편에 배치한다
        /// (docs/map-level-design.md §4.1, §4.10, §7.2). 개인 레시피 탐색은 없다 —
        /// 배합 코드는 PC에서 즉시 발급받는다(GDD §14.2).
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

            var terminals = new[]
            {
                CreateTerminal(
                    economyRoot,
                    rooms["VaccineA"],
                    "AntidoteTerminal_A",
                    new Vector2(-3f, 3.5f),
                    "VaccineA",
                    "중앙 제어 PC A",
                    antidoteConfig,
                    interactionConfig),
                CreateTerminal(
                    economyRoot,
                    rooms["VaccineB"],
                    "AntidoteTerminal_B",
                    new Vector2(-3f, -3.5f),
                    "VaccineB",
                    "중앙 제어 PC B",
                    antidoteConfig,
                    interactionConfig)
            };

            var fabricators = new[]
            {
                CreateFabricator(
                    economyRoot,
                    rooms["VaccineA"],
                    "AntidoteFabricator_A",
                    new Vector2(3f, -3.5f),
                    "VaccineA",
                    "해독제 제작대 A",
                    antidoteConfig,
                    interactionConfig),
                CreateFabricator(
                    economyRoot,
                    rooms["VaccineB"],
                    "AntidoteFabricator_B",
                    new Vector2(3f, 3.5f),
                    "VaccineB",
                    "해독제 제작대 B",
                    antidoteConfig,
                    interactionConfig)
            };

            var antidoteService = localPlayer.GetComponent<AntidoteService>();
            localPlayer.AddComponent<LocalAntidoteEconomyPrototype>()
                .Configure(
                    antidoteService,
                    localPlayer.GetComponent<InfectionService>(),
                    terminals,
                    fabricators);

            for (var index = 0; index < terminals.Length; index++)
            {
                CreateAntidoteTerminalView(
                    economyRoot,
                    terminals[index],
                    antidoteService,
                    $"[UI] AntidoteTerminal_{index + 1:00}");
            }

            for (var index = 0; index < fabricators.Length; index++)
            {
                CreateAntidoteKeypadView(
                    economyRoot,
                    fabricators[index],
                    antidoteService,
                    $"[UI] AntidoteKeypad_{index + 1:00}");
            }
        }

        private static AntidoteTerminalPrototype CreateTerminal(
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
                new Vector2(1.6f, 1.4f),
                new Color(0.2f, 0.5f, 0.4f, 1f),
                30,
                parent);
            var collider = instance.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            var terminal = instance.AddComponent<AntidoteTerminalPrototype>();
            terminal.Configure(
                instance.GetComponent<SpriteRenderer>(),
                antidoteConfig,
                roomId,
                displayName);
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkAntidoteTerminalAuthority>()
                .Configure(terminal, antidoteConfig, interactionConfig);
            return terminal;
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

        private static void CreateAntidoteTerminalView(
            Transform parent,
            AntidoteTerminalPrototype terminal,
            AntidoteService antidoteService,
            string viewName)
        {
            var viewObject = new GameObject(viewName);
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<AntidoteTerminalView>()
                .Configure(terminal, antidoteService);
        }

        private static void CreateAntidoteKeypadView(
            Transform parent,
            AntidoteFabricatorPrototype fabricator,
            AntidoteService antidoteService,
            string viewName)
        {
            var viewObject = new GameObject(viewName);
            viewObject.transform.SetParent(parent);
            viewObject.AddComponent<AntidoteKeypadView>()
                .Configure(fabricator, antidoteService);
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
            var lightingConfig = EnsureWorldLightingBalanceConfig();
            var globalLightObject = new GameObject("Light_GlobalEmergency");
            globalLightObject.transform.SetParent(presentationRoot);
            var globalLight = globalLightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.color = lightingConfig.DarkGlobalTint;
            globalLight.intensity = lightingConfig.DarkGlobalIntensityRatio;
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
                    lightingConfig);
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
            var missionView = viewObject.AddComponent<FuseMissionView>();
            missionView.Configure(station);

            // 부품 그림이 없으면 미션이 전부 회색 상자로 그려진다(ui-ux-design.md §9).
            missionView.SetSpriteCatalog(MissionUiSpriteBuilder.LinkCatalog());
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

        /// <summary>
        /// 스프라이트를 늘리지 않고 <paramref name="size"/>만큼 반복해 깐다.
        /// 방 크기가 제각각이어도 바닥 이음새 간격이 일정하게 유지된다.
        /// </summary>
        private static GameObject CreateTiledSpriteObject(
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            return CreateResizableSpriteObject(
                name,
                sprite,
                position,
                size,
                color,
                sortingOrder,
                parent,
                SpriteDrawMode.Tiled);
        }

        /// <summary>
        /// 9-slice로 그려 테두리 두께를 고정한다. 길이가 크게 다른 벽과 프롭이
        /// 같은 외곽선 굵기를 갖게 하는 것이 목적이다.
        /// </summary>
        private static GameObject CreateSlicedSpriteObject(
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent)
        {
            return CreateResizableSpriteObject(
                name,
                sprite,
                position,
                size,
                color,
                sortingOrder,
                parent,
                SpriteDrawMode.Sliced);
        }

        private static GameObject CreateResizableSpriteObject(
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            Transform parent,
            SpriteDrawMode drawMode)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent);
            instance.transform.position = position;

            // Tiled·Sliced는 localScale이 아니라 SpriteRenderer.size로 크기를 정한다.
            // 스케일을 1로 두어야 타일 간격과 테두리가 늘어나지 않는다.
            instance.transform.localScale = Vector3.one;
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = drawMode;
            if (drawMode == SpriteDrawMode.Tiled)
            {
                renderer.tileMode = SpriteTileMode.Continuous;
            }

            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = GetWorldSpriteLitMaterial();
            return instance;
        }

        /// <summary>
        /// 설치물 뒤에 강조 테두리를 깐다. 회색상자만 놓여 있으면 이게 조작할 수 있는
        /// 대상인지 그냥 배경 장식인지 구분되지 않는다(아트 가이드 §1.2).
        /// </summary>
        private static void AttachInteractableHighlight(
            GameObject target,
            Vector2 baseSize,
            int sortingOrder)
        {
            var outline = CreateSpriteObject(
                target.name + "_Highlight",
                LoadSprite(PanelSpritePath),
                target.transform.position,
                baseSize,
                Color.clear,
                sortingOrder,
                target.transform.parent);
            outline.transform.position = target.transform.position;
            var renderer = outline.GetComponent<SpriteRenderer>();

            // 조명 영향을 받으면 어두운 방에서 강조가 보이지 않는다.
            renderer.sharedMaterial = GetIndicatorUnlitMaterial();
            target.AddComponent<InteractableHighlight>()
                .Configure(renderer, baseSize);
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
            EnsureFolder("Assets/_Project/Art/Sprites", "Environment");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImportedSprite(PlayerSpritePath, 1024f);
            ConfigureImportedSprite(MonsterSpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(PlayerWalkContactASpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(PlayerWalkPassSpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(PlayerWalkContactBSpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(MonsterWalkContactASpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(MonsterWalkPassSpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(MonsterWalkContactBSpritePath, 1024f);
            ConfigureImportedSpriteIfPresent(
                RoomFloorTileFinalSpritePath,
                256f,
                TextureWrapMode.Repeat,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CorridorFloorTileFinalSpritePath,
                256f,
                TextureWrapMode.Repeat,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                WallSectionFinalSpritePath,
                512f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect,
                CreateUniformBorder(64));
            ConfigureImportedSpriteIfPresent(
                WallFaceFinalSpritePath,
                512f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect,
                CreateUniformBorder(64));
            ConfigureImportedSpriteIfPresent(
                DoorPanelFinalSpritePath,
                512f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect,
                CreateUniformBorder(64));
            ConfigureImportedSpriteIfPresent(
                DoorFrameFinalSpritePath,
                512f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect,
                CreateUniformBorder(64));
            ConfigureImportedSpriteIfPresent(
                RoomSignFinalSpritePath,
                512f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect,
                CreateUniformBorder(64));
            ConfigureImportedSpriteIfPresent(
                FloorGuideFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CeilingLightFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                EmergencyBeaconFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                WallMonitorFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                FireExtinguisherFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                TrashBinFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                EmergencyPhoneFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                LabWorkbenchFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                StorageCabinetFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ReagentShelfFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                RollingCartFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CentrifugeFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                MicroscopeFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                PharmaFridgeFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BiosafetyHoodFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ServerRackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CctvMonitorWallFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ElectronicMapTableFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                OperatorConsoleFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                GeneratorFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BreakerBankFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CableReelFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BackupCellRackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                HospitalBedFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CurtainRailFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                IvStandFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                MedicalMonitorFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                MedicineCartFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                NurseStationFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                OxygenPortsFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                MedicineCabinetFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BloodStainAFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BloodStainBFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                TriageFloorNumbersFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                GlassCellWideFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                GlassCellFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CagePodFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                DeconUnitFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ContainmentLockFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ObservationConsoleFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                DeconShowerFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                RestraintRailFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                RestraintControllerFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                QuarantineWarningBeaconFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ContainmentFloorGridFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                BrokenGlassAFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ContainmentFloorNumbersFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CryoTankFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SampleDrumFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                FrozenPipeFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                TemperatureTerminalFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                CoolantManifoldFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ColdShelfFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                InsulatedPalletFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                FrostDrainFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                VialRackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SterileBenchFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                DeconSinkFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                PpeDispenserFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SterileFloorZoneFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                InjectorTesterFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                MixingBenchFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SampleRackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                VentOutletFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SpecimenScannerFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                EyeWashStationFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                OverheadServiceRailFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ChemicalSpillMarkFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                ServerBackupRackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SampleSealerFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                PackageScannerFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
            ConfigureImportedSpriteIfPresent(
                SealedCrateStackFinalSpritePath,
                256f,
                TextureWrapMode.Clamp,
                SpriteMeshType.FullRect);
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
            EnsureSprite(
                RoomFloorTileSpritePath,
                "S_FloorTile_Room",
                FloorTilePixels,
                FloorTilePixels,
                CreateRoomFloorPixel,
                new Vector2(0.5f, 0.5f),
                FloorTilePixelsPerUnit);
            EnsureSprite(
                CorridorFloorTileSpritePath,
                "S_FloorTile_Corridor",
                FloorTilePixels,
                FloorTilePixels,
                CreateCorridorFloorPixel,
                new Vector2(0.5f, 0.5f),
                FloorTilePixelsPerUnit);
            EnsureSprite(
                WallSectionSpritePath,
                "S_WallSection",
                32,
                32,
                CreateWallSectionPixel,
                new Vector2(0.5f, 0.5f),
                SlicedSpritePixelsPerUnit,
                CreateUniformBorder(SlicedSpriteBorderPixels));
            EnsureSprite(
                WallFaceSpritePath,
                "S_WallFace",
                32,
                32,
                CreateWallFacePixel,
                new Vector2(0.5f, 0.5f),
                SlicedSpritePixelsPerUnit,
                CreateUniformBorder(SlicedSpriteBorderPixels));
            EnsureSprite(
                PropBodySpritePath,
                "S_PropBody",
                48,
                48,
                CreatePropBodyPixel,
                new Vector2(0.5f, 0.5f),
                SlicedSpritePixelsPerUnit,
                CreateUniformBorder(SlicedSpriteBorderPixels));
            foreach (EnvironmentPropCategory category in
                     Enum.GetValues(typeof(EnvironmentPropCategory)))
            {
                var iconCategory = category;
                EnsureSprite(
                    GetPropIconSpritePath(iconCategory),
                    "S_PropIcon_" + iconCategory,
                    32,
                    32,
                    (x, y) => CreatePropIconPixel(iconCategory, x, y),
                    new Vector2(0.5f, 0.5f),
                    SlicedSpritePixelsPerUnit);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 아직 그림이 들어오지 않은 걷기 프레임 때문에 씬 생성이 멈추지 않게 한다.
        /// 정지 프레임은 필수라 <see cref="ConfigureImportedSprite"/>를 그대로 쓴다.
        /// </summary>
        private static void ConfigureImportedSpriteIfPresent(
            string path,
            float pixelsPerUnit,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp,
            SpriteMeshType spriteMeshType = SpriteMeshType.Tight,
            Vector4? spriteBorder = null)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                return;
            }

            ConfigureImportedSprite(
                path,
                pixelsPerUnit,
                wrapMode,
                spriteMeshType,
                spriteBorder);
        }

        /// <summary>
        /// 걷기 프레임을 접지A → 모음 → 접지B → 모음 순서로 만든다(아트 가이드 §2.2).
        /// 세 장 중 하나라도 없으면 빈 배열을 돌려주어 정지 프레임만 쓰는
        /// 현재 동작을 그대로 유지한다.
        /// </summary>
        private static Sprite[] LoadWalkCycle(
            string contactAPath,
            string passPath,
            string contactBPath)
        {
            var contactA = LoadSprite(contactAPath, throwIfMissing: false);
            var pass = LoadSprite(passPath, throwIfMissing: false);
            var contactB = LoadSprite(contactBPath, throwIfMissing: false);
            if (contactA == null || pass == null || contactB == null)
            {
                return Array.Empty<Sprite>();
            }

            return new[] { contactA, pass, contactB, pass };
        }

        private static void ConfigureImportedSprite(
            string path,
            float pixelsPerUnit,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp,
            SpriteMeshType spriteMeshType = SpriteMeshType.Tight,
            Vector4? spriteBorder = null)
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
            importer.spriteBorder = spriteBorder ?? Vector4.zero;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrapMode;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = spriteMeshType;
            importer.SetTextureSettings(textureSettings);
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

        /// <summary>
        /// 방 바닥 타일. 방 색을 곱해 쓰므로 회색조로 두고 밝기 대비만 만든다.
        /// 가장자리 패널 이음새와 1m 하위 격자, 모서리 리벳으로 거리감을 준다.
        /// </summary>
        private static Color32 CreateRoomFloorPixel(int x, int y)
        {
            const int size = FloorTilePixels;
            const int half = size / 2;
            var edge = Mathf.Min(
                Mathf.Min(x, size - 1 - x),
                Mathf.Min(y, size - 1 - y));
            if (edge <= 1)
            {
                return new Color32(118, 125, 133, 255);
            }

            if (edge <= 3)
            {
                return new Color32(184, 191, 199, 255);
            }

            foreach (var rivet in FloorTileRivetCenters)
            {
                if (IsInsideEllipse(x, y, rivet.x, rivet.y, 2.2f, 2.2f))
                {
                    return new Color32(142, 150, 158, 255);
                }
            }

            if (x % half <= 1 || y % half <= 1)
            {
                return new Color32(212, 219, 226, 255);
            }

            return new Color32(238, 242, 246, 255);
        }

        /// <summary>
        /// 복도 바닥 타일. 복도는 가로·세로 양방향으로 깔리므로 방향성이 없는
        /// 미끄럼 방지 다이아 트레드를 쓴다.
        /// </summary>
        private static Color32 CreateCorridorFloorPixel(int x, int y)
        {
            const int size = FloorTilePixels;
            var edge = Mathf.Min(
                Mathf.Min(x, size - 1 - x),
                Mathf.Min(y, size - 1 - y));
            if (edge <= 1)
            {
                return new Color32(104, 111, 119, 255);
            }

            var cellX = x % 16 - 8;
            var cellY = y % 16 - 8;
            if (Mathf.Abs(cellX) + Mathf.Abs(cellY) <= 4)
            {
                return new Color32(230, 236, 242, 255);
            }

            return new Color32(194, 201, 209, 255);
        }

        /// <summary>
        /// 벽 단면. 방마다 색을 바꾸지 않으므로 회색조가 아니라 최종 색을 그대로 굽고
        /// 렌더러는 흰색으로 둔다. 바깥 외곽선 → 금속 림 → 코어 순으로 어두워진다.
        /// </summary>
        private static Color32 CreateWallSectionPixel(int x, int y)
        {
            const int size = 32;
            var edge = Mathf.Min(
                Mathf.Min(x, size - 1 - x),
                Mathf.Min(y, size - 1 - y));
            if (edge <= 0)
            {
                return new Color32(6, 10, 15, 255);
            }

            if (edge <= 3)
            {
                return new Color32(62, 88, 108, 255);
            }

            if (edge <= 5)
            {
                return new Color32(30, 46, 60, 255);
            }

            return new Color32(17, 28, 39, 255);
        }

        /// <summary>
        /// 카메라를 향한 벽의 정면. 위쪽에 밝은 상단 모서리, 아래쪽에 어두운
        /// 걸레받이를 넣어 두께와 접지면을 읽히게 한다. 최종 색을 구워 틴트는 흰색이다.
        /// </summary>
        private static Color32 CreateWallFacePixel(int x, int y)
        {
            const int size = 32;
            var horizontalEdge = Mathf.Min(x, size - 1 - x);

            // 아래쪽 걸레받이. 바닥과 벽이 만나는 선을 분명히 한다.
            if (y <= 2)
            {
                return new Color32(8, 12, 18, 255);
            }

            if (y <= 5)
            {
                return new Color32(26, 38, 50, 255);
            }

            // 위쪽 상단 모서리 하이라이트
            if (y >= size - 3)
            {
                return new Color32(96, 126, 148, 255);
            }

            if (y >= size - 6)
            {
                return new Color32(64, 88, 108, 255);
            }

            if (horizontalEdge <= 0)
            {
                return new Color32(10, 15, 21, 255);
            }

            // 정면은 위에서 아래로 살짝 어두워진다.
            var shade = Mathf.Lerp(
                58f,
                34f,
                Mathf.InverseLerp(size - 6f, 5f, y));
            var value = (byte)Mathf.RoundToInt(shade);
            return new Color32(
                (byte)(value * 0.72f),
                value,
                (byte)(value * 1.24f),
                255);
        }

        /// <summary>
        /// 프롭 몸체. 카테고리 색을 곱해 쓰므로 밝은 회색조 면에 어두운 외곽선을 둔다.
        /// 곱셈 틴트는 어두운 화소를 어둡게 남기므로 외곽선은 어떤 색에서도 살아남는다.
        /// </summary>
        private static Color32 CreatePropBodyPixel(int x, int y)
        {
            const int size = 48;
            if (!IsRoundedRect(x, y, size, size, 7))
            {
                return new Color32(0, 0, 0, 0);
            }

            var edge = Mathf.Min(
                Mathf.Min(x, size - 1 - x),
                Mathf.Min(y, size - 1 - y));
            if (edge <= 1)
            {
                return new Color32(26, 34, 44, 255);
            }

            if (y >= size - 6)
            {
                return new Color32(255, 255, 255, 255);
            }

            if (y <= 5)
            {
                return new Color32(148, 155, 163, 255);
            }

            return new Color32(226, 232, 238, 255);
        }

        /// <summary>
        /// 카테고리 표식. 프롭 몸체 위에 얹어 112개 회색상자가 종류별로 구분되게 한다.
        /// 늘어나면 안 되므로 프롭 크기와 무관하게 고정 크기로 배치한다.
        /// </summary>
        private static Color32 CreatePropIconPixel(
            EnvironmentPropCategory category,
            int x,
            int y)
        {
            return IsPropIconInk(category, x, y)
                ? new Color32(20, 28, 38, 255)
                : new Color32(0, 0, 0, 0);
        }

        private static bool IsPropIconInk(
            EnvironmentPropCategory category,
            float x,
            float y)
        {
            const float center = 15.5f;
            switch (category)
            {
                case EnvironmentPropCategory.Medical:
                    return IsInsideRect(x, y, center, center, 3.6f, 12f) ||
                           IsInsideRect(x, y, center, center, 12f, 3.6f);

                case EnvironmentPropCategory.Laboratory:
                    if (IsInsideRect(x, y, center, 24.5f, 2.6f, 4.5f))
                    {
                        return true;
                    }

                    if (y is >= 5f and <= 20f)
                    {
                        var flaskHalf = Mathf.Lerp(
                            2.6f,
                            9.5f,
                            Mathf.InverseLerp(20f, 6f, y));
                        return Mathf.Abs(x - center) <= flaskHalf;
                    }

                    return false;

                case EnvironmentPropCategory.Storage:
                    return (IsInsideRect(x, y, center, center, 11f, 9.5f) &&
                            !IsInsideRect(x, y, center, center, 8f, 6.5f)) ||
                           IsInsideRect(x, y, center, 20.5f, 11f, 1.6f);

                case EnvironmentPropCategory.Security:
                    var shieldHalf = y >= 16f
                        ? 10f
                        : Mathf.Lerp(0f, 10f, Mathf.InverseLerp(3f, 16f, y));
                    return y <= 27f && Mathf.Abs(x - center) <= shieldHalf;

                case EnvironmentPropCategory.Power:
                    return IsNearSegment(x, y, 19f, 28f, 12f, 17f, 2.6f) ||
                           IsNearSegment(x, y, 12f, 17f, 19.5f, 16f, 2.6f) ||
                           IsNearSegment(x, y, 19.5f, 16f, 12f, 4f, 2.6f);

                case EnvironmentPropCategory.Quarantine:
                    var inRing =
                        IsInsideEllipse(x, y, center, center, 11f, 11f) &&
                        !IsInsideEllipse(x, y, center, center, 8.5f, 8.5f);
                    var inBars =
                        IsInsideEllipse(x, y, center, center, 8.5f, 8.5f) &&
                        (IsInsideRect(x, y, 10.5f, center, 1.3f, 9f) ||
                         IsInsideRect(x, y, center, center, 1.3f, 9f) ||
                         IsInsideRect(x, y, 20.5f, center, 1.3f, 9f));
                    return inRing || inBars;

                case EnvironmentPropCategory.Utility:
                    if (IsInsideEllipse(x, y, center, center, 8f, 8f) &&
                        !IsInsideEllipse(x, y, center, center, 4f, 4f))
                    {
                        return true;
                    }

                    for (var tooth = 0; tooth < 3; tooth++)
                    {
                        var angle = tooth * Mathf.PI / 3f;
                        var dx = Mathf.Cos(angle) * 11f;
                        var dy = Mathf.Sin(angle) * 11f;
                        if (IsNearSegment(
                                x,
                                y,
                                center - dx,
                                center - dy,
                                center + dx,
                                center + dy,
                                2.2f))
                        {
                            return true;
                        }
                    }

                    return false;

                case EnvironmentPropCategory.Hazard:
                    if (IsInsideRect(x, y, center, 14f, 1.6f, 5f) ||
                        IsInsideEllipse(x, y, center, 7f, 1.9f, 1.9f))
                    {
                        return true;
                    }

                    var outerHalf = Mathf.Lerp(
                        0f,
                        12f,
                        Mathf.InverseLerp(28f, 4f, y));
                    var innerHalf = Mathf.Lerp(
                        0f,
                        12f,
                        Mathf.InverseLerp(24f, 8f, y));
                    var inOuter = y is >= 4f and <= 28f &&
                                  Mathf.Abs(x - center) <= outerHalf;
                    var inInner = y is >= 8f and <= 24f &&
                                  Mathf.Abs(x - center) <= innerHalf;
                    return inOuter && !inInner;

                default:
                    foreach (var offset in PropIconDotOffsets)
                    {
                        if (IsInsideEllipse(
                                x,
                                y,
                                center + offset.x,
                                center + offset.y,
                                3f,
                                3f))
                        {
                            return true;
                        }
                    }

                    return false;
            }
        }

        private static string GetPropIconSpritePath(
            EnvironmentPropCategory category)
        {
            return SpriteRoot + "/S_PropIcon_" + category + ".asset";
        }

        private static Vector4 CreateUniformBorder(int borderPixels)
        {
            return new Vector4(
                borderPixels,
                borderPixels,
                borderPixels,
                borderPixels);
        }

        private static bool IsInsideRect(
            float x,
            float y,
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight)
        {
            return Mathf.Abs(x - centerX) <= halfWidth &&
                   Mathf.Abs(y - centerY) <= halfHeight;
        }

        private static bool IsNearSegment(
            float x,
            float y,
            float startX,
            float startY,
            float endX,
            float endY,
            float halfWidth)
        {
            var deltaX = endX - startX;
            var deltaY = endY - startY;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            var progress = lengthSquared <= 0.0001f
                ? 0f
                : Mathf.Clamp01(
                    ((x - startX) * deltaX + (y - startY) * deltaY) /
                    lengthSquared);
            var offsetX = x - (startX + deltaX * progress);
            var offsetY = y - (startY + deltaY * progress);
            return offsetX * offsetX + offsetY * offsetY <=
                   halfWidth * halfWidth;
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
            return EnsureSprite(
                path,
                spriteName,
                width,
                height,
                pixelFactory,
                pivot,
                pixelsPerUnit,
                Vector4.zero);
        }

        /// <param name="border">
        /// 9-slice 테두리(왼·아래·오른·위, px). 0이 아니면 <see cref="SpriteDrawMode.Sliced"/>로
        /// 그렸을 때 이 폭이 늘어나지 않아 외곽선 두께가 크기와 무관하게 일정해진다.
        /// </param>
        private static Sprite EnsureSprite(
            string path,
            string spriteName,
            int width,
            int height,
            Func<int, int, Color32> pixelFactory,
            Vector2 pivot,
            float pixelsPerUnit,
            Vector4 border)
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
                SpriteMeshType.FullRect,
                border);
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

        /// <summary>
        /// 완성 PNG가 있으면 사용하고, 아직 제작되지 않았거나 빠졌으면 기존 절차형
        /// 회색상자로 되돌아간다. 에셋 제작 중에도 씬 빌드를 막지 않기 위한 경계다.
        /// </summary>
        private static Sprite LoadPreferredSprite(
            string finalPath,
            string fallbackPath)
        {
            return LoadSprite(finalPath, throwIfMissing: false) ??
                   LoadSprite(fallbackPath);
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
                // 같은 회색조 타일을 유지하되 방 기능과 A/B 구역을 이동 중에도
                // 구분할 수 있도록 저채도 고유 색조를 곱한다. 정전 상태에서
                // 장비 실루엣과 손전등 대비를 해치지 않는 밝기 범위다.
                "VaccineA" => new Color(0.22f, 0.42f, 0.40f),
                "VaccineB" => new Color(0.20f, 0.38f, 0.44f),
                "LabA" => new Color(0.24f, 0.36f, 0.43f),
                "LabB" => new Color(0.27f, 0.31f, 0.43f),
                "QuarantineA" => new Color(0.42f, 0.22f, 0.24f),
                "QuarantineB" => new Color(0.37f, 0.21f, 0.31f),
                "Storage" => new Color(0.20f, 0.34f, 0.39f),
                "Security" => new Color(0.20f, 0.29f, 0.43f),
                "Power" => new Color(0.44f, 0.34f, 0.16f),
                "Ward" => new Color(0.29f, 0.38f, 0.34f),
                _ => new Color(0.22f, 0.31f, 0.35f)
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

            // 전역광 강도는 밸런스 값이므로 상수로 비교하지 않고
            // SO_WorldLightingBalance_Default와 일치하는지만 확인한다.
            var expectedDarkIntensity =
                EnsureWorldLightingBalanceConfig().DarkGlobalIntensityRatio;
            var globalLight = GameObject.Find("Light_GlobalEmergency")?
                .GetComponent<Light2D>();
            if (globalLight == null ||
                globalLight.lightType != Light2D.LightType.Global ||
                Mathf.Abs(globalLight.intensity - expectedDarkIntensity) >
                    0.0001f)
            {
                failures.Add(
                    "The near-dark global emergency light does not match " +
                    "SO_WorldLightingBalance_Default.");
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
                float end,
                bool hasVisibleFace = false)
            {
                IsHorizontal = isHorizontal;
                FixedCoordinate = fixedCoordinate;
                Start = start;
                End = end;
                HasVisibleFace = hasVisibleFace;
            }

            public bool IsHorizontal { get; }
            public float FixedCoordinate { get; }
            public float Start { get; }
            public float End { get; }

            /// <summary>
            /// 바닥이 이 벽의 아래에 있어 벽 정면이 카메라를 향하는가.
            /// 이런 벽만 입면을 그린다. 방 아래쪽 벽에 입면을 그리면
            /// 그 방 바닥을 덮어버린다(아트 가이드 §1.1 혼합 시점).
            /// </summary>
            public bool HasVisibleFace { get; }
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
