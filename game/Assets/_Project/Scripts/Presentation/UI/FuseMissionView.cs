using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class FuseMissionView : MonoBehaviour
    {
        private const float PanelWidth = 680f;
        private const float PanelHeight = 410f;
        private const float StatusMessageDurationSeconds = 2.5f;

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

        [SerializeField] private FuseStationPrototype _station;

        private bool _isOpen;
        private bool _isSubscribed;
        private string _statusMessage = string.Empty;
        private Color _statusColor = Color.white;
        private float _statusVisibleUntil;

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
        }

        private void OnGUI()
        {
            if (_isOpen && _station != null)
            {
                DrawMissionPanel();
            }

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
            GUI.Box(panelRect, string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(panelRect.x + 20f, panelRect.y + 18f, panelRect.width - 40f, 42f), "퓨즈 순서 맞추기", titleStyle);

            var instructionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = new Color(0.84f, 0.90f, 0.94f) }
            };
            GUI.Label(
                new Rect(panelRect.x + 25f, panelRect.y + 62f, panelRect.width - 50f, 32f),
                "오른쪽 슬롯의 번호 순서대로 왼쪽 퓨즈를 클릭하세요. 잘못 누르면 즉시 실패합니다.",
                instructionStyle);

            DrawFuseButtons(panelRect);
            DrawTargetSlots(panelRect);

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
            GUI.Label(new Rect(panelRect.x + 35f, panelRect.y + 112f, 250f, 30f), "사용 가능한 퓨즈", sectionStyle);

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
                var previousEnabled = GUI.enabled;
                GUI.backgroundColor = FuseColors[index];
                GUI.enabled = !_station.IsFuseInserted(fuseId);
                var label = _station.IsFuseInserted(fuseId)
                    ? $"{fuseId}\n삽입됨"
                    : $"{fuseId}\n{FuseColorNames[index]}";

                if (GUI.Button(rect, label))
                {
                    GUI.backgroundColor = previousColor;
                    GUI.enabled = previousEnabled;
                    _station.SubmitFuse(fuseId);
                    return;
                }

                GUI.backgroundColor = previousColor;
                GUI.enabled = previousEnabled;
            }
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
            GUI.Label(new Rect(panelRect.x + 340f, panelRect.y + 112f, 300f, 30f), "목표 슬롯 순서", sectionStyle);

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
                var label = $"{prefix} 슬롯 {index + 1}: {fuseId}번 ({FuseColorNames[fuseId - 1]})";
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = FuseColors[fuseId - 1];
                GUI.Box(rect, label, slotStyle);
                GUI.backgroundColor = previousColor;
            }
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
            GUI.Box(
                new Rect((Screen.width - 480f) * 0.5f, 28f, 480f, 52f),
                _statusMessage,
                style);
            GUI.backgroundColor = previousColor;
        }

        private void Subscribe()
        {
            if (_isSubscribed || _station == null)
            {
                return;
            }

            _station.MissionStarted += HandleMissionStarted;
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
            _station.MissionFailed -= HandleMissionFailed;
            _station.MissionCancelled -= HandleMissionCancelled;
            _station.MissionCompleted -= HandleMissionCompleted;
            _isSubscribed = false;
        }

        private void HandleMissionStarted(FuseStationPrototype station)
        {
            _statusMessage = string.Empty;
            _isOpen = true;
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedFuseId,
            int expectedFuseId)
        {
            _isOpen = false;
            ShowStatus($"실패: {submittedFuseId}번 퓨즈는 지금 순서가 아닙니다.", new Color(0.75f, 0.12f, 0.15f));
        }

        private void HandleMissionCancelled(FuseStationPrototype station)
        {
            _isOpen = false;
            ShowStatus("퓨즈 미션이 취소되었습니다.", new Color(0.35f, 0.42f, 0.48f));
        }

        private void HandleMissionCompleted(FuseStationPrototype station)
        {
            _isOpen = false;
            ShowStatus("성공: 전력 퓨즈를 복구했습니다.", new Color(0.12f, 0.60f, 0.28f));
        }

        private void ShowStatus(string message, Color color)
        {
            _statusMessage = message;
            _statusColor = color;
            _statusVisibleUntil = Time.unscaledTime + StatusMessageDurationSeconds;
        }
    }
}
