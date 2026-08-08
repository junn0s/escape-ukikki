using System.Collections.Generic;
using MonkeyLab.Presentation.Audio;
using MonkeyLab.Presentation.VFX;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.Settings
{
    public sealed class RuntimeSettingsOverlay : MonoBehaviour
    {
        private const float PanelWidth = 640f;
        private const float PanelMaximumHeight = 800f;

        private static bool _isBootstrapped;

        private readonly List<Vector2Int> _resolutions = new();
        private Vector2 _scrollPosition;
        private int _selectedResolutionIndex;
        private bool _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrapState()
        {
            _isBootstrapped = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_isBootstrapped)
            {
                return;
            }

            _isBootstrapped = true;
            var instance = new GameObject("[Settings] LocalRuntimeSettings");
            instance.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(instance);
            instance.AddComponent<ScreenBrightnessOverlay>();
            instance.AddComponent<RuntimeAudioVolumeRouter>();
            instance.AddComponent<RuntimeSettingsOverlay>();
        }

        private void Awake()
        {
            BuildResolutionList();
        }

        private void OnGUI()
        {
            GUI.depth = -1000;
            HandleKeyboardShortcut(Event.current);

            if (!_isOpen)
            {
                if (GUI.Button(
                        new Rect(
                            Screen.width - 98f,
                            Screen.height - 38f,
                            86f,
                            26f),
                        "F1 설정"))
                {
                    _isOpen = true;
                }

                return;
            }

            DrawBackdrop();
            DrawPanel();
        }

        private void DrawBackdrop()
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawPanel()
        {
            var height = Mathf.Min(PanelMaximumHeight, Screen.height - 36f);
            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - height) * 0.5f,
                PanelWidth,
                height);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    panel.x + 18f,
                    panel.y + 14f,
                    panel.width - 36f,
                    panel.height - 28f));

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(25),
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label("설정 · 접근성", titleStyle);
            GUILayout.Label(
                "온라인 라운드는 설정 창을 열어도 계속 진행됩니다.",
                CreateLabelStyle(13));

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            DrawGraphicsSettings();
            DrawAudioSettings();
            DrawAccessibilitySettings();
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기본값 복원", GUILayout.Height(34f)))
            {
                LocalGameSettings.ResetDefaults();
                SelectSavedResolution();
            }

            if (GUILayout.Button("닫기 [F1 / Esc]", GUILayout.Height(34f)))
            {
                _isOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawGraphicsSettings()
        {
            DrawSectionTitle("그래픽");
            GUILayout.BeginHorizontal();
            GUILayout.Label("해상도", CreateLabelStyle(15), GUILayout.Width(145f));
            GUI.enabled = _resolutions.Count > 1;
            if (GUILayout.Button("◀", GUILayout.Width(42f)))
            {
                _selectedResolutionIndex =
                    (_selectedResolutionIndex - 1 + _resolutions.Count) %
                    _resolutions.Count;
            }

            GUI.enabled = true;
            var resolution = _resolutions[_selectedResolutionIndex];
            GUILayout.Label(
                $"{resolution.x} × {resolution.y}",
                CreateLabelStyle(15),
                GUILayout.Width(180f));
            GUI.enabled = _resolutions.Count > 1;
            if (GUILayout.Button("▶", GUILayout.Width(42f)))
            {
                _selectedResolutionIndex =
                    (_selectedResolutionIndex + 1) % _resolutions.Count;
            }

            GUI.enabled = true;
            if (GUILayout.Button("적용", GUILayout.Width(80f)))
            {
                LocalGameSettings.SetResolution(resolution.x, resolution.y);
            }

            GUILayout.EndHorizontal();

            LocalGameSettings.IsFullScreen = GUILayout.Toggle(
                LocalGameSettings.IsFullScreen,
                "전체 화면");
            LocalGameSettings.IsVSyncEnabled = GUILayout.Toggle(
                LocalGameSettings.IsVSyncEnabled,
                "VSync");

            GUILayout.BeginHorizontal();
            GUILayout.Label("품질", CreateLabelStyle(15), GUILayout.Width(145f));
            if (GUILayout.Button("◀", GUILayout.Width(42f)))
            {
                LocalGameSettings.QualityLevel--;
            }

            var qualityNames = QualitySettings.names;
            var qualityLabel = qualityNames.Length > 0
                ? qualityNames[Mathf.Clamp(
                    LocalGameSettings.QualityLevel,
                    0,
                    qualityNames.Length - 1)]
                : "Default";
            GUILayout.Label(
                qualityLabel,
                CreateLabelStyle(15),
                GUILayout.Width(180f));
            if (GUILayout.Button("▶", GUILayout.Width(42f)))
            {
                LocalGameSettings.QualityLevel++;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "프레임 제한",
                CreateLabelStyle(15),
                GUILayout.Width(145f));
            if (GUILayout.Button(CreateFrameRateLabel()))
            {
                LocalGameSettings.TargetFrameRate =
                    GetNextFrameRate(LocalGameSettings.TargetFrameRate);
            }

            GUILayout.EndHorizontal();
            LocalGameSettings.Brightness = DrawSlider(
                "화면 밝기",
                LocalGameSettings.Brightness,
                0.65f,
                1f,
                $"{Mathf.RoundToInt(LocalGameSettings.Brightness * 100f)}%");
            GUILayout.Label(
                "암흑 시야 규칙을 유지하기 위해 기본값보다 밝게는 올릴 수 없습니다.",
                CreateLabelStyle(12));
        }

        private void DrawAudioSettings()
        {
            DrawSectionTitle("오디오");
            LocalGameSettings.MasterVolume = DrawVolumeSlider(
                "마스터",
                LocalGameSettings.MasterVolume);
            LocalGameSettings.MusicVolume = DrawVolumeSlider(
                "음악",
                LocalGameSettings.MusicVolume);
            LocalGameSettings.EffectsVolume = DrawVolumeSlider(
                "효과음",
                LocalGameSettings.EffectsVolume);
            LocalGameSettings.UserInterfaceVolume = DrawVolumeSlider(
                "UI",
                LocalGameSettings.UserInterfaceVolume);
            LocalGameSettings.DangerVolume = DrawVolumeSlider(
                "괴물·위험 신호",
                LocalGameSettings.DangerVolume);
        }

        private void DrawAccessibilitySettings()
        {
            DrawSectionTitle("게임 · 접근성");
            LocalGameSettings.TextScale = DrawSlider(
                "텍스트 크기",
                LocalGameSettings.TextScale,
                0.85f,
                1.4f,
                $"{Mathf.RoundToInt(LocalGameSettings.TextScale * 100f)}%");
            LocalGameSettings.ScreenShakeIntensity = DrawSlider(
                "화면 흔들림",
                LocalGameSettings.ScreenShakeIntensity,
                0f,
                1f,
                CreateIntensityLabel(LocalGameSettings.ScreenShakeIntensity));
            LocalGameSettings.FlashIntensity = DrawSlider(
                "플래시·점멸",
                LocalGameSettings.FlashIntensity,
                0f,
                1f,
                CreateIntensityLabel(LocalGameSettings.FlashIntensity));
            LocalGameSettings.VignetteIntensity = DrawSlider(
                "위험 비네팅",
                LocalGameSettings.VignetteIntensity,
                0f,
                1f,
                CreateIntensityLabel(LocalGameSettings.VignetteIntensity));
            LocalGameSettings.ShowSoundCaptions = GUILayout.Toggle(
                LocalGameSettings.ShowSoundCaptions,
                "주요 소리 자막");
            LocalGameSettings.ShowSoundDirection = GUILayout.Toggle(
                LocalGameSettings.ShowSoundDirection,
                "소리 방향 표시");

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "색상 구분 보조",
                CreateLabelStyle(15),
                GUILayout.Width(145f));
            if (GUILayout.Button(CreateColorVisionLabel()))
            {
                var next = (int)LocalGameSettings.ColorMode + 1;
                if (next > (int)ColorVisionMode.HighContrast)
                {
                    next = 0;
                }

                LocalGameSettings.ColorMode =
                    (ColorVisionMode)next;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(
                "색상과 함께 경고 문장·기호를 항상 표시합니다.",
                CreateLabelStyle(12));
        }

        private static float DrawVolumeSlider(string label, float value)
        {
            return DrawSlider(
                label,
                value,
                0f,
                1f,
                $"{Mathf.RoundToInt(value * 100f)}%");
        }

        private static float DrawSlider(
            string label,
            float value,
            float minimum,
            float maximum,
            string valueLabel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, CreateLabelStyle(15), GUILayout.Width(145f));
            var result = GUILayout.HorizontalSlider(value, minimum, maximum);
            GUILayout.Label(
                valueLabel,
                CreateLabelStyle(14),
                GUILayout.Width(64f));
            GUILayout.EndHorizontal();
            return result;
        }

        private static void DrawSectionTitle(string title)
        {
            GUILayout.Space(12f);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = LocalGameSettings.GetScaledFontSize(18),
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = LocalGameSettings.GetSemanticColor(
                        SemanticUiColor.Information)
                }
            };
            GUILayout.Label(title, style);
        }

        private static GUIStyle CreateLabelStyle(int baseFontSize)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = LocalGameSettings.GetScaledFontSize(baseFontSize),
                wordWrap = true
            };
        }

        private static string CreateFrameRateLabel()
        {
            return LocalGameSettings.TargetFrameRate > 0
                ? $"{LocalGameSettings.TargetFrameRate} FPS"
                : "제한 없음";
        }

        private static int GetNextFrameRate(int current)
        {
            return current switch
            {
                30 => 60,
                60 => 120,
                120 => 144,
                144 => -1,
                _ => 30
            };
        }

        private static string CreateIntensityLabel(float value)
        {
            if (value <= 0.01f)
            {
                return "끔";
            }

            return $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private static string CreateColorVisionLabel()
        {
            return LocalGameSettings.ColorMode switch
            {
                ColorVisionMode.Deuteranopia => "녹색약 보조",
                ColorVisionMode.Protanopia => "적색약 보조",
                ColorVisionMode.Tritanopia => "청색약 보조",
                ColorVisionMode.HighContrast => "고대비",
                _ => "기본"
            };
        }

        private void HandleKeyboardShortcut(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.F1)
            {
                _isOpen = !_isOpen;
                currentEvent.Use();
                return;
            }

            if (_isOpen && currentEvent.keyCode == KeyCode.Escape)
            {
                _isOpen = false;
                currentEvent.Use();
            }
        }

        private void BuildResolutionList()
        {
            _resolutions.Clear();
            foreach (var resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!_resolutions.Contains(size))
                {
                    _resolutions.Add(size);
                }
            }

            if (_resolutions.Count == 0)
            {
                _resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            }

            SelectSavedResolution();
        }

        private void SelectSavedResolution()
        {
            var saved = new Vector2Int(
                LocalGameSettings.ResolutionWidth,
                LocalGameSettings.ResolutionHeight);
            var index = _resolutions.IndexOf(saved);
            _selectedResolutionIndex = index >= 0
                ? index
                : Mathf.Clamp(_resolutions.Count - 1, 0, _resolutions.Count - 1);
        }
    }

    public sealed class ScreenBrightnessOverlay : MonoBehaviour
    {
        private void OnGUI()
        {
            var dimAlpha = 1f - LocalGameSettings.Brightness;
            if (dimAlpha <= 0.001f)
            {
                return;
            }

            GUI.depth = 1000;
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }

    /// <summary>
    /// 기존 씬의 AudioSource도 씬 재생성 없이 카테고리 음량을 따르게 한다.
    /// 검색은 씬 로드 시에만 수행하고 프레임 루프에서는 재검색하지 않는다.
    /// </summary>
    public sealed class RuntimeAudioVolumeRouter : MonoBehaviour
    {
        private readonly List<RoutedAudioSource> _sources = new();

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalGameSettings.Changed += ApplyVolumes;
            RefreshSources();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LocalGameSettings.Changed -= ApplyVolumes;
            _sources.Clear();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshSources();
        }

        private void RefreshSources()
        {
            _sources.Clear();
            var sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var source in sources)
            {
                if (source == null ||
                    source.GetComponent<SettingsAudioSource>() != null)
                {
                    continue;
                }

                _sources.Add(new RoutedAudioSource(
                    source,
                    source.volume,
                    ResolveCategory(source)));
            }

            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            for (var index = _sources.Count - 1; index >= 0; index--)
            {
                var routed = _sources[index];
                if (routed.Source == null)
                {
                    _sources.RemoveAt(index);
                    continue;
                }

                routed.Source.volume = routed.BaseVolume *
                    LocalGameSettings.GetCategoryVolume(routed.Category);
            }
        }

        private static AudioCategory ResolveCategory(AudioSource source)
        {
            if (source.GetComponent<FuseFailureFeedback>() != null)
            {
                return AudioCategory.Danger;
            }

            if (source.GetComponent<ProjectMilestoneWorldPresenter>() != null ||
                source.GetComponent<MissionAssetFeedbackPresenter>() != null ||
                source.GetComponent<RoundEndingSequencePresenter>() != null)
            {
                return AudioCategory.Effects;
            }

            return source.loop ? AudioCategory.Music : AudioCategory.Effects;
        }

        private readonly struct RoutedAudioSource
        {
            public RoutedAudioSource(
                AudioSource source,
                float baseVolume,
                AudioCategory category)
            {
                Source = source;
                BaseVolume = baseVolume;
                Category = category;
            }

            public AudioSource Source { get; }
            public float BaseVolume { get; }
            public AudioCategory Category { get; }
        }
    }
}
