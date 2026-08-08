using System;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.Settings;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    [Serializable]
    public struct RoomPresentationZone
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Vector2 _center;
        [SerializeField] private Vector2 _size;

        public RoomPresentationZone(
            string displayName,
            Vector2 center,
            Vector2 size)
        {
            _displayName = displayName;
            _center = center;
            _size = size;
        }

        public string DisplayName => _displayName;

        public bool Contains(Vector2 worldPosition)
        {
            var halfSize = _size * 0.5f;
            return Mathf.Abs(worldPosition.x - _center.x) <= halfSize.x &&
                   Mathf.Abs(worldPosition.y - _center.y) <= halfSize.y;
        }
    }

    /// <summary>
    /// 기존 게임 규칙을 바꾸지 않고 현재 방, 미션 결과, 추격, 조준과
    /// 상호작용 대상을 하나의 로컬 피드백 계층으로 표현한다.
    /// </summary>
    public sealed class GameplayFeelView : MonoBehaviour
    {
        private const float RoomScanIntervalSeconds = 0.12f;
        private const float EventBannerDurationSeconds = 2.4f;
        private const float DamageFlashDurationSeconds = 0.42f;
        private const float SpeakerAlarmPulseSpeed = 18f;
        private const float ThreatBlendSpeed = 3.2f;
        private const float ReticleRadius = 9f;
        private const float InteractionMarkerRadius = 26f;
        private const float InitialControlHintSeconds = 8f;

        private static Color Cyan => LocalGameSettings.GetSemanticColor(
            SemanticUiColor.Information);
        private static Color Green => LocalGameSettings.GetSemanticColor(
            SemanticUiColor.Success);
        private static Color Orange => LocalGameSettings.GetSemanticColor(
            SemanticUiColor.Warning);
        private static Color Red => LocalGameSettings.GetSemanticColor(
            SemanticUiColor.Danger);

        [SerializeField] private TopDownCamera _camera;
        [SerializeField] private MonsterTarget _localTarget;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private FuseStationPrototype[] _stations =
            Array.Empty<FuseStationPrototype>();
        [SerializeField] private MonsterBrain[] _localMonsters =
            Array.Empty<MonsterBrain>();
        [SerializeField] private RoomPresentationZone[] _rooms =
            Array.Empty<RoomPresentationZone>();

        private NetworkRoundState _roundState;
        private NetworkSpeakerAuthority _speakerAuthority;
        private UnityEngine.Camera _worldCameraComponent;
        private GUIStyle _roomStyle;
        private GUIStyle _eventBannerStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _threatStyle;
        private string _currentRoomName = "연구소 복도";
        private string _eventBanner = string.Empty;
        private Color _eventBannerColor = Color.white;
        private float _eventBannerUntil;
        private float _damageFlashStartedAt;
        private float _damageFlashUntil;
        private float _speakerAlarmStartedAt;
        private float _speakerAlarmUntil;
        private float _nextRoomScanAt;
        private float _threatIntensity;
        private int _currentRoomIndex = -2;
        private ProjectMilestone _lastMilestone;
        private bool _isStationSubscribed;
        private bool _isTargetSubscribed;
        private bool _isControlHintsOpen;
        private float _controlHintsVisibleUntil;

        public int RoomCount => _rooms?.Length ?? 0;
        public int StationCount => _stations?.Length ?? 0;
        public TopDownCamera WorldCamera => _camera;

        public void Configure(
            TopDownCamera worldCamera,
            MonsterTarget localTarget,
            PlayerInteractor interactor,
            PlayerInputReader input,
            FuseStationPrototype[] stations,
            MonsterBrain[] localMonsters,
            RoomPresentationZone[] rooms)
        {
            UnsubscribeStations();
            _camera = worldCamera;
            _worldCameraComponent = worldCamera != null
                ? worldCamera.GetComponent<UnityEngine.Camera>()
                : null;
            _stations = stations ?? Array.Empty<FuseStationPrototype>();
            _localMonsters = localMonsters ?? Array.Empty<MonsterBrain>();
            _rooms = rooms ?? Array.Empty<RoomPresentationZone>();
            BindLocalPlayer(
                localTarget != null ? localTarget.transform : null,
                localTarget,
                interactor,
                input);
            SubscribeStations();
            RefreshRoom(forceRefresh: true);
        }

        public void BindLocalPlayer(
            Transform playerTransform,
            MonsterTarget localTarget,
            PlayerInteractor interactor,
            PlayerInputReader input)
        {
            var hasTargetChanged = _localTarget != localTarget ||
                (playerTransform != null && _camera != null &&
                 _camera.Target != playerTransform);
            if (_localTarget != localTarget)
            {
                UnsubscribeTarget();
                _localTarget = localTarget;
            }

            _interactor = interactor;
            _input = input;
            if (playerTransform != null && _camera != null &&
                _camera.Target != playerTransform)
            {
                _camera.SetTarget(playerTransform, shouldSnap: true);
            }

            SubscribeTarget();
            if (hasTargetChanged)
            {
                _currentRoomIndex = -2;
                RefreshRoom(forceRefresh: true);
            }
        }

        private void OnEnable()
        {
            LocalGameSettings.Changed += HandleSettingsChanged;
            NetworkRoundState.CurrentChanged += BindCurrentRound;
            NetworkSpeakerAuthority.CurrentChanged += BindSpeakerAuthority;
            BindCurrentRound();
            BindSpeakerAuthority();
            SubscribeStations();
            SubscribeTarget();
            _controlHintsVisibleUntil =
                Time.unscaledTime + InitialControlHintSeconds;
        }

        private void OnDisable()
        {
            LocalGameSettings.Changed -= HandleSettingsChanged;
            NetworkRoundState.CurrentChanged -= BindCurrentRound;
            NetworkSpeakerAuthority.CurrentChanged -= BindSpeakerAuthority;
            UnbindRound();
            UnbindSpeakerAuthority();
            UnsubscribeStations();
            UnsubscribeTarget();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRoomScanAt)
            {
                _nextRoomScanAt =
                    Time.unscaledTime + RoomScanIntervalSeconds;
                RefreshRoom(forceRefresh: false);
            }

            var targetThreat = IsLocalPlayerThreatened() ? 1f : 0f;
            var previousThreat = _threatIntensity;
            _threatIntensity = Mathf.MoveTowards(
                _threatIntensity,
                targetThreat,
                ThreatBlendSpeed * Time.unscaledDeltaTime);
            if (previousThreat <= 0.01f && _threatIntensity > 0.01f)
            {
                _camera?.AddTrauma(0.12f);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 미션 중에는 이동·조준이 잠기므로 월드를 보며 읽는 정보는 필요 없다.
            // 그대로 두면 미션 안내문·부품 위에 겹쳐 양쪽 다 읽히지 않는다.
            var isExplorationHudVisible = _roundState == null ||
                _roundState.Phase == RoundPhase.Exploration ||
                _roundState.Phase == RoundPhase.GracePeriod;
            if (!MissionOverlayState.IsOpen && isExplorationHudVisible)
            {
                HandleControlHintInput(Event.current);
                DrawRoomStatus();
                DrawControlHints();
                DrawReticle();
                DrawInteractionMarker();
            }

            // 사건 배너와 위험·피격 표시는 미션 중에도 남긴다.
            // 괴물이 오는 것을 미션 화면 때문에 놓치면 안 된다.
            DrawEventBanner();
            DrawThreatFeedback();
            DrawSpeakerAlarmFeedback();
            DrawDamageFlash();
        }

        private void DrawRoomStatus()
        {
            const float width = 190f;
            var rect = new Rect(
                Screen.width - width - 18f,
                14f,
                width,
                34f);
            DrawSolidRect(rect, new Color(0.02f, 0.06f, 0.09f, 0.76f));
            GUI.Label(rect, _currentRoomName, _roomStyle);
        }

        private void DrawEventBanner()
        {
            if (string.IsNullOrEmpty(_eventBanner) ||
                Time.unscaledTime > _eventBannerUntil)
            {
                return;
            }

            var rect = new Rect(
                (Screen.width - 580f) * 0.5f,
                132f,
                580f,
                52f);
            DrawSolidRect(rect, new Color(0.02f, 0.05f, 0.07f, 0.94f));
            DrawSolidRect(
                new Rect(rect.x, rect.yMax - 4f, rect.width, 4f),
                _eventBannerColor);
            GUI.Label(rect, _eventBanner, _eventBannerStyle);
        }

        private void DrawControlHints()
        {
            if (!_isControlHintsOpen &&
                Time.unscaledTime > _controlHintsVisibleUntil)
            {
                if (GUI.Button(
                        new Rect(
                            (Screen.width - 86f) * 0.5f,
                            Screen.height - 34f,
                            86f,
                            24f),
                        "조작 [H]"))
                {
                    _isControlHintsOpen = true;
                }

                return;
            }

            var content = new GUIContent(
                "[WASD] 이동   [E] 상호작용   [F] 손전등   [Tab] 지도   [R] 해독제   [H] 닫기");

            // 고정 폭이면 텍스트 크기 배율을 올렸을 때 잘려서 항목이 붙어 보인다.
            // 실제 문자열 폭을 재서 맞춘다.
            var width = Mathf.Min(
                _hintStyle.CalcSize(content).x + 24f,
                Screen.width - 36f);
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - 36f,
                width,
                24f);
            DrawSolidRect(rect, new Color(0.01f, 0.04f, 0.06f, 0.74f));
            GUI.Label(rect, content, _hintStyle);
        }

        private void HandleControlHintInput(Event currentEvent)
        {
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.H)
            {
                return;
            }

            _isControlHintsOpen = !_isControlHintsOpen;
            _controlHintsVisibleUntil = 0f;
            currentEvent.Use();
        }

        private void DrawReticle()
        {
            if (_input == null)
            {
                return;
            }

            var pointer = _input.PointerPosition;
            var center = new Vector2(pointer.x, Screen.height - pointer.y);
            if (center.x < 0f || center.x > Screen.width ||
                center.y < 0f || center.y > Screen.height)
            {
                return;
            }

            var color = Color.Lerp(Cyan, Red, _threatIntensity);
            DrawReticleLine(
                new Rect(center.x - 1f, center.y - ReticleRadius - 5f, 2f, 6f),
                color);
            DrawReticleLine(
                new Rect(center.x - 1f, center.y + ReticleRadius - 1f, 2f, 6f),
                color);
            DrawReticleLine(
                new Rect(center.x - ReticleRadius - 5f, center.y - 1f, 6f, 2f),
                color);
            DrawReticleLine(
                new Rect(center.x + ReticleRadius - 1f, center.y - 1f, 6f, 2f),
                color);
            DrawSolidRect(
                new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f),
                color);
        }

        private void DrawInteractionMarker()
        {
            if (_worldCameraComponent == null || _interactor == null ||
                !_interactor.HasTarget ||
                _interactor.CurrentTargetTransform == null)
            {
                return;
            }

            var screenPoint = _worldCameraComponent.WorldToScreenPoint(
                    _interactor.CurrentTargetTransform.position);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            var center = new Vector2(
                screenPoint.x,
                Screen.height - screenPoint.y);
            var pulse = 0.75f +
                        Mathf.Sin(Time.unscaledTime * 5f) * 0.15f;
            var color = new Color(Cyan.r, Cyan.g, Cyan.b, pulse);
            const float cornerLength = 10f;
            var radius = InteractionMarkerRadius + (1f - pulse) * 5f;
            DrawCorner(
                new Vector2(center.x - radius, center.y - radius),
                new Vector2(1f, 1f),
                cornerLength,
                color);
            DrawCorner(
                new Vector2(center.x + radius, center.y - radius),
                new Vector2(-1f, 1f),
                cornerLength,
                color);
            DrawCorner(
                new Vector2(center.x - radius, center.y + radius),
                new Vector2(1f, -1f),
                cornerLength,
                color);
            DrawCorner(
                new Vector2(center.x + radius, center.y + radius),
                new Vector2(-1f, -1f),
                cornerLength,
                color);
        }

        private void DrawThreatFeedback()
        {
            if (_threatIntensity <= 0.001f)
            {
                return;
            }

            var pulse = 0.55f +
                        Mathf.Sin(Time.unscaledTime * 7f) * 0.25f;
            var alpha = _threatIntensity * pulse * 0.42f *
                        LocalGameSettings.VignetteIntensity;
            var edgeColor = new Color(Red.r, Red.g, Red.b, alpha);
            const float thickness = 28f;
            DrawSolidRect(new Rect(0f, 0f, Screen.width, thickness), edgeColor);
            DrawSolidRect(
                new Rect(0f, Screen.height - thickness, Screen.width, thickness),
                edgeColor);
            DrawSolidRect(new Rect(0f, 0f, thickness, Screen.height), edgeColor);
            DrawSolidRect(
                new Rect(Screen.width - thickness, 0f, thickness, Screen.height),
                edgeColor);
            GUI.Label(
                new Rect(Screen.width - 278f, 84f, 260f, 32f),
                "위험 — 추적당하고 있습니다",
                _threatStyle);
        }

        private void DrawDamageFlash()
        {
            if (Time.unscaledTime > _damageFlashUntil)
            {
                return;
            }

            var normalized = Mathf.InverseLerp(
                _damageFlashUntil,
                _damageFlashStartedAt,
                Time.unscaledTime);
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(
                    Red.r,
                    Red.g,
                    Red.b,
                    normalized * 0.30f *
                    LocalGameSettings.FlashIntensity));
        }

        private void DrawSpeakerAlarmFeedback()
        {
            if (Time.unscaledTime > _speakerAlarmUntil)
            {
                return;
            }

            var elapsed = Time.unscaledTime - _speakerAlarmStartedAt;
            var pulse = 0.5f +
                        (Mathf.Sin(elapsed * SpeakerAlarmPulseSpeed) * 0.5f +
                         0.5f) * 0.5f;
            var flashIntensity = LocalGameSettings.FlashIntensity;
            var vignetteIntensity = LocalGameSettings.VignetteIntensity;
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(Red.r, Red.g, Red.b, pulse * 0.08f * flashIntensity));

            var thickness = 34f + pulse * 22f;
            var edgeColor = new Color(
                Red.r,
                Red.g,
                Red.b,
                (0.35f + pulse * 0.45f) * vignetteIntensity);
            DrawSolidRect(new Rect(0f, 0f, Screen.width, thickness), edgeColor);
            DrawSolidRect(
                new Rect(0f, Screen.height - thickness, Screen.width, thickness),
                edgeColor);
            DrawSolidRect(new Rect(0f, 0f, thickness, Screen.height), edgeColor);
            DrawSolidRect(
                new Rect(Screen.width - thickness, 0f, thickness, Screen.height),
                edgeColor);
            GUI.Label(
                new Rect((Screen.width - 520f) * 0.5f, 82f, 520f, 38f),
                "비상 스피커 작동 — 원숭이 급습 주의",
                _threatStyle);
        }

        private void RefreshRoom(bool forceRefresh)
        {
            var targetTransform = _localTarget != null
                ? _localTarget.transform
                : _camera != null
                    ? _camera.Target
                    : null;
            if (targetTransform == null)
            {
                return;
            }

            var roomIndex = -1;
            for (var index = 0; index < _rooms.Length; index++)
            {
                if (_rooms[index].Contains(targetTransform.position))
                {
                    roomIndex = index;
                    break;
                }
            }

            if (!forceRefresh && roomIndex == _currentRoomIndex)
            {
                return;
            }

            _currentRoomIndex = roomIndex;
            _currentRoomName = roomIndex >= 0
                ? _rooms[roomIndex].DisplayName
                : "연구소 복도";
        }

        private bool IsLocalPlayerThreatened()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening &&
                networkManager.LocalClient != null)
            {
                var localClientId = networkManager.LocalClientId;
                foreach (var authority in
                         NetworkMonsterAuthority.ActiveAuthorities)
                {
                    if (authority != null &&
                        authority.IsThreateningClient(localClientId))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (_localTarget == null)
            {
                return false;
            }

            foreach (var monster in _localMonsters)
            {
                if (monster != null && monster.isActiveAndEnabled &&
                    monster.State is MonsterState.Chase or MonsterState.Bite &&
                    monster.Senses?.Target == _localTarget)
                {
                    return true;
                }
            }

            return false;
        }

        private void BindCurrentRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState == null)
            {
                _lastMilestone = ProjectMilestone.None;
                return;
            }

            _lastMilestone = _roundState.ProjectMilestone;
            _roundState.StateChanged += HandleRoundStateChanged;
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= HandleRoundStateChanged;
            }

            _roundState = null;
        }

        private void BindSpeakerAuthority()
        {
            UnbindSpeakerAuthority();
            _speakerAuthority = NetworkSpeakerAuthority.Current;
            if (_speakerAuthority != null)
            {
                _speakerAuthority.SpeakerActivated += HandleSpeakerActivated;
            }
        }

        private void UnbindSpeakerAuthority()
        {
            if (_speakerAuthority != null)
            {
                _speakerAuthority.SpeakerActivated -= HandleSpeakerActivated;
            }

            _speakerAuthority = null;
        }

        private void HandleRoundStateChanged()
        {
            if (_roundState == null ||
                _roundState.ProjectMilestone == _lastMilestone)
            {
                return;
            }

            _lastMilestone = _roundState.ProjectMilestone;
            var message = _lastMilestone switch
            {
                ProjectMilestone.FacilityGuidance =>
                    "부분 전력 복구 — 일부 조명이 켜졌습니다.",
                ProjectMilestone.SecurityAccess =>
                    "보안망 복구 — CCTV와 전자지도가 활성화됩니다.",
                ProjectMilestone.ExitGuidance =>
                    "탈출 경로 확인 — 출구와 남은 구역이 표시됩니다.",
                ProjectMilestone.Completed =>
                    "시설 복구 완료 — 생존자 승리",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(message))
            {
                ShowEventBanner(message, Green, EventBannerDurationSeconds);
                _camera?.AddTrauma(0.16f);
            }
        }

        private void HandleMissionStarted(FuseStationPrototype station)
        {
            ShowEventBanner(
                $"작업 시작 — {station.Prompt}",
                Cyan,
                1.4f);
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submitted,
            int expected)
        {
            ShowEventBanner(
                "작업 실패 — 발생한 소음에 주의하세요.",
                Red,
                EventBannerDurationSeconds);
            _camera?.AddTrauma(0.34f);
        }

        private void HandleMissionCancelled(FuseStationPrototype station)
        {
            ShowEventBanner("작업을 중단했습니다.", Orange, 1.5f);
        }

        private void HandleMissionCompleted(FuseStationPrototype station)
        {
            ShowEventBanner(
                $"작업 완료 — {station.Prompt}",
                Green,
                EventBannerDurationSeconds);
            _camera?.AddTrauma(0.12f);
        }

        private void HandleBitten(
            MonsterTarget target,
            MonsterBiteController source)
        {
            _damageFlashStartedAt = Time.unscaledTime;
            _damageFlashUntil =
                Time.unscaledTime + DamageFlashDurationSeconds;
            _camera?.AddTrauma(1f);
        }

        private void HandleSpeakerActivated(
            string roomId,
            float playbackSeconds)
        {
            _speakerAlarmStartedAt = Time.unscaledTime;
            _speakerAlarmUntil = Time.unscaledTime +
                                 Mathf.Max(0.8f, playbackSeconds);
            ShowEventBanner(
                "비상 스피커 작동 — 원숭이 급습 주의",
                Red,
                Mathf.Max(EventBannerDurationSeconds, playbackSeconds));
            _camera?.AddTrauma(0.8f);
        }

        private void ShowEventBanner(
            string message,
            Color color,
            float durationSeconds)
        {
            _eventBanner = message;
            _eventBannerColor = color;
            _eventBannerUntil =
                Time.unscaledTime + Mathf.Max(0f, durationSeconds);
        }

        private void HandleSettingsChanged()
        {
            _roomStyle = null;
            _eventBannerStyle = null;
            _hintStyle = null;
            _threatStyle = null;
        }

        private void SubscribeStations()
        {
            if (_isStationSubscribed)
            {
                return;
            }

            foreach (var station in _stations)
            {
                if (station == null)
                {
                    continue;
                }

                station.MissionStarted += HandleMissionStarted;
                station.MissionFailed += HandleMissionFailed;
                station.MissionCancelled += HandleMissionCancelled;
                station.MissionCompleted += HandleMissionCompleted;
            }

            _isStationSubscribed = true;
        }

        private void UnsubscribeStations()
        {
            if (!_isStationSubscribed)
            {
                return;
            }

            foreach (var station in _stations)
            {
                if (station == null)
                {
                    continue;
                }

                station.MissionStarted -= HandleMissionStarted;
                station.MissionFailed -= HandleMissionFailed;
                station.MissionCancelled -= HandleMissionCancelled;
                station.MissionCompleted -= HandleMissionCompleted;
            }

            _isStationSubscribed = false;
        }

        private void SubscribeTarget()
        {
            if (_isTargetSubscribed || _localTarget == null)
            {
                return;
            }

            _localTarget.BitePresented += HandleBitten;
            _isTargetSubscribed = true;
        }

        private void UnsubscribeTarget()
        {
            if (!_isTargetSubscribed || _localTarget == null)
            {
                return;
            }

            _localTarget.BitePresented -= HandleBitten;
            _isTargetSubscribed = false;
        }

        private void EnsureStyles()
        {
            if (_roomStyle != null)
            {
                return;
            }

            _roomStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(14),
                fontStyle = FontStyle.Bold
            };
            _roomStyle.normal.textColor = Color.white;
            _eventBannerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(18),
                fontStyle = FontStyle.Bold
            };
            _eventBannerStyle.normal.textColor = Color.white;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(13)
            };
            _hintStyle.normal.textColor =
                new Color(0.78f, 0.88f, 0.92f);
            _threatStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(16),
                fontStyle = FontStyle.Bold
            };
            _threatStyle.normal.textColor = Red;
        }

        private static void DrawReticleLine(Rect rect, Color color)
        {
            DrawSolidRect(rect, color);
        }

        private static void DrawCorner(
            Vector2 corner,
            Vector2 inward,
            float length,
            Color color)
        {
            var horizontalX = inward.x > 0f
                ? corner.x
                : corner.x - length;
            var verticalY = inward.y > 0f
                ? corner.y
                : corner.y - length;
            DrawSolidRect(
                new Rect(horizontalX, corner.y - 1f, length, 2f),
                color);
            DrawSolidRect(
                new Rect(corner.x - 1f, verticalY, 2f, length),
                color);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
