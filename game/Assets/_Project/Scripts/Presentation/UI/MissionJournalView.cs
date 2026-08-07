using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// `Tab`으로 여닫는 미션 목록과 전자지도다(GDD §7.2).
    /// 실제 연구소 배치를 그린 정적 지도 위에 본인 위치만 표시한다.
    /// 개인 미션은 소유자에게만 복제되므로 남의 목록은 볼 수 없다.
    /// </summary>
    public sealed class MissionJournalView : MonoBehaviour
    {
        [SerializeField] private MapRoomMarker[] _roomMarkers =
            System.Array.Empty<MapRoomMarker>();
        [SerializeField] private MapRoomMarker[] _exitMarkers =
            System.Array.Empty<MapRoomMarker>();

        private const string MapTextureResourcePath = "UI/T_LaboratoryMap";

        // 생성된 정적 지도에서 각 방 중심을 직접 보정한 UV다. 방 배치와
        // 복도 구조는 현재 10_Laboratory 씬을 기준으로 한다.
        private static readonly MapProjectionAnchor[] MapProjectionAnchors =
        {
            new("백신실 A", new Vector2(-34f, 22f), new Vector2(0.082f, 0.174f)),
            new("실험실 A", new Vector2(-17f, 24f), new Vector2(0.288f, 0.174f)),
            new("격리실 A", new Vector2(-2f, 24f), new Vector2(0.485f, 0.169f)),
            new("액체 보관실", new Vector2(-36f, 1f), new Vector2(0.293f, 0.475f)),
            new("중앙 보안 광장", new Vector2(-3f, 3f), new Vector2(0.502f, 0.551f)),
            new("전력 복구실", new Vector2(14f, 7f), new Vector2(0.903f, 0.570f)),
            new("입원실", new Vector2(14f, 23f), new Vector2(0.672f, 0.169f)),
            new("실험실 B", new Vector2(-18f, -14f), new Vector2(0.455f, 0.865f)),
            new("격리실 B", new Vector2(1f, -17f), new Vector2(0.686f, 0.837f)),
            new("백신실 B", new Vector2(31f, 23f), new Vector2(0.883f, 0.179f))
        };

        private static readonly Vector3Int[] MapProjectionTriangles =
        {
            new(0, 1, 3), new(1, 4, 3),
            new(1, 2, 4), new(2, 6, 4),
            new(6, 5, 4), new(6, 9, 5),
            new(3, 4, 7), new(4, 8, 7),
            new(4, 5, 8), new(5, 9, 8)
        };

        private PlayerInputReader _input;
        private NetworkRoundState _roundState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _compactTitleStyle;
        private GUIStyle _compactMissionStyle;
        private GUIStyle _compactCompletedStyle;
        private GUIStyle _mapMarkerStyle;
        private Texture2D _mapTexture;
        private Texture2D _mapDotTexture;
        private Vector2 _scroll;
        private bool _isOpen;
        private bool _isCompactListCollapsed;
        private int _lastToggleFrame = -1;

        public bool IsOpen => _isOpen;

        public void Configure(MapRoomMarker[] roomMarkers)
        {
            _roomMarkers = roomMarkers ?? System.Array.Empty<MapRoomMarker>();
        }

        public void Configure(
            MapRoomMarker[] roomMarkers,
            MapRoomMarker[] exitMarkers)
        {
            _roomMarkers = roomMarkers ?? System.Array.Empty<MapRoomMarker>();
            _exitMarkers = exitMarkers ?? System.Array.Empty<MapRoomMarker>();
        }

        /// <summary>
        /// 소유자 입력만 연결한다. 네트워크 플레이어가 생성된 뒤 어댑터가 호출한다.
        /// </summary>
        public void BindInput(PlayerInputReader input)
        {
            UnbindInput();
            _input = input;
            if (_input != null)
            {
                _input.JournalPressed += ToggleOpen;
            }
        }

        public void UnbindInput()
        {
            if (_input != null)
            {
                _input.JournalPressed -= ToggleOpen;
            }

            _input = null;
        }

        private void OnEnable()
        {
            NetworkRoundState.CurrentChanged += BindRound;
            _mapTexture = Resources.Load<Texture2D>(MapTextureResourcePath);
            _mapDotTexture = CreateDotTexture(32);
            BindRound();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            UnbindInput();
            _roundState = null;
            _isOpen = false;
            if (_mapDotTexture != null)
            {
                Destroy(_mapDotTexture);
                _mapDotTexture = null;
            }
        }

        private void BindRound()
        {
            _roundState = NetworkRoundState.Current;
        }

        private void ToggleOpen()
        {
            if (_lastToggleFrame == Time.frameCount)
            {
                return;
            }

            _lastToggleFrame = Time.frameCount;
            _isOpen = !_isOpen;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!_isOpen)
            {
                DrawCompactMissionHud();
                return;
            }

            GUI.depth = -8000;
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.008f, 0.018f, 0.025f, 0.94f));

            var safeArea = Screen.safeArea;
            var rect = new Rect(
                safeArea.x + 28f,
                safeArea.y + 28f,
                safeArea.width - 56f,
                safeArea.height - 56f);
            DrawSolidRect(rect, new Color(0.035f, 0.055f, 0.065f, 0.99f));
            DrawRectOutline(
                rect,
                new Color(0.2f, 0.72f, 0.8f, 0.9f),
                3f);

            GUI.Label(
                new Rect(rect.x + 26f, rect.y + 16f, rect.width - 180f, 40f),
                "RX-9 연구소 작전 지도",
                _titleStyle);
            if (GUI.Button(
                    new Rect(rect.xMax - 150f, rect.y + 14f, 124f, 36f),
                    "닫기  [Tab]"))
            {
                _isOpen = false;
                return;
            }

            var contentTop = rect.y + 64f;
            var contentHeight = rect.height - 88f;
            var missionWidth = Mathf.Clamp(
                rect.width * 0.32f,
                280f,
                390f);
            var missionRect = new Rect(
                rect.x + 22f,
                contentTop,
                missionWidth,
                contentHeight);
            var mapRect = new Rect(
                missionRect.xMax + 18f,
                contentTop,
                rect.xMax - missionRect.xMax - 40f,
                contentHeight);

            GUI.Box(missionRect, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    missionRect.x + 16f,
                    missionRect.y + 14f,
                    missionRect.width - 32f,
                    missionRect.height - 28f));
            var displayedPlayer = GetDisplayedPlayerObject();
            GUILayout.Label(
                IsVillain(displayedPlayer)
                    ? "빌런 전용 임무"
                    : "내 임무",
                _titleStyle);
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawMissionList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.Box(mapRect, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    mapRect.x + 18f,
                    mapRect.y + 14f,
                    mapRect.width - 36f,
                    mapRect.height - 28f));
            DrawElectronicMap(mapRect.height - 112f);
            GUILayout.EndArea();
        }

        private void DrawMissionList()
        {
            var playerObject = GetDisplayedPlayerObject();
            if (IsVillain(playerObject))
            {
                DrawVillainMissionList();
                return;
            }

            if (playerObject == null ||
                !playerObject.TryGetComponent<NetworkPlayerMissionJournal>(
                    out var journal))
            {
                GUILayout.Label("배정된 미션이 없습니다.", _bodyStyle);
                DrawRecoveryMissionList();
                return;
            }

            if (journal.AssignedCount <= 0)
            {
                GUILayout.Label(
                    "배정된 개인 미션이 없습니다.",
                    _bodyStyle);
            }
            else
            {
                GUILayout.Label(
                    $"내 미션 {journal.CompletedCount}/{journal.AssignedCount}",
                    _bodyStyle);
                for (var index = 0; index < journal.AssignedCount; index++)
                {
                    var missionId = journal.GetAssignedMissionId(index);
                    var isCompleted = journal.IsCompleted(missionId);
                    GUILayout.Label(
                        $"  {index + 1}. " +
                        $"{(isCompleted ? "[완료]" : "[진행]")} " +
                        $"{DescribeMission(missionId)}",
                        _bodyStyle);
                }
            }

            DrawRecoveryMissionList();
        }

        /// <summary>
        /// 덕몽어스 계열처럼 플레이 중에도 개인 미션을 좌측에 계속 보여준다.
        /// 전체 지도는 Tab으로 열지만, 배정된 일과 완료 여부는 Tab을 누르지
        /// 않아도 확인할 수 있어야 한다(ui-ux-design §6.2).
        /// </summary>
        private void DrawCompactMissionHud()
        {
            var playerObject = GetDisplayedPlayerObject();
            if (playerObject == null ||
                (_roundState != null &&
                 _roundState.Phase == RoundPhase.RoundResult))
            {
                return;
            }

            if (IsVillain(playerObject))
            {
                DrawCompactVillainMissionHud();
                return;
            }

            if (
                !playerObject.TryGetComponent<NetworkPlayerMissionJournal>(
                    out var journal) ||
                journal.AssignedCount <= 0)
            {
                return;
            }

            GUI.depth = -120;
            var safeArea = Screen.safeArea;
            var width = Mathf.Min(390f, safeArea.width - 36f);
            var lineHeight = 29f;
            var expandedHeight = 76f + journal.AssignedCount * lineHeight;
            var height = _isCompactListCollapsed ? 48f : expandedHeight;
            var rect = new Rect(
                safeArea.x + 18f,
                Mathf.Max(safeArea.y + 186f, 186f),
                width,
                height);

            DrawSolidRect(rect, new Color(0.018f, 0.045f, 0.052f, 0.92f));
            DrawRectOutline(
                rect,
                new Color(0.16f, 0.72f, 0.76f, 0.9f),
                2f);
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 8f, rect.width - 104f, 30f),
                $"내 미션  {journal.CompletedCount}/{journal.AssignedCount}",
                _compactTitleStyle);

            if (GUI.Button(
                    new Rect(rect.xMax - 86f, rect.y + 8f, 72f, 28f),
                    _isCompactListCollapsed ? "펼치기" : "접기"))
            {
                _isCompactListCollapsed = !_isCompactListCollapsed;
            }

            if (_isCompactListCollapsed)
            {
                return;
            }

            var y = rect.y + 42f;
            for (var index = 0; index < journal.AssignedCount; index++)
            {
                var missionId = journal.GetAssignedMissionId(index);
                var isCompleted = journal.IsCompleted(missionId);
                var markerRect = new Rect(rect.x + 14f, y + 7f, 12f, 12f);
                DrawSolidRect(
                    markerRect,
                    isCompleted
                        ? new Color(0.2f, 0.75f, 0.56f, 1f)
                        : new Color(1f, 0.76f, 0.18f, 1f));
                DrawRectOutline(markerRect, Color.white, 1f);
                GUI.Label(
                    new Rect(
                        markerRect.xMax + 8f,
                        y,
                        rect.width - 48f,
                        lineHeight),
                    $"{index + 1}. {DescribeMission(missionId)}" +
                    (isCompleted ? "  [완료]" : string.Empty),
                    isCompleted
                        ? _compactCompletedStyle
                        : _compactMissionStyle);
                y += lineHeight;
            }

            GUI.Label(
                new Rect(rect.x + 14f, rect.yMax - 27f, rect.width - 28f, 22f),
                "[Tab] 전체 미션 목록과 위치 지도",
                _bodyStyle);
        }

        private void DrawVillainMissionList()
        {
            var authority = NetworkVillainMissionStackAuthority.Current;
            if (authority == null || !authority.IsSpawned ||
                authority.LocalAssignedMissionCount == 0)
            {
                GUILayout.Label(
                    "빌런 작전 정보를 불러오는 중입니다.",
                    _bodyStyle);
                return;
            }

            GUILayout.Label(
                $"빌런 전용 미션 {authority.LocalClearCount}/" +
                $"{authority.LocalAssignedMissionCount}",
                _bodyStyle);
            var displayIndex = 1;
            var definitions = VillainMissionCatalog.All;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (!authority.LocalIsMissionAssigned(definition.Kind))
                {
                    continue;
                }

                var isCompleted =
                    authority.LocalIsMissionCompleted(definition.Kind);
                GUILayout.Label(
                    $"  {displayIndex}. " +
                    $"{(isCompleted ? "[완료]" : "[진행]")} " +
                    $"{definition.DisplayName} — {definition.RoomDisplayName}",
                    _bodyStyle);
                displayIndex++;
            }

            GUILayout.Space(10f);
            GUILayout.Label(
                "스피커로 원숭이 유도 — 우측 리모컨",
                _bodyStyle);
            GUILayout.Label(
                "전용 미션은 시민 작업처럼 보이지만 프로젝트 진행률을 올리지 않습니다.",
                _bodyStyle);
        }

        private void DrawCompactVillainMissionHud()
        {
            var authority = NetworkVillainMissionStackAuthority.Current;
            var objectiveCount = authority != null &&
                                 authority.LocalAssignedMissionCount > 0
                ? authority.LocalAssignedMissionCount
                : VillainMissionAssignmentService.AssignedMissionCount;
            var safeArea = Screen.safeArea;
            var width = Mathf.Min(390f, safeArea.width - 36f);
            var lineHeight = 29f;
            var expandedHeight = 76f + objectiveCount * lineHeight;
            var height = _isCompactListCollapsed ? 48f : expandedHeight;
            var rect = new Rect(
                safeArea.x + 18f,
                Mathf.Max(safeArea.y + 186f, 186f),
                width,
                height);
            var completed = authority?.LocalClearCount ?? 0;

            DrawSolidRect(rect, new Color(0.12f, 0.025f, 0.055f, 0.94f));
            DrawRectOutline(
                rect,
                new Color(0.95f, 0.25f, 0.48f, 0.95f),
                2f);
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 8f, rect.width - 104f, 30f),
                $"빌런 전용 임무  {completed}/{objectiveCount}",
                _compactTitleStyle);

            if (GUI.Button(
                    new Rect(rect.xMax - 86f, rect.y + 8f, 72f, 28f),
                    _isCompactListCollapsed ? "펼치기" : "접기"))
            {
                _isCompactListCollapsed = !_isCompactListCollapsed;
            }

            if (_isCompactListCollapsed)
            {
                return;
            }

            var y = rect.y + 42f;
            var displayIndex = 1;
            var definitions = VillainMissionCatalog.All;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (authority == null ||
                    !authority.LocalIsMissionAssigned(definition.Kind))
                {
                    continue;
                }

                y = DrawVillainObjectiveLine(
                    rect,
                    y,
                    lineHeight,
                    $"{displayIndex}. {definition.DisplayName} — " +
                    definition.RoomDisplayName,
                    authority.LocalIsMissionCompleted(definition.Kind));
                displayIndex++;
            }

            GUI.Label(
                new Rect(rect.x + 14f, rect.yMax - 27f, rect.width - 28f, 22f),
                "[Tab] 빌런 작전 목록과 위치 지도",
                _bodyStyle);
        }

        private float DrawVillainObjectiveLine(
            Rect panelRect,
            float y,
            float lineHeight,
            string label,
            bool isCompleted)
        {
            var markerRect = new Rect(panelRect.x + 14f, y + 7f, 12f, 12f);
            DrawSolidRect(
                markerRect,
                isCompleted
                    ? new Color(0.2f, 0.75f, 0.56f, 1f)
                    : new Color(0.95f, 0.25f, 0.48f, 1f));
            DrawRectOutline(markerRect, Color.white, 1f);
            GUI.Label(
                new Rect(
                    markerRect.xMax + 8f,
                    y,
                    panelRect.width - 48f,
                    lineHeight),
                label + (isCompleted ? "  [완료]" : string.Empty),
                isCompleted ? _compactCompletedStyle : _compactMissionStyle);
            return y + lineHeight;
        }

        private static bool IsVillain(GameObject playerObject)
        {
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkPlayerAvatar>(
                       out var avatar) &&
                   avatar.Role == PlayerRole.Villain;
        }

        private void DrawRecoveryMissionList()
        {
            if (_roundState == null ||
                _roundState.RecoveryMissionCount <= 0)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                $"공용 복구 미션 {_roundState.RecoveryMissionCount}개",
                _titleStyle);
            GUILayout.Label(
                "복귀하지 않은 생존자의 남은 작업입니다.",
                _bodyStyle);
            for (var index = 0;
                 index < _roundState.RecoveryMissionCount;
                 index++)
            {
                var missionId = _roundState.GetRecoveryMissionId(index);
                GUILayout.Label(
                    $"  [복구] {DescribeMission(missionId)}",
                    _bodyStyle);
            }
        }

        /// <summary>
        /// 미션 ID는 스테이션의 NetworkObjectId다. 종류와 위치를 찾아 문구로 만든다.
        /// </summary>
        private string DescribeMission(ulong missionId)
        {
            if (SurvivorMissionCatalog.TryGetDefinition(
                    missionId,
                    out var definition))
            {
                return $"{definition.DisplayName} — " +
                       definition.RoomDisplayName;
            }

            var spawnManager = NetworkManager.Singleton?.SpawnManager;
            if (spawnManager == null ||
                !spawnManager.SpawnedObjects.TryGetValue(
                    missionId,
                    out var stationObject) ||
                !stationObject.TryGetComponent<FuseStationPrototype>(
                    out var station))
            {
                return "알 수 없는 스테이션";
            }

            var kindLabel = station.Kind switch
            {
                MissionPrototypeKind.FuseSequence => "퓨즈 순서",
                MissionPrototypeKind.BreakerSequence => "차단기",
                MissionPrototypeKind.CctvReboot => "CCTV 재부팅",
                MissionPrototypeKind.SampleSorting => "시료 분류",
                MissionPrototypeKind.BatteryTransport => "비상 배터리 운반",
                MissionPrototypeKind.PressureValves => "압력 밸브",
                MissionPrototypeKind.SecurityCircuit => "보안 회로",
                MissionPrototypeKind.AntennaAlignment => "안테나 조율",
                MissionPrototypeKind.ServerLogRecovery => "서버 로그 복구",
                _ => "미션"
            };
            return $"{kindLabel} — {FindNearestRoomName(stationObject.transform.position)}";
        }

        private void DrawElectronicMap(float schematicHeight)
        {
            var milestone = _roundState != null
                ? _roundState.ProjectMilestone
                : ProjectMilestone.None;
            var playerObject = GetDisplayedPlayerObject();
            var currentRoomName = playerObject != null
                ? FindNearestRoomName(playerObject.transform.position)
                : "알 수 없음";

            GUILayout.Label("연구소 지도", _titleStyle);
            GUILayout.Label($"현재 위치: {currentRoomName}", _bodyStyle);

            DrawStaticMap(
                playerObject != null
                    ? (Vector2?)playerObject.transform.position
                    : null,
                schematicHeight);

            if (milestone >= ProjectMilestone.ExitGuidance)
            {
                DrawExitGuidance(playerObject != null
                    ? playerObject
                    : null);
            }
        }

        private GameObject GetDisplayedPlayerObject()
        {
            var networkPlayer =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (networkPlayer != null)
            {
                return networkPlayer.gameObject;
            }

            return _input != null ? _input.gameObject : null;
        }

        private void DrawStaticMap(
            Vector2? playerPosition,
            float schematicHeight)
        {
            var mapRect = GUILayoutUtility.GetRect(
                10f,
                Mathf.Max(285f, schematicHeight),
                GUILayout.ExpandWidth(true));
            DrawSolidRect(mapRect, new Color(0.006f, 0.012f, 0.016f, 1f));
            DrawRectOutline(mapRect, new Color(0.22f, 0.7f, 0.78f, 0.9f), 2f);

            if (_mapTexture == null)
            {
                GUI.Label(mapRect, "지도 이미지가 없습니다.", _bodyStyle);
                return;
            }

            var imageRect = FitAspect(
                mapRect,
                (float)_mapTexture.width / _mapTexture.height);
            GUI.DrawTexture(
                imageRect,
                _mapTexture,
                ScaleMode.ScaleToFit,
                false);
            DrawStaticRoomNames(imageRect);

            if (!playerPosition.HasValue)
            {
                return;
            }

            var playerMapPosition = MapUvToGuiPosition(
                ProjectWorldToMapUv(playerPosition.Value),
                imageRect);
            var playerRect = new Rect(
                playerMapPosition.x - 9f,
                playerMapPosition.y - 9f,
                18f,
                18f);
            var previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(playerRect, _mapDotTexture);
            GUI.color = new Color(1f, 0.78f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(
                playerRect.x + 3f,
                playerRect.y + 3f,
                playerRect.width - 6f,
                playerRect.height - 6f), _mapDotTexture);
            GUI.color = previousColor;
        }

        private void DrawStaticRoomNames(Rect imageRect)
        {
            const float width = 104f;
            const float height = 22f;
            for (var index = 0; index < MapProjectionAnchors.Length; index++)
            {
                var anchor = MapProjectionAnchors[index];
                var position = MapUvToGuiPosition(anchor.MapUv, imageRect);
                var markerRect = new Rect(
                    position.x - width * 0.5f,
                    position.y - height * 0.5f,
                    width,
                    height);
                DrawSolidRect(
                    markerRect,
                    new Color(0.006f, 0.018f, 0.024f, 0.72f));
                GUI.Label(markerRect, anchor.DisplayName, _mapMarkerStyle);
            }
        }

        private static Vector2 ProjectWorldToMapUv(Vector2 worldPosition)
        {
            for (var index = 0; index < MapProjectionTriangles.Length; index++)
            {
                var triangle = MapProjectionTriangles[index];
                if (!TryGetBarycentricWeights(
                        worldPosition,
                        MapProjectionAnchors[triangle.x].WorldPosition,
                        MapProjectionAnchors[triangle.y].WorldPosition,
                        MapProjectionAnchors[triangle.z].WorldPosition,
                        out var weights))
                {
                    continue;
                }

                return MapProjectionAnchors[triangle.x].MapUv * weights.x +
                       MapProjectionAnchors[triangle.y].MapUv * weights.y +
                       MapProjectionAnchors[triangle.z].MapUv * weights.z;
            }

            return ProjectOutsideAnchorHull(worldPosition);
        }

        private static Vector2 ProjectOutsideAnchorHull(Vector2 worldPosition)
        {
            var weightedUv = Vector2.zero;
            var totalWeight = 0f;
            for (var index = 0; index < MapProjectionAnchors.Length; index++)
            {
                var anchor = MapProjectionAnchors[index];
                var squaredDistance =
                    (worldPosition - anchor.WorldPosition).sqrMagnitude;
                if (squaredDistance <= 0.0001f)
                {
                    return anchor.MapUv;
                }

                var weight = 1f / Mathf.Max(4f, squaredDistance);
                weightedUv += anchor.MapUv * weight;
                totalWeight += weight;
            }

            return totalWeight > 0f
                ? weightedUv / totalWeight
                : new Vector2(0.5f, 0.5f);
        }

        private static bool TryGetBarycentricWeights(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            out Vector3 weights)
        {
            var denominator =
                (b.y - c.y) * (a.x - c.x) +
                (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                weights = default;
                return false;
            }

            var first =
                ((b.y - c.y) * (point.x - c.x) +
                 (c.x - b.x) * (point.y - c.y)) / denominator;
            var second =
                ((c.y - a.y) * (point.x - c.x) +
                 (a.x - c.x) * (point.y - c.y)) / denominator;
            var third = 1f - first - second;
            weights = new Vector3(first, second, third);
            const float tolerance = -0.001f;
            return first >= tolerance && second >= tolerance &&
                   third >= tolerance;
        }

        private static Vector2 MapUvToGuiPosition(Vector2 uv, Rect imageRect)
        {
            return new Vector2(
                Mathf.Lerp(imageRect.xMin, imageRect.xMax, uv.x),
                Mathf.Lerp(imageRect.yMin, imageRect.yMax, uv.y));
        }

        private static Rect FitAspect(Rect bounds, float aspect)
        {
            var width = bounds.width;
            var height = width / aspect;
            if (height > bounds.height)
            {
                height = bounds.height;
                width = height * aspect;
            }

            return new Rect(
                bounds.center.x - width * 0.5f,
                bounds.center.y - height * 0.5f,
                width,
                height);
        }

        private static Texture2D CreateDotTexture(int size)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "UI_MapPlayerDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            var center = (size - 1f) * 0.5f;
            var radius = size * 0.48f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(center, center));
                    var alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(radius - distance + 0.8f) * 255f);
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawRectOutline(
            Rect rect,
            Color color,
            float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(
                new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
                color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(
                new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
                color);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawExitGuidance(GameObject playerObject)
        {
            GUILayout.Space(5f);
            GUILayout.Label("탈출 경로", _titleStyle);
            for (var index = 0; index < _exitMarkers.Length; index++)
            {
                var exit = _exitMarkers[index];
                var distance = playerObject != null
                    ? Vector2.Distance(
                        playerObject.transform.position,
                        exit.WorldPosition)
                    : 0f;
                GUILayout.Label(
                    $"  ▶ {exit.DisplayName}" +
                    (playerObject != null ? $"  {distance:0}m" : string.Empty),
                    _bodyStyle);
            }

            var incompleteRooms = CollectIncompleteMissionRooms(playerObject);
            GUILayout.Label("남은 미션 구역", _titleStyle);
            if (incompleteRooms.Count == 0)
            {
                GUILayout.Label("  남은 개인 미션 없음", _bodyStyle);
                return;
            }

            foreach (var roomName in incompleteRooms)
            {
                GUILayout.Label($"  ● {roomName}", _bodyStyle);
            }
        }

        private HashSet<string> CollectIncompleteMissionRooms(
            GameObject playerObject)
        {
            var rooms = new HashSet<string>();
            if (playerObject == null ||
                !playerObject.TryGetComponent<NetworkPlayerMissionJournal>(
                    out var journal))
            {
                return rooms;
            }

            var spawnManager = NetworkManager.Singleton?.SpawnManager;
            for (var index = 0; index < journal.AssignedCount; index++)
            {
                var missionId = journal.GetAssignedMissionId(index);
                if (journal.IsCompleted(missionId) ||
                    spawnManager == null ||
                    !spawnManager.SpawnedObjects.TryGetValue(
                        missionId,
                        out var stationObject))
                {
                    continue;
                }

                rooms.Add(FindNearestRoomName(stationObject.transform.position));
            }

            if (_roundState == null)
            {
                return rooms;
            }

            for (var index = 0;
                 index < _roundState.RecoveryMissionCount;
                 index++)
            {
                var missionId = _roundState.GetRecoveryMissionId(index);
                if (spawnManager == null ||
                    !spawnManager.SpawnedObjects.TryGetValue(
                        missionId,
                        out var stationObject))
                {
                    continue;
                }

                rooms.Add(FindNearestRoomName(stationObject.transform.position));
            }

            return rooms;
        }

        private string FindNearestRoomName(Vector2 worldPosition)
        {
            var nearestName = "복도";
            var nearestSquaredDistance = float.MaxValue;
            for (var index = 0; index < _roomMarkers.Length; index++)
            {
                var marker = _roomMarkers[index];
                var squaredDistance =
                    (marker.WorldPosition - worldPosition).sqrMagnitude;
                if (squaredDistance >= nearestSquaredDistance)
                {
                    continue;
                }

                nearestSquaredDistance = squaredDistance;
                nearestName = marker.DisplayName;
            }

            return nearestName;
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            _compactTitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.96f, 0.86f) }
            };
            _compactMissionStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _compactCompletedStyle ??= new GUIStyle(_compactMissionStyle)
            {
                normal = { textColor = new Color(0.48f, 0.64f, 0.62f) }
            };
            _mapMarkerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.8f, 0.84f) }
            };
        }

        private readonly struct MapProjectionAnchor
        {
            public MapProjectionAnchor(
                string displayName,
                Vector2 worldPosition,
                Vector2 mapUv)
            {
                DisplayName = displayName;
                WorldPosition = worldPosition;
                MapUv = mapUv;
            }

            public string DisplayName { get; }
            public Vector2 WorldPosition { get; }
            public Vector2 MapUv { get; }
        }
    }
}
