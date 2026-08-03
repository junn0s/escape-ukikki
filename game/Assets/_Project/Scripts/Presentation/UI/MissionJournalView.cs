using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// `Tab`으로 여닫는 미션 목록과 전자지도다(GDD §7.2).
    /// 미션 목록은 언제나 볼 수 있지만, 전자지도는 프로젝트 50%
    /// (`SecurityAccess`) 이후에만 열린다(GDD §9.2).
    /// 개인 미션은 소유자에게만 복제되므로 남의 목록은 볼 수 없다.
    /// </summary>
    public sealed class MissionJournalView : MonoBehaviour
    {
        [SerializeField] private MapRoomMarker[] _roomMarkers =
            System.Array.Empty<MapRoomMarker>();
        [SerializeField] private MapRoomMarker[] _exitMarkers =
            System.Array.Empty<MapRoomMarker>();

        private PlayerInputReader _input;
        private NetworkRoundState _roundState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private Vector2 _scroll;
        private bool _isOpen;

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
            BindRound();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            UnbindInput();
            _roundState = null;
            _isOpen = false;
        }

        private void BindRound()
        {
            _roundState = NetworkRoundState.Current;
        }

        private void ToggleOpen()
        {
            _isOpen = !_isOpen;
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                return;
            }

            EnsureStyles();
            const float width = 560f;
            var height = Mathf.Min(640f, Screen.height - 80f);
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(rect.x + 20f, rect.y + 16f, width - 40f, height - 32f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("미션 목록과 전자지도  [Tab] 닫기", _titleStyle);
            DrawMissionList();
            GUILayout.Space(10f);
            DrawElectronicMap();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMissionList()
        {
            var playerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
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
                        $"  {(isCompleted ? "[완료]" : "[진행]")} " +
                        $"{DescribeMission(missionId)}",
                        _bodyStyle);
                }
            }

            DrawRecoveryMissionList();
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

        private void DrawElectronicMap()
        {
            // 전자지도는 프로젝트 50% 이후에 열린다(GDD §9.2).
            var milestone = _roundState != null
                ? _roundState.ProjectMilestone
                : ProjectMilestone.None;
            if (milestone < ProjectMilestone.SecurityAccess)
            {
                GUILayout.Label(
                    "전자지도 — 프로젝트 50% 이후에 활성화됩니다.",
                    _bodyStyle);
                return;
            }

            GUILayout.Label("전자지도", _titleStyle);
            var playerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var currentRoomName = playerObject != null
                ? FindNearestRoomName(playerObject.transform.position)
                : "알 수 없음";
            GUILayout.Label($"현재 위치: {currentRoomName}", _bodyStyle);

            for (var index = 0; index < _roomMarkers.Length; index++)
            {
                var marker = _roomMarkers[index];
                var isCurrent = marker.DisplayName == currentRoomName;
                GUILayout.Label(
                    $"  {(isCurrent ? "▶" : "·")} {marker.DisplayName}",
                    _bodyStyle);
            }

            if (milestone >= ProjectMilestone.ExitGuidance)
            {
                DrawExitGuidance(playerObject != null
                    ? playerObject.gameObject
                    : null);
            }
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
        }
    }
}
