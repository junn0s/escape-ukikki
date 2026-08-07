using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 빌런 본인에게만 전용 미션 누적 강화 단계를 표시한다.
    /// 생존자 화면에는 배정 목록이나 강화 단계를 그리지 않는다.
    /// </summary>
    public sealed class VillainUpgradeHudView : MonoBehaviour
    {
        public const float PanelWidth = 234f;
        public const float PanelHeight = 154f;
        public const float PanelTopInset = 126f;
        public const float PanelRightInset = 18f;
        public const float PanelGap = 8f;

        private static readonly Color PanelColor =
            new(0.08f, 0.045f, 0.12f, 0.98f);
        private static readonly Color AccentColor =
            new(0.95f, 0.35f, 0.72f, 1f);
        private static readonly Color SafeColor =
            new(0.25f, 0.9f, 0.55f, 1f);

        private NetworkVillainMissionStackAuthority _missionStackAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _missionTitleStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _hintStyle;

        private void OnEnable()
        {
            NetworkVillainMissionStackAuthority.CurrentChanged += BindAuthority;
            BindAuthority();
        }

        private void OnDisable()
        {
            NetworkVillainMissionStackAuthority.CurrentChanged -= BindAuthority;
            UnbindAuthority();
        }

        private void BindAuthority()
        {
            UnbindAuthority();
            _missionStackAuthority =
                NetworkVillainMissionStackAuthority.Current;
            if (_missionStackAuthority != null)
            {
                _missionStackAuthority.LocalMissionStateChanged += RepaintView;
            }
        }

        private void UnbindAuthority()
        {
            if (_missionStackAuthority != null)
            {
                _missionStackAuthority.LocalMissionStateChanged -= RepaintView;
            }

            _missionStackAuthority = null;
        }

        private void RepaintView()
        {
        }

        private void OnGUI()
        {
            if (_missionStackAuthority == null ||
                !_missionStackAuthority.IsSpawned ||
                !IsLocalPlayerVillain())
            {
                return;
            }

            EnsureStyles();
            DrawUpgradeLevels();
        }

        private void DrawUpgradeLevels()
        {
            var area = GetUpgradePanelRect();
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    area.x + 12f,
                    area.y + 10f,
                    area.width - 24f,
                    area.height - 20f));
            GUILayout.Label(
                $"강화 단계  {_missionStackAuthority.LocalClearCount}/4",
                _titleStyle);
            GUILayout.Label(
                $"개체 {FormatLevel(UpgradeAxis.Population)} · " +
                DescribeEffect(UpgradeAxis.Population),
                _bodyStyle);
            GUILayout.Label(
                $"독성 {FormatLevel(UpgradeAxis.Toxicity)} · " +
                DescribeEffect(UpgradeAxis.Toxicity),
                _bodyStyle);
            GUILayout.Label(
                $"후각 {FormatLevel(UpgradeAxis.Scent)} · " +
                DescribeEffect(UpgradeAxis.Scent),
                _bodyStyle);
            GUILayout.Label("배정된 전용 미션 완료 즉시 서버 적용", _hintStyle);
            GUILayout.EndArea();
        }

        public static Rect GetUpgradePanelRect()
        {
            var safeArea = Screen.safeArea;
            return new Rect(
                safeArea.xMax - PanelWidth - PanelRightInset,
                safeArea.y + PanelTopInset,
                PanelWidth,
                PanelHeight);
        }

        private void DrawInteractiveChallenge(
            UpgradeStationPrototype station)
        {
            const float width = 680f;
            const float height = 440f;
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            var previousColor = GUI.color;
            GUI.color = PanelColor;
            GUI.Box(area, GUIContent.none);
            GUI.color = previousColor;

            GUI.Label(
                new Rect(area.x + 28f, area.y + 20f, width - 56f, 42f),
                GetChallengeTitle(station.Axis),
                _missionTitleStyle);
            GUI.Label(
                new Rect(area.x + 28f, area.y + 62f, width - 56f, 30f),
                GetChallengeInstruction(station.Axis),
                _centerStyle);

            switch (station.Axis)
            {
                case UpgradeAxis.Scent:
                    DrawScentChallenge(station, area);
                    break;
                case UpgradeAxis.Population:
                    DrawPopulationChallenge(station, area);
                    break;
                case UpgradeAxis.Toxicity:
                    DrawToxicityChallenge(station, area);
                    break;
            }

            var status = station.IsAwaitingServerCompletion
                ? "서버가 조작을 검증하고 있습니다..."
                : "[Esc] 취소 — 취소·오답·거리 이탈 시 처음부터 다시 시작";
            GUI.Label(
                new Rect(
                    area.x + 28f,
                    area.yMax - 38f,
                    width - 56f,
                    24f),
                status,
                _hintStyle);
        }

        private void DrawScentChallenge(
            UpgradeStationPrototype station,
            Rect area)
        {
            var targetPercent = Mathf.RoundToInt(
                station.ScentTargetNormalized * 100f);
            var tolerancePercent = Mathf.RoundToInt(
                station.ScentToleranceNormalized * 100f);
            GUI.Label(
                new Rect(area.x + 60f, area.y + 112f, 560f, 30f),
                $"목표 혼합 농도 {targetPercent}%  (허용 ±{tolerancePercent}%)",
                _centerStyle);

            DrawScentSlider(station, area, 0, "시약 A", 160f);
            DrawScentSlider(station, area, 1, "시약 B", 230f);

            GUI.Label(
                new Rect(area.x + 60f, area.y + 292f, 560f, 24f),
                $"현재 혼합 농도 {station.ScentPressureNormalized * 100f:0}%",
                _centerStyle);
            DrawProgressBar(
                new Rect(area.x + 110f, area.y + 322f, 460f, 22f),
                station.ScentStabilityProgress,
                SafeColor);

            var previousEnabled = GUI.enabled;
            GUI.enabled = !station.IsAwaitingServerCompletion;
            if (GUI.Button(
                    new Rect(area.x + 230f, area.y + 360f, 220f, 42f),
                    "혼합물 봉인"))
            {
                station.SealScentMixture();
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawScentSlider(
            UpgradeStationPrototype station,
            Rect area,
            int valveIndex,
            string label,
            float yOffset)
        {
            var current = station.GetScentValveOpening(valveIndex);
            GUI.Label(
                new Rect(area.x + 70f, area.y + yOffset, 90f, 28f),
                $"{label} {current * 100f:0}%",
                _bodyStyle);
            var next = GUI.HorizontalSlider(
                new Rect(
                    area.x + 170f,
                    area.y + yOffset + 5f,
                    420f,
                    24f),
                current,
                0f,
                1f);
            var quantized = Mathf.Round(next * 20f) / 20f;
            if (!station.IsAwaitingServerCompletion &&
                Mathf.Abs(quantized - current) >= 0.049f)
            {
                station.SetScentValveOpening(valveIndex, quantized);
            }
        }

        private void DrawPopulationChallenge(
            UpgradeStationPrototype station,
            Rect area)
        {
            var nodeCount = station.PopulationNodeCount;
            const float buttonWidth = 150f;
            const float gap = 24f;
            var totalWidth =
                nodeCount * buttonWidth +
                Mathf.Max(0, nodeCount - 1) * gap;
            var startX = area.center.x - totalWidth * 0.5f;
            var previousEnabled = GUI.enabled;
            GUI.enabled = !station.IsAwaitingServerCompletion;
            for (var index = 0; index < nodeCount; index++)
            {
                var current =
                    station.GetPopulationCurrentRotation(index) * 90;
                var target =
                    station.GetPopulationTargetRotation(index) * 90;
                if (GUI.Button(
                        new Rect(
                            startX + index * (buttonWidth + gap),
                            area.y + 150f,
                            buttonWidth,
                            120f),
                        $"모듈 {index + 1}\n\n현재 {current}°\n목표 {target}°\n\n클릭 ↻"))
                {
                    station.RotatePopulationCircuit(index);
                }
            }

            GUI.Label(
                new Rect(area.x + 60f, area.y + 290f, 560f, 28f),
                $"정렬 {station.PopulationAlignedNodeCount}/{nodeCount}",
                _centerStyle);
            DrawProgressBar(
                new Rect(area.x + 110f, area.y + 322f, 460f, 22f),
                station.NormalizedProgress,
                AccentColor);
            if (GUI.Button(
                    new Rect(area.x + 230f, area.y + 360f, 220f, 42f),
                    "격리 회로 시험 가동"))
            {
                station.TestPopulationCircuit();
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawToxicityChallenge(
            UpgradeStationPrototype station,
            Rect area)
        {
            GUI.Label(
                new Rect(area.x + 60f, area.y + 126f, 560f, 28f),
                $"안전 주입 {station.ToxicityProgressIndex}/{station.ToxicityStepCount}",
                _centerStyle);

            var track = new Rect(
                area.x + 90f,
                area.y + 195f,
                500f,
                50f);
            DrawSolidRect(track, new Color(0.12f, 0.15f, 0.2f, 1f));
            var tolerance =
                station.ToxicitySuccessToleranceNormalized;
            var target = station.ToxicityTargetNormalized;
            var safeStart = Mathf.Clamp01(target - tolerance);
            var safeEnd = Mathf.Clamp01(target + tolerance);
            DrawSolidRect(
                new Rect(
                    track.x + safeStart * track.width,
                    track.y,
                    (safeEnd - safeStart) * track.width,
                    track.height),
                new Color(0.18f, 0.65f, 0.38f, 0.85f));
            var markerX =
                track.x +
                station.ToxicityMarkerNormalized * track.width;
            DrawSolidRect(
                new Rect(markerX - 3f, track.y - 8f, 6f, 66f),
                new Color(1f, 0.55f, 0.12f, 1f));

            GUI.Label(
                new Rect(area.x + 60f, area.y + 264f, 560f, 28f),
                "주황 마커가 초록 구간 안에 있을 때 주입하세요.",
                _centerStyle);

            var shouldInject =
                !station.IsAwaitingServerCompletion &&
                Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Space;
            var previousEnabled = GUI.enabled;
            GUI.enabled = !station.IsAwaitingServerCompletion;
            if (GUI.Button(
                    new Rect(area.x + 230f, area.y + 325f, 220f, 50f),
                    "약품 주입  [Space]") || shouldInject)
            {
                station.InjectToxicityDose();
                if (shouldInject)
                {
                    Event.current.Use();
                }
            }
            GUI.enabled = previousEnabled;
        }

        private string FormatLevel(UpgradeAxis axis)
        {
            var clearCount = _missionStackAuthority.LocalClearCount;
            var level = axis switch
            {
                UpgradeAxis.Population =>
                    VillainMissionStackEffectRules.GetPopulationTier(
                        clearCount),
                UpgradeAxis.Toxicity =>
                    VillainMissionStackEffectRules.GetToxicityTier(
                        clearCount),
                _ => VillainMissionStackEffectRules
                    .GetProximityDetectionTier(clearCount)
            };
            return level > 0 ? $"{level}단계" : "기본";
        }

        private string DescribeEffect(UpgradeAxis axis)
        {
            var tierConfig = _missionStackAuthority.TierConfig;
            if (tierConfig == null)
            {
                return "효과 계산 중";
            }

            var clearCount = _missionStackAuthority.LocalClearCount;
            var level = axis switch
            {
                UpgradeAxis.Population =>
                    VillainMissionStackEffectRules.GetPopulationTier(
                        clearCount),
                UpgradeAxis.Toxicity =>
                    VillainMissionStackEffectRules.GetToxicityTier(
                        clearCount),
                _ => VillainMissionStackEffectRules
                    .GetProximityDetectionTier(clearCount)
            };
            return axis switch
            {
                UpgradeAxis.Scent =>
                    $"탐지 {tierConfig.GetProximityDetectionRadius(level):0.##}m",
                UpgradeAxis.Population =>
                    $"원숭이 {tierConfig.GetMonsterCount(level)}마리",
                UpgradeAxis.Toxicity =>
                    $"감염 {tierConfig.GetInfectionDurationSeconds(level):0}초",
                _ => string.Empty
            };
        }

        private static string GetChallengeTitle(UpgradeAxis axis)
        {
            return axis switch
            {
                UpgradeAxis.Scent => "위장 작업 — 화학물질 비율 조정",
                UpgradeAxis.Population => "위장 작업 — 격리 잠금장치 복구",
                UpgradeAxis.Toxicity => "위장 작업 — 약품 안정화",
                _ => "빌런 강화 작업"
            };
        }

        private static string GetChallengeInstruction(UpgradeAxis axis)
        {
            return axis switch
            {
                UpgradeAxis.Scent =>
                    "두 밸브를 드래그해 목표 농도를 유지하고 혼합물을 봉인하세요.",
                UpgradeAxis.Population =>
                    "각 모듈을 클릭해 목표 각도로 맞춘 뒤 회로를 시험하세요.",
                UpgradeAxis.Toxicity =>
                    "왕복 마커가 안전 구간에 들어올 때 3회 주입하세요.",
                _ => string.Empty
            };
        }

        private static void DrawProgressBar(
            Rect rect,
            float normalized,
            Color fillColor)
        {
            DrawSolidRect(rect, new Color(0.12f, 0.15f, 0.2f, 1f));
            DrawSolidRect(
                new Rect(
                    rect.x,
                    rect.y,
                    rect.width * Mathf.Clamp01(normalized),
                    rect.height),
                fillColor);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static bool IsLocalPlayerVillain()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject =
                networkManager != null && networkManager.IsClient
                    ? networkManager.LocalClient?.PlayerObject
                    : null;
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkPlayerAvatar>(
                       out var avatar) &&
                   avatar.Role == PlayerRole.Villain;
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.55f, 0.2f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _missionTitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = AccentColor }
            };
            _centerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            _hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.75f, 0.76f, 0.82f) }
            };
        }
    }
}
