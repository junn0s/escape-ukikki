using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Settings;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class FuseMissionView : MonoBehaviour
    {
        private const float PanelWidth = 680f;
        private const float PanelHeight = 500f;
        private const float StatusMessageDurationSeconds = 2.5f;
        private const float ResultEffectDurationSeconds = 1.2f;
        private const float DangerPulseSpeed = 4f;
        private const float DangerBorderThickness = 18f;
        private const float PressureInputSendIntervalSeconds = 0.1f;

        private enum MissionResultEffect : byte
        {
            None = 0,
            Success = 1,
            Failure = 2
        }

        private static readonly Color[] FuseColors =
        {
            new(0.20f, 0.78f, 0.80f),
            new(0.91f, 0.72f, 0.29f),
            new(0.88f, 0.28f, 0.32f),
            new(0.55f, 0.31f, 0.78f),
            new(0.80f, 0.84f, 0.86f)
        };

        private static readonly string[] FuseColorNames =
        {
            "청록",
            "노랑",
            "빨강",
            "보라",
            "흰색"
        };

        private static readonly string[] SampleBinNames =
        {
            "A 보관함",
            "B 보관함",
            "C 보관함",
            "D 보관함",
            "E 보관함"
        };

        private static readonly string[] CircuitRotationGlyphs =
        {
            "↑", "→", "↓", "←"
        };

        private static readonly string[] ServerLogKeyNames =
        {
            "Q", "W", "E", "R", "T"
        };

        [SerializeField] private FuseStationPrototype _station;

        [Tooltip("미션 부품 그림. 비어 있으면 예전 회색 상자로 그린다")]
        [SerializeField] private MissionUiSpriteCatalog _spriteCatalog;

        private bool _isOpenBacking;

        /// <summary>
        /// 열림 상태를 <see cref="MissionOverlayState"/>에 함께 알린다.
        /// 방 배너·조작 도움말·해독제 상태가 미션 안내문 위에 겹치지 않게 하려면
        /// 다른 화면들도 이 상태를 알아야 한다.
        /// </summary>
        private bool _isOpen
        {
            get => _isOpenBacking;
            set
            {
                _isOpenBacking = value;
                MissionOverlayState.SetOpen(value);
            }
        }
        private bool _isSubscribed;
        private string _statusMessage = string.Empty;
        private Color _statusColor = Color.white;
        private float _statusVisibleUntil;
        private int _draggedFuseId;
        private int _selectedCctvChannelId;
        private Vector2 _dragPosition;
        private bool _isDraggingBattery;
        private int _draggedPressureValveIndex = -1;
        private readonly float[] _pressureValvePreview =
            new float[PressureValveMissionInstance.ValveCount];
        private bool _isDraggingPressureLock;
        private float _pressureLockDragStartY;
        private float _pressureLockNormalized;
        private float _nextPressureInputSendTime;
        private MonsterTarget _activeInteractorTarget;
        private NetworkObject _activeInteractorNetworkObject;
        private MissionResultEffect _resultEffect;
        private float _resultEffectStartedAt;

        public void Configure(FuseStationPrototype station)
        {
            Unsubscribe();
            _station = station;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _isOpen = false;
            ResetInteractionState();
        }

        private void OnGUI()
        {
            if (_isOpen && _station != null)
            {
                DrawMissionPanel();
                if (IsActiveInteractorThreatened())
                {
                    DrawDangerWarning();
                }
            }

            if (_station != null && _station.IsBatteryCarrying &&
                !_station.IsBatteryInsertionOpen)
            {
                DrawBatteryCarryHud();
            }

            DrawResultEffect();
            if (!string.IsNullOrEmpty(_statusMessage) && Time.unscaledTime <= _statusVisibleUntil)
            {
                DrawStatusBanner();
            }
        }

        private void DrawMissionPanel()
        {
            var panelRect = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);

            // 기본 스킨 상자는 반투명이라 어두운 월드 위에서 배경이 없는 것처럼 보이고
            // 글자가 맵과 겹쳐 읽히지 않는다. 불투명 패널을 먼저 깐다.
            DrawPanelBackground(panelRect);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 20f,
                    panelRect.y + 18f,
                    panelRect.width - 40f,
                    42f),
                GetMissionTitle(),
                titleStyle);

            var instructionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = new Color(0.84f, 0.90f, 0.94f) }
            };
            GUI.Label(
                new Rect(panelRect.x + 25f, panelRect.y + 62f, panelRect.width - 50f, 32f),
                GetMissionInstruction(),
                instructionStyle);

            switch (_station.Kind)
            {
                case MissionPrototypeKind.FuseSequence:
                    DrawFuseButtons(panelRect);
                    DrawTargetSlots(panelRect);
                    break;
                case MissionPrototypeKind.BreakerSequence:
                    DrawBreakerTiming(panelRect);
                    break;
                case MissionPrototypeKind.CctvReboot:
                    DrawCctvChannels(panelRect);
                    break;
                case MissionPrototypeKind.SampleSorting:
                    DrawSampleSorting(panelRect);
                    break;
                case MissionPrototypeKind.BatteryTransport:
                    DrawBatteryTransport(panelRect);
                    break;
                case MissionPrototypeKind.PressureValves:
                    DrawPressureValves(panelRect);
                    break;
                case MissionPrototypeKind.SecurityCircuit:
                    DrawSecurityCircuit(panelRect);
                    break;
                case MissionPrototypeKind.AntennaAlignment:
                    DrawAntennaAlignment(panelRect);
                    break;
                case MissionPrototypeKind.ServerLogRecovery:
                    DrawServerLogRecovery(panelRect);
                    break;
            }

            var cancelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            if (GUI.Button(
                    new Rect(panelRect.x + panelRect.width - 155f, panelRect.y + panelRect.height - 58f, 125f, 36f),
                    "나가기 (Esc)",
                    cancelStyle))
            {
                _station.CancelMission();
            }
        }

        private void DrawFuseButtons(Rect panelRect)
        {
            var sectionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 35f,
                    panelRect.y + 112f,
                    250f,
                    30f),
                GetItemSectionTitle(),
                sectionStyle);

            for (var index = 0; index < _station.FuseCount; index++)
            {
                var fuseId = index + 1;
                var row = index / 2;
                var column = index % 2;
                var rect = new Rect(
                    panelRect.x + 45f + column * 125f,
                    panelRect.y + 155f + row * 78f,
                    105f,
                    58f);
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = FuseColors[index];
                var label = _station.IsFuseInserted(fuseId)
                    ? $"{fuseId}\n완료"
                    : GetItemLabel(fuseId);

                GUI.Box(rect, label);
                if (!_station.IsFuseInserted(fuseId) &&
                    Event.current.type == EventType.MouseDown &&
                    rect.Contains(Event.current.mousePosition))
                {
                    _draggedFuseId = fuseId;
                    _dragPosition = Event.current.mousePosition;
                    Event.current.Use();
                }

                GUI.backgroundColor = previousColor;
            }

            if (_draggedFuseId == 0)
            {
                return;
            }

            UpdateDragPosition();
            var previousDragColor = GUI.backgroundColor;
            GUI.backgroundColor = FuseColors[_draggedFuseId - 1];
            GUI.Box(
                new Rect(
                    _dragPosition.x - 52f,
                    _dragPosition.y - 29f,
                    105f,
                    58f),
                GetItemLabel(_draggedFuseId));
            GUI.backgroundColor = previousDragColor;

            if (Event.current.type != EventType.MouseUp)
            {
                return;
            }

            var currentSlotRect = new Rect(
                panelRect.x + 355f,
                panelRect.y + 158f + _station.ProgressIndex * 47f,
                255f,
                38f);
            var droppedFuseId = _draggedFuseId;
            _draggedFuseId = 0;
            if (currentSlotRect.Contains(Event.current.mousePosition))
            {
                _station.SubmitFuse(droppedFuseId);
            }

            Event.current.Use();
        }

        private void DrawTargetSlots(Rect panelRect)
        {
            var sectionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 340f,
                    panelRect.y + 112f,
                    300f,
                    30f),
                GetTargetSectionTitle(),
                sectionStyle);

            var slotStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            for (var index = 0; index < _station.RequiredOrder.Count; index++)
            {
                var fuseId = _station.RequiredOrder[index];
                var rect = new Rect(
                    panelRect.x + 355f,
                    panelRect.y + 158f + index * 47f,
                    255f,
                    38f);
                var prefix = index < _station.ProgressIndex ? "✓" : index == _station.ProgressIndex ? "▶" : "·";
                var label =
                    $"{prefix} {index + 1}단계: {GetTargetLabel(fuseId)}";
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = FuseColors[fuseId - 1];
                GUI.Box(rect, label, slotStyle);
                GUI.backgroundColor = previousColor;
            }
        }

        private void DrawBreakerTiming(Rect panelRect)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(panelRect.x + 40f, panelRect.y + 112f, 600f, 32f),
                $"기동 단계 {_station.ProgressIndex + 1}/{_station.FuseCount}",
                labelStyle);

            var trackRect = new Rect(
                panelRect.x + 65f,
                panelRect.y + 175f,
                panelRect.width - 130f,
                42f);
            GUI.Box(trackRect, string.Empty);

            var targetCenter = trackRect.x +
                               trackRect.width *
                               _station.BreakerTargetNormalized;
            var targetWidth = trackRect.width *
                              _station.BreakerSuccessToleranceNormalized * 2f;
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.72f, 0.30f);
            GUI.Box(
                new Rect(
                    targetCenter - targetWidth * 0.5f,
                    trackRect.y + 4f,
                    targetWidth,
                    trackRect.height - 8f),
                "안전");

            GUI.backgroundColor = new Color(0.95f, 0.78f, 0.18f);
            var markerX = trackRect.x +
                          trackRect.width *
                          _station.BreakerMarkerNormalized;
            GUI.Box(
                new Rect(markerX - 5f, trackRect.y - 8f, 10f, trackRect.height + 16f),
                string.Empty);
            GUI.backgroundColor = previousColor;

            var engageRect = new Rect(
                panelRect.x + 215f,
                panelRect.y + 255f,
                250f,
                92f);
            var engageStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.backgroundColor = new Color(0.95f, 0.70f, 0.16f);
            var keyboardTriggered = Event.current.type == EventType.KeyDown &&
                                    Event.current.keyCode == KeyCode.Space;
            var pointerTriggered = GUI.Button(
                engageRect,
                "지금 투입!\n클릭 또는 Space",
                engageStyle);
            GUI.backgroundColor = previousColor;
            if (pointerTriggered || keyboardTriggered)
            {
                _station.SubmitBreakerTiming();
                if (keyboardTriggered)
                {
                    Event.current.Use();
                }
            }
        }

        private void DrawCctvChannels(Rect panelRect)
        {
            var channelCount = _station.CctvChannelCount;
            var nodeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };

            for (var channelId = 1;
                 channelId <= channelCount;
                 channelId++)
            {
                if (!_station.IsCctvChannelConnected(channelId))
                {
                    continue;
                }

                var targetSlotIndex = FindCctvTargetSlotIndex(
                    channelId,
                    channelCount);
                if (targetSlotIndex < 0)
                {
                    continue;
                }

                DrawCable(
                    GetCctvSourceRect(panelRect, channelId - 1).center,
                    GetCctvTargetRect(panelRect, targetSlotIndex).center,
                    FuseColors[channelId - 1],
                    7f);
            }

            for (var index = 0; index < channelCount; index++)
            {
                var channelId = index + 1;
                var previousColor = GUI.backgroundColor;
                var sourceRect = GetCctvSourceRect(panelRect, index);
                GUI.backgroundColor = FuseColors[index];
                var sourceLabel = _station.IsCctvChannelConnected(channelId)
                    ? $"신호 {(char)('A' + index)}  연결됨 ✓"
                    : _selectedCctvChannelId == channelId
                        ? $"신호 {(char)('A' + index)}  선택됨 ▶"
                        : $"신호 {(char)('A' + index)}";
                if (_station.IsCctvChannelConnected(channelId))
                {
                    GUI.Box(sourceRect, sourceLabel, nodeStyle);
                }
                else if (GUI.Button(sourceRect, sourceLabel, nodeStyle))
                {
                    _selectedCctvChannelId =
                        _selectedCctvChannelId == channelId
                            ? 0
                            : channelId;
                }

                GUI.backgroundColor = previousColor;

                var targetChannelId =
                    _station.GetCctvTargetChannelAtSlot(index);
                var targetRect = GetCctvTargetRect(panelRect, index);
                GUI.backgroundColor = FuseColors[targetChannelId - 1];
                var targetLabel =
                    $"포트 {(char)('A' + targetChannelId - 1)}";
                if (_selectedCctvChannelId == 0)
                {
                    GUI.Box(targetRect, targetLabel, nodeStyle);
                }
                else if (GUI.Button(targetRect, targetLabel, nodeStyle))
                {
                    var sourceChannelId = _selectedCctvChannelId;
                    _selectedCctvChannelId = 0;
                    _station.SubmitCctvConnection(
                        sourceChannelId,
                        targetChannelId);
                    if (sourceChannelId != targetChannelId)
                    {
                        ShowStatus(
                            "채널이 맞지 않습니다. 문자와 색을 다시 확인하세요.",
                            new Color(0.42f, 0.46f, 0.52f));
                    }
                }

                GUI.backgroundColor = previousColor;
            }

            if (_selectedCctvChannelId != 0)
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor =
                    FuseColors[_selectedCctvChannelId - 1];
                DrawCable(
                    GetCctvSourceRect(
                        panelRect,
                        _selectedCctvChannelId - 1).center,
                    Event.current.mousePosition,
                    GUI.backgroundColor,
                    5f);
                GUI.backgroundColor = previousColor;
            }

            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 80f,
                    panelRect.y + panelRect.height - 105f,
                    520f,
                    34f),
                $"복구된 채널 {_station.ProgressIndex}/{channelCount}",
                statusStyle);
        }

        private void DrawSampleSorting(Rect panelRect)
        {
            var headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(panelRect.x + 35f, panelRect.y + 112f, 285f, 30f),
                "분류 대기 시료",
                headingStyle);
            GUI.Label(
                new Rect(panelRect.x + 360f, panelRect.y + 112f, 285f, 30f),
                "보관함 선택",
                headingStyle);

            for (var index = 0; index < _station.SampleCount; index++)
            {
                var sampleId = index + 1;
                var category = _station.GetSampleRequiredCategory(sampleId);
                var isSorted = _station.IsSampleSorted(sampleId);
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor =
                    FuseColors[Mathf.Max(0, category - 1)];
                var label = isSorted
                    ? $"시료 {sampleId}  ✓"
                    : _station.SelectedSampleId == sampleId
                        ? $"시료 {sampleId}  선택됨 ▶\n라벨 {SampleBinNames[Mathf.Max(0, category - 1)][0]}"
                        : $"시료 {sampleId}\n라벨 {SampleBinNames[Mathf.Max(0, category - 1)][0]}";
                var sampleRect = new Rect(
                    panelRect.x + 60f + (index % 2) * 125f,
                    panelRect.y + 155f + (index / 2) * 72f,
                    108f,
                    54f);
                if (isSorted)
                {
                    GUI.Box(sampleRect, label);
                }
                else if (GUI.Button(sampleRect, label))
                {
                    _station.SelectSample(sampleId);
                }

                GUI.backgroundColor = previousColor;
            }

            for (var index = 0; index < _station.SampleCategoryCount; index++)
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = FuseColors[index];
                var binRect = GetSampleBinRect(panelRect, index);
                if (_station.SelectedSampleId == 0)
                {
                    GUI.Box(binRect, SampleBinNames[index]);
                }
                else if (GUI.Button(binRect, SampleBinNames[index]))
                {
                    _station.SubmitSampleCategory(index + 1);
                }

                GUI.backgroundColor = previousColor;
            }
        }

        private void DrawBatteryTransport(Rect panelRect)
        {
            var isInsertion = _station.IsBatteryInsertionOpen;
            var headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var batteryStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 45f,
                    panelRect.y + 115f,
                    panelRect.width - 90f,
                    36f),
                isInsertion
                    ? "운반한 배터리를 수신 단말기에 장착"
                    : "잠금 거치대에서 비상 배터리 분리",
                headingStyle);

            var batteryRect = new Rect(
                panelRect.x + 80f,
                panelRect.y + 205f,
                190f,
                90f);
            var targetRect = new Rect(
                panelRect.x + panelRect.width - 275f,
                panelRect.y + 190f,
                195f,
                120f);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.92f, 0.68f, 0.12f);
            GUI.Box(
                batteryRect,
                isInsertion ? "비상 배터리\n운반 완료" : "비상 배터리\n잠금핀 해제",
                batteryStyle);
            GUI.backgroundColor = isInsertion
                ? new Color(0.18f, 0.72f, 0.32f)
                : new Color(0.24f, 0.48f, 0.62f);
            GUI.Box(
                targetRect,
                isInsertion ? "전원 소켓\n여기에 밀어 넣기" : "분리 레일\n여기까지 당기기",
                batteryStyle);
            GUI.backgroundColor = previousColor;

            DrawCable(
                new Vector2(batteryRect.xMax, batteryRect.center.y),
                new Vector2(targetRect.xMin, targetRect.center.y),
                new Color(0.72f, 0.76f, 0.80f, 0.8f),
                8f);

            if (!_isDraggingBattery &&
                Event.current.type == EventType.MouseDown &&
                batteryRect.Contains(Event.current.mousePosition))
            {
                _isDraggingBattery = true;
                _dragPosition = Event.current.mousePosition;
                Event.current.Use();
            }

            if (!_isDraggingBattery)
            {
                return;
            }

            UpdateDragPosition();
            GUI.backgroundColor = new Color(1f, 0.76f, 0.12f);
            GUI.Box(
                new Rect(
                    _dragPosition.x - 90f,
                    _dragPosition.y - 42f,
                    180f,
                    84f),
                "비상 배터리",
                batteryStyle);
            GUI.backgroundColor = previousColor;
            if (Event.current.type != EventType.MouseUp)
            {
                return;
            }

            var droppedInTarget =
                targetRect.Contains(Event.current.mousePosition);
            _isDraggingBattery = false;
            if (droppedInTarget)
            {
                if (isInsertion)
                {
                    _station.SubmitBatteryInsert();
                }
                else
                {
                    _station.SubmitBatteryDetach();
                }
            }

            Event.current.Use();
        }

        private void DrawBatteryCarryHud()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.84f, 0.57f, 0.08f, 0.94f);
            GUI.Box(
                new Rect(24f, Screen.height - 105f, 420f, 72f),
                "비상 배터리 운반 중  |  수신 단말기에서 E로 장착\nEsc: 배터리 놓기 (낙하 소음 발생)",
                style);
            GUI.backgroundColor = previousColor;
        }

        /// <summary>미션 부품 그림을 연결한다. 없으면 예전 표현으로 되돌아간다.</summary>
        public void SetSpriteCatalog(MissionUiSpriteCatalog catalog)
        {
            _spriteCatalog = catalog;
        }

        /// <summary>
        /// 미션 패널 배경. 어두운 월드 위에서 글자가 읽히려면 불투명해야 한다
        /// (ui-ux-design.md §9.1 공통 프레임, §15.3 패널).
        /// </summary>
        private void DrawPanelBackground(Rect panelRect)
        {
            var panel = MissionUiSpriteCatalog.GetTexture(
                _spriteCatalog != null ? _spriteCatalog.Panel : null);
            if (panel != null)
            {
                GUI.DrawTexture(panelRect, panel, ScaleMode.StretchToFill);
                return;
            }

            var previousColor = GUI.color;
            GUI.color = new Color(0.078f, 0.102f, 0.149f, 0.97f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.227f, 0.290f, 0.388f, 1f);
            GUI.DrawTexture(
                new Rect(panelRect.x, panelRect.y, panelRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(panelRect.x, panelRect.yMax - 2f, panelRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawPressureValves(Rect panelRect)
        {
            if (_draggedPressureValveIndex < 0)
            {
                for (var index = 0;
                     index < PressureValveMissionInstance.ValveCount;
                     index++)
                {
                    _pressureValvePreview[index] =
                        _station.GetPressureValveOpening(index);
                }
            }

            var leftCenter = new Vector2(
                panelRect.x + 185f,
                panelRect.y + 245f);
            var rightCenter = new Vector2(
                panelRect.x + panelRect.width - 185f,
                panelRect.y + 245f);
            DrawPressureValve(0, leftCenter, "공급 밸브");
            DrawPressureValve(1, rightCenter, "보조 밸브");

            var previewPressure =
                (_pressureValvePreview[0] +
                 _pressureValvePreview[1]) * 0.5f;
            var gaugeRect = new Rect(
                panelRect.x + 105f,
                panelRect.y + 355f,
                panelRect.width - 210f,
                38f);
            GUI.Box(gaugeRect, string.Empty);
            var targetCenter = gaugeRect.x + gaugeRect.width *
                _station.PressureTargetNormalized;
            var safeWidth = gaugeRect.width *
                _station.PressureToleranceNormalized * 2f;
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.72f, 0.30f);
            GUI.Box(
                new Rect(
                    targetCenter - safeWidth * 0.5f,
                    gaugeRect.y + 4f,
                    safeWidth,
                    gaugeRect.height - 8f),
                "안정");
            GUI.backgroundColor = new Color(0.95f, 0.72f, 0.10f);
            var needleX = gaugeRect.x + gaugeRect.width * previewPressure;
            GUI.Box(
                new Rect(
                    needleX - 5f,
                    gaugeRect.y - 8f,
                    10f,
                    gaugeRect.height + 16f),
                string.Empty);
            GUI.backgroundColor = previousColor;

            var stabilityRect = new Rect(
                panelRect.x + 190f,
                panelRect.y + 410f,
                250f,
                24f);
            GUI.Box(stabilityRect, "압력 안정화");
            GUI.backgroundColor = new Color(0.10f, 0.68f, 0.34f);
            GUI.Box(
                new Rect(
                    stabilityRect.x + 3f,
                    stabilityRect.y + 3f,
                    (stabilityRect.width - 6f) *
                    _station.PressureStabilityProgress,
                    stabilityRect.height - 6f),
                string.Empty);
            GUI.backgroundColor = previousColor;

            DrawPressureLockLever(panelRect);
        }

        private void DrawPressureValve(
            int valveIndex,
            Vector2 center,
            string label)
        {
            var opening = _pressureValvePreview[valveIndex];
            var knobRect = new Rect(
                center.x - 66f,
                center.y - 66f,
                132f,
                132f);
            var angleDegrees = Mathf.Lerp(135f, 405f, opening);
            var dial = MissionUiSpriteCatalog.GetTexture(
                _spriteCatalog != null ? _spriteCatalog.Dial : null);
            if (dial != null)
            {
                // 원형 조작이므로 다이얼 자체를 돌려 개방도를 읽히게 한다.
                // 글자는 같이 돌면 못 읽으므로 회전 밖에서 그린다.
                var previousMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(angleDegrees - 90f, center);
                GUI.DrawTexture(knobRect, dial);
                GUI.matrix = previousMatrix;
            }
            else
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.22f, 0.46f, 0.60f);
                GUI.Box(knobRect, string.Empty);
                GUI.backgroundColor = previousColor;
            }

            var knobStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(knobRect.x, knobRect.y - 34f, knobRect.width, 30f),
                label,
                knobStyle);
            GUI.Label(
                knobRect,
                $"{Mathf.RoundToInt(opening * 100f)}%",
                knobStyle);

            if (dial == null)
            {
                var angleRadians = angleDegrees * Mathf.Deg2Rad;
                var pointerEnd = center + new Vector2(
                    Mathf.Cos(angleRadians),
                    Mathf.Sin(angleRadians)) * 52f;
                DrawCable(center, pointerEnd, Color.white, 7f);
            }

            if (_draggedPressureValveIndex < 0 &&
                Event.current.type == EventType.MouseDown &&
                knobRect.Contains(Event.current.mousePosition))
            {
                _draggedPressureValveIndex = valveIndex;
                _pressureValvePreview[valveIndex] =
                    GetValveOpeningFromPointer(
                        center,
                        Event.current.mousePosition);
                Event.current.Use();
                return;
            }

            if (_draggedPressureValveIndex != valveIndex)
            {
                return;
            }

            if (Event.current.type == EventType.MouseDrag)
            {
                _pressureValvePreview[valveIndex] =
                    GetValveOpeningFromPointer(
                        center,
                        Event.current.mousePosition);
                if (Time.unscaledTime >= _nextPressureInputSendTime)
                {
                    _nextPressureInputSendTime =
                        Time.unscaledTime +
                        PressureInputSendIntervalSeconds;
                    _station.SubmitPressureValve(
                        valveIndex,
                        _pressureValvePreview[valveIndex]);
                }

                Event.current.Use();
                return;
            }

            if (Event.current.type == EventType.MouseUp)
            {
                var submittedOpening =
                    _pressureValvePreview[valveIndex];
                _draggedPressureValveIndex = -1;
                _station.SubmitPressureValve(
                    valveIndex,
                    submittedOpening);
                Event.current.Use();
            }
        }

        private void DrawPressureLockLever(Rect panelRect)
        {
            var trackRect = new Rect(
                panelRect.x + panelRect.width - 95f,
                panelRect.y + 350f,
                55f,
                90f);
            GUI.Box(trackRect, "잠금");
            var handleRect = new Rect(
                trackRect.x + 7f,
                trackRect.y + 8f + _pressureLockNormalized * 48f,
                trackRect.width - 14f,
                28f);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.92f, 0.62f, 0.08f);
            GUI.Box(handleRect, "▼");
            GUI.backgroundColor = previousColor;

            if (!_isDraggingPressureLock &&
                Event.current.type == EventType.MouseDown &&
                handleRect.Contains(Event.current.mousePosition))
            {
                _isDraggingPressureLock = true;
                _pressureLockDragStartY = Event.current.mousePosition.y;
                _pressureLockNormalized = 0f;
                Event.current.Use();
                return;
            }

            if (!_isDraggingPressureLock)
            {
                return;
            }

            if (Event.current.type == EventType.MouseDrag)
            {
                _pressureLockNormalized = Mathf.Clamp01(
                    (Event.current.mousePosition.y -
                     _pressureLockDragStartY) / 48f);
                Event.current.Use();
                return;
            }

            if (Event.current.type == EventType.MouseUp)
            {
                var didLock = _pressureLockNormalized >= 0.85f;
                _isDraggingPressureLock = false;
                _pressureLockNormalized = 0f;
                if (didLock)
                {
                    _station.SubmitPressureLock();
                }

                Event.current.Use();
            }
        }

        private static float GetValveOpeningFromPointer(
            Vector2 center,
            Vector2 pointer)
        {
            var delta = pointer - center;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 135f)
            {
                angle += 360f;
            }

            return Mathf.InverseLerp(135f, 405f, angle);
        }

        private void DrawSecurityCircuit(Rect panelRect)
        {
            var headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(
                    panelRect.x + 45f,
                    panelRect.y + 112f,
                    panelRect.width - 90f,
                    34f),
                "모듈을 클릭해 현재 화살표를 목표 방향과 일치시키세요.",
                headingStyle);

            var nodeCount = _station.SecurityCircuitNodeCount;
            var nodeWidth = 104f;
            var gap = 12f;
            var totalWidth = nodeCount * nodeWidth +
                             (nodeCount - 1) * gap;
            var startX = panelRect.x +
                         (panelRect.width - totalWidth) * 0.5f;
            for (var index = 0; index < nodeCount; index++)
            {
                var currentRotation =
                    _station.GetSecurityCircuitRotation(index);
                var targetRotation =
                    _station.GetSecurityCircuitTargetRotation(index);
                var isAligned = currentRotation == targetRotation;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = isAligned
                    ? new Color(0.12f, 0.68f, 0.32f)
                    : new Color(0.28f, 0.46f, 0.62f);
                var nodeStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                var nodeRect = new Rect(
                    startX + index * (nodeWidth + gap),
                    panelRect.y + 180f,
                    nodeWidth,
                    112f);
                var label =
                    $"모듈 {index + 1}\n현재 {CircuitRotationGlyphs[currentRotation]}\n목표 {CircuitRotationGlyphs[targetRotation]}";
                if (GUI.Button(nodeRect, label, nodeStyle))
                {
                    _station.RotateSecurityCircuitNode(index, 1);
                }

                GUI.backgroundColor = previousColor;
            }

            var keyboardTriggered =
                Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Space;
            var testStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousTestColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.88f, 0.55f, 0.12f);
            var clicked = GUI.Button(
                new Rect(
                    panelRect.x + 205f,
                    panelRect.y + 335f,
                    270f,
                    70f),
                $"회로 전원 인가  (Space)\n정렬 {_station.ProgressIndex}/{nodeCount}",
                testStyle);
            GUI.backgroundColor = previousTestColor;
            if (clicked || keyboardTriggered)
            {
                _station.TestSecurityCircuit();
                if (keyboardTriggered)
                {
                    Event.current.Use();
                }
            }
        }

        private void DrawAntennaAlignment(Rect panelRect)
        {
            HandleAntennaKeyboard();

            var gridSize = _station.AntennaGridSize;
            const float cellSize = 48f;
            var gridWidth = gridSize * cellSize;
            var gridRect = new Rect(
                panelRect.x + 72f,
                panelRect.y + 150f,
                gridWidth,
                gridWidth);
            for (var frequency = 0; frequency < gridSize; frequency++)
            {
                for (var azimuth = 0; azimuth < gridSize; azimuth++)
                {
                    var isTarget =
                        azimuth == _station.AntennaTargetAzimuth &&
                        frequency == _station.AntennaTargetFrequency;
                    var isCurrent =
                        azimuth == _station.AntennaAzimuth &&
                        frequency == _station.AntennaFrequency;
                    var previousCellColor = GUI.backgroundColor;
                    GUI.backgroundColor = isTarget && isCurrent
                        ? new Color(0.10f, 0.78f, 0.34f)
                        : isCurrent
                            ? new Color(0.95f, 0.70f, 0.10f)
                            : isTarget
                                ? new Color(0.12f, 0.58f, 0.32f)
                                : new Color(0.18f, 0.28f, 0.36f);
                    GUI.Box(
                        new Rect(
                            gridRect.x + azimuth * cellSize,
                            gridRect.y +
                            (gridSize - 1 - frequency) * cellSize,
                            cellSize - 2f,
                            cellSize - 2f),
                        isCurrent ? "●" : isTarget ? "◎" : string.Empty);
                    GUI.backgroundColor = previousCellColor;
                }
            }

            var controlX = panelRect.x + 405f;
            var controlY = panelRect.y + 166f;
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            if (GUI.Button(
                    new Rect(controlX + 60f, controlY, 72f, 54f),
                    "W / ↑",
                    buttonStyle))
            {
                _station.AdjustAntenna(
                    AntennaAlignmentMissionInstance.FrequencyAxis,
                    1);
            }

            if (GUI.Button(
                    new Rect(controlX, controlY + 62f, 72f, 54f),
                    "A / ←",
                    buttonStyle))
            {
                _station.AdjustAntenna(
                    AntennaAlignmentMissionInstance.AzimuthAxis,
                    -1);
            }

            if (GUI.Button(
                    new Rect(controlX + 120f, controlY + 62f, 72f, 54f),
                    "D / →",
                    buttonStyle))
            {
                _station.AdjustAntenna(
                    AntennaAlignmentMissionInstance.AzimuthAxis,
                    1);
            }

            if (GUI.Button(
                    new Rect(controlX + 60f, controlY + 124f, 72f, 54f),
                    "S / ↓",
                    buttonStyle))
            {
                _station.AdjustAntenna(
                    AntennaAlignmentMissionInstance.FrequencyAxis,
                    -1);
            }

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.65f, 0.76f);
            if (GUI.Button(
                    new Rect(controlX, controlY + 205f, 192f, 65f),
                    "구조 신호 고정\nEnter",
                    buttonStyle))
            {
                _station.LockAntennaSignal();
            }

            GUI.backgroundColor = previousColor;
            GUI.Label(
                new Rect(
                    panelRect.x + 55f,
                    panelRect.y + 410f,
                    340f,
                    30f),
                $"현재 방위 {_station.AntennaAzimuth + 1} / 주파수 {_station.AntennaFrequency + 1}",
                buttonStyle);
        }

        private void HandleAntennaKeyboard()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            var wasHandled = true;
            switch (Event.current.keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _station.AdjustAntenna(
                        AntennaAlignmentMissionInstance.AzimuthAxis,
                        -1);
                    break;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _station.AdjustAntenna(
                        AntennaAlignmentMissionInstance.AzimuthAxis,
                        1);
                    break;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _station.AdjustAntenna(
                        AntennaAlignmentMissionInstance.FrequencyAxis,
                        1);
                    break;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _station.AdjustAntenna(
                        AntennaAlignmentMissionInstance.FrequencyAxis,
                        -1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _station.LockAntennaSignal();
                    break;
                default:
                    wasHandled = false;
                    break;
            }

            if (wasHandled)
            {
                Event.current.Use();
            }
        }

        private void DrawServerLogRecovery(Rect panelRect)
        {
            var keyToken = GetServerLogKeyToken(Event.current);
            if (keyToken != 0)
            {
                _station.SubmitServerLogToken(keyToken);
                Event.current.Use();
                if (!_station.IsMissionActive)
                {
                    return;
                }
            }

            var consoleStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.34f, 1f, 0.52f) }
            };
            GUI.Box(
                new Rect(
                    panelRect.x + 55f,
                    panelRect.y + 125f,
                    panelRect.width - 110f,
                    92f),
                $"  RECOVERY://LAB_ARCHIVE\n  손상 블록 {_station.ProgressIndex}/{_station.ServerLogTokenCount} 복구됨",
                consoleStyle);

            var tokenCount = _station.ServerLogTokenCount;
            var tokenWidth = 88f;
            var gap = 14f;
            var totalWidth = tokenCount * tokenWidth +
                             (tokenCount - 1) * gap;
            var startX = panelRect.x +
                         (panelRect.width - totalWidth) * 0.5f;
            var keyStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            for (var index = 0; index < tokenCount; index++)
            {
                var token = _station.GetServerLogRequiredToken(index);
                var isRecovered = index < _station.ProgressIndex;
                var isCurrent = index == _station.ProgressIndex;
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = isRecovered
                    ? new Color(0.12f, 0.62f, 0.30f)
                    : isCurrent
                        ? new Color(0.88f, 0.58f, 0.12f)
                        : new Color(0.25f, 0.34f, 0.42f);
                var rect = new Rect(
                    startX + index * (tokenWidth + gap),
                    panelRect.y + 255f,
                    tokenWidth,
                    82f);
                var label = isRecovered
                    ? $"{ServerLogKeyNames[token - 1]}\n✓"
                    : $"[{ServerLogKeyNames[token - 1]}]\n{index + 1:00}";
                if (!isRecovered && GUI.Button(rect, label, keyStyle))
                {
                    _station.SubmitServerLogToken(token);
                    GUI.backgroundColor = previousColor;
                    if (!_station.IsMissionActive)
                    {
                        return;
                    }
                }
                else if (isRecovered)
                {
                    GUI.Box(rect, label, keyStyle);
                }

                GUI.backgroundColor = previousColor;
            }

            GUI.Label(
                new Rect(
                    panelRect.x + 70f,
                    panelRect.y + 365f,
                    panelRect.width - 140f,
                    42f),
                "화면의 키를 왼쪽부터 직접 입력하세요. 오타는 복구 실패와 경고음을 발생시킵니다.",
                consoleStyle);
        }

        private static int GetServerLogKeyToken(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return 0;
            }

            return currentEvent.keyCode switch
            {
                KeyCode.Q => 1,
                KeyCode.W => 2,
                KeyCode.E => 3,
                KeyCode.R => 4,
                KeyCode.T => 5,
                _ => 0
            };
        }

        private void DrawStatusBanner()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = _statusColor;
            var horizontalOffset = 0f;
            if (_resultEffect == MissionResultEffect.Failure)
            {
                var elapsed = Time.unscaledTime - _resultEffectStartedAt;
                var remaining = Mathf.Clamp01(
                    1f - elapsed / ResultEffectDurationSeconds);
                horizontalOffset = Mathf.Sin(elapsed * 52f) * 9f * remaining;
            }

            GUI.Box(
                new Rect(
                    (Screen.width - 480f) * 0.5f + horizontalOffset,
                    28f,
                    480f,
                    52f),
                _statusMessage,
                style);
            GUI.backgroundColor = previousColor;
        }

        private bool IsActiveInteractorThreatened()
        {
            if (_activeInteractorNetworkObject != null &&
                _activeInteractorNetworkObject.IsSpawned)
            {
                var clientId =
                    _activeInteractorNetworkObject.OwnerClientId;
                foreach (var authority in
                         NetworkMonsterAuthority.ActiveAuthorities)
                {
                    if (authority != null &&
                        authority.IsThreateningClient(clientId))
                    {
                        return true;
                    }
                }
            }

            if (_activeInteractorTarget == null)
            {
                return false;
            }

            foreach (var brain in MonsterBrain.ActiveBrains)
            {
                if (brain != null && brain.isActiveAndEnabled &&
                    brain.State is MonsterState.Chase or MonsterState.Bite &&
                    brain.Senses?.Target == _activeInteractorTarget)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawDangerWarning()
        {
            var pulse = Mathf.Sin(
                Time.unscaledTime * DangerPulseSpeed) * 0.5f + 0.5f;
            var dangerColor = new Color(
                0.82f,
                0.02f,
                0.03f,
                (0.38f + pulse * 0.42f) *
                LocalGameSettings.VignetteIntensity);
            DrawScreenEdgeFrame(dangerColor, DangerBorderThickness);

            var warningStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(18),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = dangerColor;
            GUI.Box(
                new Rect((Screen.width - 360f) * 0.5f, 92f, 360f, 42f),
                "위험: 괴물이 접근 중입니다",
                warningStyle);
            GUI.backgroundColor = previousColor;
        }

        private void DrawResultEffect()
        {
            if (_resultEffect == MissionResultEffect.None)
            {
                return;
            }

            var elapsed = Time.unscaledTime - _resultEffectStartedAt;
            var remaining = Mathf.Clamp01(
                1f - elapsed / ResultEffectDurationSeconds);
            if (remaining <= 0f)
            {
                _resultEffect = MissionResultEffect.None;
                return;
            }

            var role = _resultEffect == MissionResultEffect.Success
                ? SemanticUiColor.Success
                : SemanticUiColor.Danger;
            var effectAlpha = _resultEffect == MissionResultEffect.Success
                ? remaining * 0.75f
                : remaining *
                  (0.35f + Mathf.Abs(Mathf.Sin(elapsed * 18f)) * 0.5f);
            var color = LocalGameSettings.GetSemanticColor(
                role,
                effectAlpha * LocalGameSettings.FlashIntensity);
            DrawScreenEdgeFrame(
                color,
                12f + remaining * 18f);
        }

        private static void DrawScreenEdgeFrame(
            Color color,
            float thickness)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(new Rect(0f, 0f, Screen.width, thickness), string.Empty);
            GUI.Box(
                new Rect(
                    0f,
                    Screen.height - thickness,
                    Screen.width,
                    thickness),
                string.Empty);
            GUI.Box(new Rect(0f, 0f, thickness, Screen.height), string.Empty);
            GUI.Box(
                new Rect(
                    Screen.width - thickness,
                    0f,
                    thickness,
                    Screen.height),
                string.Empty);
            GUI.backgroundColor = previousColor;
        }

        private void BeginResultEffect(MissionResultEffect effect)
        {
            _resultEffect = effect;
            _resultEffectStartedAt = Time.unscaledTime;
        }

        private static Rect GetCctvTargetRect(
            Rect panelRect,
            int slotIndex)
        {
            return new Rect(
                panelRect.x + panelRect.width - 235f,
                panelRect.y + 135f + slotIndex * 52f,
                160f,
                40f);
        }

        private static Rect GetCctvSourceRect(
            Rect panelRect,
            int channelIndex)
        {
            return new Rect(
                panelRect.x + 75f,
                panelRect.y + 135f + channelIndex * 52f,
                160f,
                40f);
        }

        private int FindCctvTargetSlotIndex(
            int channelId,
            int channelCount)
        {
            for (var slotIndex = 0;
                 slotIndex < channelCount;
                 slotIndex++)
            {
                if (_station.GetCctvTargetChannelAtSlot(slotIndex) ==
                    channelId)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static void DrawCable(
            Vector2 start,
            Vector2 end,
            Color color,
            float thickness)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                start);
            GUI.DrawTexture(
                new Rect(
                    start.x,
                    start.y - thickness * 0.5f,
                    length,
                    thickness),
                Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static Rect GetSampleBinRect(
            Rect panelRect,
            int categoryIndex)
        {
            return new Rect(
                panelRect.x + 380f,
                panelRect.y + 153f + categoryIndex * 56f,
                225f,
                48f);
        }

        private void UpdateDragPosition()
        {
            if (Event.current.type is EventType.MouseDrag or EventType.MouseUp)
            {
                _dragPosition = Event.current.mousePosition;
            }
        }

        private void ResetInteractionState()
        {
            _draggedFuseId = 0;
            _selectedCctvChannelId = 0;
            _dragPosition = Vector2.zero;
            _isDraggingBattery = false;
            _draggedPressureValveIndex = -1;
            _pressureValvePreview[0] = 0f;
            _pressureValvePreview[1] = 0f;
            _isDraggingPressureLock = false;
            _pressureLockDragStartY = 0f;
            _pressureLockNormalized = 0f;
            _nextPressureInputSendTime = 0f;
            _activeInteractorTarget = null;
            _activeInteractorNetworkObject = null;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _station == null)
            {
                return;
            }

            _station.MissionStarted += HandleMissionStarted;
            _station.ProgressChanged += HandleProgressChanged;
            _station.MissionFailed += HandleMissionFailed;
            _station.MissionCancelled += HandleMissionCancelled;
            _station.MissionCompleted += HandleMissionCompleted;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _station == null)
            {
                return;
            }

            _station.MissionStarted -= HandleMissionStarted;
            _station.ProgressChanged -= HandleProgressChanged;
            _station.MissionFailed -= HandleMissionFailed;
            _station.MissionCancelled -= HandleMissionCancelled;
            _station.MissionCompleted -= HandleMissionCompleted;
            _isSubscribed = false;
        }

        private void HandleMissionStarted(FuseStationPrototype station)
        {
            _statusMessage = string.Empty;
            ResetInteractionState();
            if (station.ActiveInteractor != null)
            {
                _activeInteractorTarget =
                    station.ActiveInteractor.GetComponent<MonsterTarget>();
                _activeInteractorNetworkObject =
                    station.ActiveInteractor.GetComponent<NetworkObject>();
            }

            _isOpen = true;
        }

        private void HandleProgressChanged(FuseStationPrototype station)
        {
            if (station.Kind != MissionPrototypeKind.BatteryTransport)
            {
                return;
            }

            _isDraggingBattery = false;
            _isOpen = !station.IsBatteryCarrying ||
                      station.IsBatteryInsertionOpen;
            if (station.IsBatteryCarrying &&
                !station.IsBatteryInsertionOpen)
            {
                ShowStatus(
                    "배터리를 수신 단말기까지 운반하세요.",
                    new Color(0.72f, 0.48f, 0.08f));
            }
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedFuseId,
            int expectedFuseId)
        {
            _isOpen = false;
            ResetInteractionState();
            BeginResultEffect(MissionResultEffect.Failure);
            ShowStatus(
                GetFailureMessage(submittedFuseId, expectedFuseId),
                new Color(0.75f, 0.12f, 0.15f));
        }

        private void HandleMissionCancelled(FuseStationPrototype station)
        {
            _isOpen = false;
            ResetInteractionState();
            ShowStatus(
                "미션이 취소되었습니다.",
                new Color(0.35f, 0.42f, 0.48f));
        }

        private void HandleMissionCompleted(FuseStationPrototype station)
        {
            _isOpen = false;
            ResetInteractionState();
            BeginResultEffect(MissionResultEffect.Success);
            ShowStatus(
                GetSuccessMessage(),
                new Color(0.12f, 0.60f, 0.28f));
        }

        private string GetMissionTitle()
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence => "퓨즈 순서 맞추기",
                MissionPrototypeKind.BreakerSequence => "차단기 기동",
                MissionPrototypeKind.CctvReboot => "CCTV 재부팅",
                MissionPrototypeKind.SampleSorting => "시료 분류",
                MissionPrototypeKind.BatteryTransport =>
                    _station.IsBatteryInsertionOpen
                        ? "비상 배터리 장착"
                        : "비상 배터리 분리",
                MissionPrototypeKind.PressureValves => "압력 밸브 안정화",
                MissionPrototypeKind.SecurityCircuit => "출입문 보안 회로",
                MissionPrototypeKind.AntennaAlignment => "구조 안테나 조율",
                MissionPrototypeKind.ServerLogRecovery => "손상 서버 로그 복구",
                _ => "시설 미션"
            };
        }

        private string GetMissionInstruction()
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    "왼쪽 퓨즈를 잡아 오른쪽의 현재 슬롯으로 드래그하세요. 잘못 꽂으면 실패합니다.",
                MissionPrototypeKind.BreakerSequence =>
                    "전류 표시가 초록색 구간에 들어오는 순간 버튼을 클릭하거나 Space를 누르세요.",
                MissionPrototypeKind.CctvReboot =>
                    "왼쪽 신호 노드를 클릭한 뒤 같은 문자·색상의 오른쪽 포트를 클릭하세요.",
                MissionPrototypeKind.SampleSorting =>
                    "시료를 클릭해 분석한 뒤 라벨 문자와 같은 보관함을 클릭하세요.",
                MissionPrototypeKind.BatteryTransport =>
                    _station.IsBatteryInsertionOpen
                        ? "운반한 배터리를 잡아 전원 소켓 끝까지 밀어 넣으세요."
                        : "거치대 배터리를 잡아 분리 레일 끝까지 당기세요.",
                MissionPrototypeKind.PressureValves =>
                    "두 밸브를 원형으로 돌려 압력을 초록 구간에 유지한 뒤 잠금 레버를 내리세요.",
                MissionPrototypeKind.SecurityCircuit =>
                    "회로 모듈을 클릭해 목표 방향으로 돌린 뒤 Space로 전원을 인가하세요.",
                MissionPrototypeKind.AntennaAlignment =>
                    "WASD 또는 방향키로 신호점을 목표 위치에 맞추고 Enter로 고정하세요.",
                MissionPrototypeKind.ServerLogRecovery =>
                    "화면에 표시된 Q·W·E·R·T 키를 왼쪽부터 정확히 입력하세요.",
                _ => "표시된 순서대로 항목을 선택하세요."
            };
        }

        private string GetItemSectionTitle()
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence => "사용 가능한 퓨즈",
                MissionPrototypeKind.BreakerSequence => "차단기 패널",
                MissionPrototypeKind.CctvReboot => "CCTV 채널",
                MissionPrototypeKind.SampleSorting => "분류 대기 시료",
                _ => "사용 가능한 항목"
            };
        }

        private string GetTargetSectionTitle()
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence => "목표 슬롯 순서",
                MissionPrototypeKind.BreakerSequence => "기동 순서",
                MissionPrototypeKind.CctvReboot => "채널 복구 순서",
                MissionPrototypeKind.SampleSorting => "시료 분석 순서",
                _ => "목표 순서"
            };
        }

        private string GetItemLabel(int itemId)
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    $"{itemId}\n{FuseColorNames[itemId - 1]}",
                MissionPrototypeKind.BreakerSequence =>
                    $"차단기\n{itemId}",
                MissionPrototypeKind.CctvReboot =>
                    $"채널\n{(char)('A' + itemId - 1)}",
                MissionPrototypeKind.SampleSorting =>
                    $"시료 {itemId}\n{FuseColorNames[itemId - 1]}",
                _ => itemId.ToString()
            };
        }

        private string GetTargetLabel(int itemId)
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    $"{itemId}번 ({FuseColorNames[itemId - 1]})",
                MissionPrototypeKind.BreakerSequence =>
                    $"{itemId}번 차단기",
                MissionPrototypeKind.CctvReboot =>
                    $"{(char)('A' + itemId - 1)} 채널",
                MissionPrototypeKind.SampleSorting =>
                    $"{itemId}번 {FuseColorNames[itemId - 1]} 시료",
                _ => $"{itemId}번"
            };
        }

        private string GetSuccessMessage()
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    "성공: 전력 퓨즈를 복구했습니다.",
                MissionPrototypeKind.BreakerSequence =>
                    "성공: 차단기를 기동했습니다.",
                MissionPrototypeKind.CctvReboot =>
                    "성공: CCTV를 재부팅했습니다.",
                MissionPrototypeKind.SampleSorting =>
                    "성공: 시료 분류를 완료했습니다.",
                MissionPrototypeKind.BatteryTransport =>
                    "성공: 비상 배터리를 수신 단말기에 장착했습니다.",
                MissionPrototypeKind.PressureValves =>
                    "성공: 두 밸브의 압력을 안정화했습니다.",
                MissionPrototypeKind.SecurityCircuit =>
                    "성공: 출입문 보안 회로에 전원을 복구했습니다.",
                MissionPrototypeKind.AntennaAlignment =>
                    "성공: 구조 신호의 방향과 주파수를 고정했습니다.",
                MissionPrototypeKind.ServerLogRecovery =>
                    "성공: 손상된 서버 로그를 복구했습니다.",
                _ => "성공: 미션을 완료했습니다."
            };
        }

        private string GetFailureMessage(
            int submittedValue,
            int expectedValue)
        {
            return _station.Kind switch
            {
                MissionPrototypeKind.FuseSequence =>
                    $"실패: {submittedValue}번 퓨즈가 지금 순서가 아닙니다.",
                MissionPrototypeKind.BreakerSequence =>
                    "실패: 안전 구간 밖에서 차단기를 올려 스파크가 발생했습니다.",
                MissionPrototypeKind.SampleSorting =>
                    $"실패: 선택한 시료는 {SampleBinNames[Mathf.Max(0, expectedValue - 1)]} 대상입니다.",
                MissionPrototypeKind.BatteryTransport =>
                    "실패: 운반 중 배터리를 떨어뜨려 충격음이 발생했습니다.",
                MissionPrototypeKind.PressureValves =>
                    "실패: 압력이 안정되기 전에 잠금 레버를 작동했습니다.",
                MissionPrototypeKind.SecurityCircuit =>
                    $"실패: 회로가 {expectedValue - submittedValue}개 모듈에서 끊어졌습니다.",
                MissionPrototypeKind.AntennaAlignment =>
                    "실패: 방향 또는 주파수가 맞지 않아 구조 신호가 손실됐습니다.",
                MissionPrototypeKind.ServerLogRecovery =>
                    $"실패: 로그 키 [{ServerLogKeyNames[Mathf.Clamp(expectedValue - 1, 0, ServerLogKeyNames.Length - 1)]}]가 필요합니다.",
                _ => "실패: 장비 조작을 처음부터 다시 진행하세요."
            };
        }

        private void ShowStatus(string message, Color color)
        {
            _statusMessage = message;
            _statusColor = color;
            _statusVisibleUntil = Time.unscaledTime + StatusMessageDurationSeconds;
        }
    }
}
