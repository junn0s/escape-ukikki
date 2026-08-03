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

        private PlayerInputReader _input;
        private NetworkRoundState _roundState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        public void Configure(MapRoomMarker[] roomMarkers)
        {
            _roomMarkers = roomMarkers ?? System.Array.Empty<MapRoomMarker>();
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
            const float height = 380f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(rect.x + 20f, rect.y + 16f, width - 40f, height - 32f));
            GUILayout.Label("미션 목록과 전자지도  [Tab] 닫기", _titleStyle);
            DrawMissionList();
            GUILayout.Space(10f);
            DrawElectronicMap();
            GUILayout.EndArea();
        }

        private void DrawMissionList()
        {
            var playerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject == null ||
                !playerObject.TryGetComponent<NetworkPlayerMissionJournal>(
                    out var journal) ||
                journal.AssignedCount <= 0)
            {
                GUILayout.Label("배정된 미션이 없습니다.", _bodyStyle);
                return;
            }

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
