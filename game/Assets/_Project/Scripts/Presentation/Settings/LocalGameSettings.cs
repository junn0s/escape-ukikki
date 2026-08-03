using System;
using UnityEngine;

namespace MonkeyLab.Presentation.Settings
{
    public enum AudioCategory : byte
    {
        Music = 0,
        Effects = 1,
        UserInterface = 2,
        Danger = 3
    }

    public enum ColorVisionMode : byte
    {
        Default = 0,
        Deuteranopia = 1,
        Protanopia = 2,
        Tritanopia = 3,
        HighContrast = 4
    }

    public enum SemanticUiColor : byte
    {
        Information = 0,
        Success = 1,
        Warning = 2,
        Danger = 3
    }

    /// <summary>
    /// 로컬 그래픽·오디오·접근성 설정의 단일 저장 지점이다.
    /// 온라인 판정에는 관여하지 않으며 값은 PlayerPrefs에 즉시 저장한다.
    /// </summary>
    public static class LocalGameSettings
    {
        private const string Prefix = "MonkeyLab.Settings.";
        private const float MinimumBrightness = 0.65f;
        private const float MaximumBrightness = 1f;
        private const float MinimumTextScale = 0.85f;
        private const float MaximumTextScale = 1.4f;

        private static float _brightness;
        private static bool _isFullScreen;
        private static bool _isVSyncEnabled;
        private static int _targetFrameRate;
        private static int _qualityLevel;
        private static int _resolutionWidth;
        private static int _resolutionHeight;
        private static float _masterVolume;
        private static float _musicVolume;
        private static float _effectsVolume;
        private static float _userInterfaceVolume;
        private static float _dangerVolume;
        private static float _textScale;
        private static float _screenShakeIntensity;
        private static float _flashIntensity;
        private static float _vignetteIntensity;
        private static bool _showSoundDirection;
        private static bool _showSoundCaptions;
        private static ColorVisionMode _colorVisionMode;

        static LocalGameSettings()
        {
            Load();
            ApplyRuntimeSettings();
        }

        public static event Action Changed;

        public static float Brightness
        {
            get => _brightness;
            set => SetFloat(
                ref _brightness,
                Mathf.Clamp(value, MinimumBrightness, MaximumBrightness),
                nameof(Brightness));
        }

        public static bool IsFullScreen
        {
            get => _isFullScreen;
            set
            {
                if (!SetBool(ref _isFullScreen, value, nameof(IsFullScreen)))
                {
                    return;
                }

                ApplyDisplaySettings();
            }
        }

        public static bool IsVSyncEnabled
        {
            get => _isVSyncEnabled;
            set
            {
                if (!SetBool(
                        ref _isVSyncEnabled,
                        value,
                        nameof(IsVSyncEnabled)))
                {
                    return;
                }

                QualitySettings.vSyncCount = value ? 1 : 0;
            }
        }

        public static int TargetFrameRate
        {
            get => _targetFrameRate;
            set
            {
                var sanitized = value is 30 or 60 or 120 or 144 ? value : -1;
                if (!SetInt(
                        ref _targetFrameRate,
                        sanitized,
                        nameof(TargetFrameRate)))
                {
                    return;
                }

                Application.targetFrameRate = sanitized;
            }
        }

        public static int QualityLevel
        {
            get => _qualityLevel;
            set
            {
                var maximum = Mathf.Max(0, QualitySettings.names.Length - 1);
                var sanitized = Mathf.Clamp(value, 0, maximum);
                if (!SetInt(
                        ref _qualityLevel,
                        sanitized,
                        nameof(QualityLevel)))
                {
                    return;
                }

                QualitySettings.SetQualityLevel(sanitized, true);
            }
        }

        public static int ResolutionWidth => _resolutionWidth;
        public static int ResolutionHeight => _resolutionHeight;

        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                if (!SetFloat(
                        ref _masterVolume,
                        Mathf.Clamp01(value),
                        nameof(MasterVolume)))
                {
                    return;
                }

                AudioListener.volume = _masterVolume;
            }
        }

        public static float MusicVolume
        {
            get => _musicVolume;
            set => SetFloat(
                ref _musicVolume,
                Mathf.Clamp01(value),
                nameof(MusicVolume));
        }

        public static float EffectsVolume
        {
            get => _effectsVolume;
            set => SetFloat(
                ref _effectsVolume,
                Mathf.Clamp01(value),
                nameof(EffectsVolume));
        }

        public static float UserInterfaceVolume
        {
            get => _userInterfaceVolume;
            set => SetFloat(
                ref _userInterfaceVolume,
                Mathf.Clamp01(value),
                nameof(UserInterfaceVolume));
        }

        public static float DangerVolume
        {
            get => _dangerVolume;
            set => SetFloat(
                ref _dangerVolume,
                Mathf.Clamp01(value),
                nameof(DangerVolume));
        }

        public static float TextScale
        {
            get => _textScale;
            set => SetFloat(
                ref _textScale,
                Mathf.Clamp(value, MinimumTextScale, MaximumTextScale),
                nameof(TextScale));
        }

        public static float ScreenShakeIntensity
        {
            get => _screenShakeIntensity;
            set => SetFloat(
                ref _screenShakeIntensity,
                Mathf.Clamp01(value),
                nameof(ScreenShakeIntensity));
        }

        public static float FlashIntensity
        {
            get => _flashIntensity;
            set => SetFloat(
                ref _flashIntensity,
                Mathf.Clamp01(value),
                nameof(FlashIntensity));
        }

        public static float VignetteIntensity
        {
            get => _vignetteIntensity;
            set => SetFloat(
                ref _vignetteIntensity,
                Mathf.Clamp01(value),
                nameof(VignetteIntensity));
        }

        public static bool ShowSoundDirection
        {
            get => _showSoundDirection;
            set => SetBool(
                ref _showSoundDirection,
                value,
                nameof(ShowSoundDirection));
        }

        public static bool ShowSoundCaptions
        {
            get => _showSoundCaptions;
            set => SetBool(
                ref _showSoundCaptions,
                value,
                nameof(ShowSoundCaptions));
        }

        public static ColorVisionMode ColorMode
        {
            get => _colorVisionMode;
            set => SetIntEnum(
                ref _colorVisionMode,
                value,
                nameof(ColorMode));
        }

        public static int GetScaledFontSize(int baseSize)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseSize * _textScale));
        }

        public static float GetCategoryVolume(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => _musicVolume,
                AudioCategory.Effects => _effectsVolume,
                AudioCategory.UserInterface => _userInterfaceVolume,
                AudioCategory.Danger => _dangerVolume,
                _ => 1f
            };
        }

        public static Color GetSemanticColor(
            SemanticUiColor role,
            float alpha = 1f)
        {
            var color = _colorVisionMode switch
            {
                ColorVisionMode.Deuteranopia => role switch
                {
                    SemanticUiColor.Success => new Color(0.18f, 0.72f, 1f),
                    SemanticUiColor.Warning => new Color(1f, 0.78f, 0.12f),
                    SemanticUiColor.Danger => new Color(0.92f, 0.22f, 0.82f),
                    _ => new Color(0.30f, 0.88f, 1f)
                },
                ColorVisionMode.Protanopia => role switch
                {
                    SemanticUiColor.Success => new Color(0.16f, 0.78f, 0.92f),
                    SemanticUiColor.Warning => new Color(1f, 0.74f, 0.08f),
                    SemanticUiColor.Danger => new Color(0.96f, 0.42f, 0.08f),
                    _ => new Color(0.34f, 0.86f, 1f)
                },
                ColorVisionMode.Tritanopia => role switch
                {
                    SemanticUiColor.Success => new Color(0.26f, 0.90f, 0.52f),
                    SemanticUiColor.Warning => new Color(1f, 0.48f, 0.34f),
                    SemanticUiColor.Danger => new Color(0.94f, 0.16f, 0.26f),
                    _ => new Color(0.30f, 0.82f, 0.92f)
                },
                ColorVisionMode.HighContrast => role switch
                {
                    SemanticUiColor.Success => new Color(0.20f, 1f, 0.82f),
                    SemanticUiColor.Warning => new Color(1f, 0.88f, 0.08f),
                    SemanticUiColor.Danger => new Color(1f, 0.08f, 0.70f),
                    _ => Color.white
                },
                _ => role switch
                {
                    SemanticUiColor.Success => new Color(0.22f, 0.95f, 0.48f),
                    SemanticUiColor.Warning => new Color(1f, 0.62f, 0.14f),
                    SemanticUiColor.Danger => new Color(1f, 0.10f, 0.12f),
                    _ => new Color(0.24f, 0.91f, 0.94f)
                }
            };
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static void SetResolution(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _resolutionWidth = width;
            _resolutionHeight = height;
            PlayerPrefs.SetInt(Prefix + nameof(ResolutionWidth), width);
            PlayerPrefs.SetInt(Prefix + nameof(ResolutionHeight), height);
            PlayerPrefs.Save();
            ApplyDisplaySettings();
            Changed?.Invoke();
        }

        public static void ResetDefaults()
        {
            _brightness = 1f;
            _isFullScreen = true;
            _isVSyncEnabled = true;
            _targetFrameRate = 60;
            _qualityLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
            _resolutionWidth = Screen.currentResolution.width;
            _resolutionHeight = Screen.currentResolution.height;
            _masterVolume = 1f;
            _musicVolume = 0.8f;
            _effectsVolume = 1f;
            _userInterfaceVolume = 1f;
            _dangerVolume = 1f;
            _textScale = 1f;
            _screenShakeIntensity = 1f;
            _flashIntensity = 1f;
            _vignetteIntensity = 1f;
            _showSoundDirection = true;
            _showSoundCaptions = true;
            _colorVisionMode = ColorVisionMode.Default;
            SaveAll();
            ApplyRuntimeSettings();
            Changed?.Invoke();
        }

        private static void Load()
        {
            _brightness = Mathf.Clamp(
                PlayerPrefs.GetFloat(Prefix + nameof(Brightness), 1f),
                MinimumBrightness,
                MaximumBrightness);
            _isFullScreen = GetBool(nameof(IsFullScreen), true);
            _isVSyncEnabled = GetBool(nameof(IsVSyncEnabled), true);
            _targetFrameRate = PlayerPrefs.GetInt(
                Prefix + nameof(TargetFrameRate),
                60);
            _qualityLevel = Mathf.Clamp(
                PlayerPrefs.GetInt(
                    Prefix + nameof(QualityLevel),
                    Mathf.Max(0, QualitySettings.names.Length - 1)),
                0,
                Mathf.Max(0, QualitySettings.names.Length - 1));
            _resolutionWidth = PlayerPrefs.GetInt(
                Prefix + nameof(ResolutionWidth),
                Screen.currentResolution.width);
            _resolutionHeight = PlayerPrefs.GetInt(
                Prefix + nameof(ResolutionHeight),
                Screen.currentResolution.height);
            _masterVolume = LoadVolume(nameof(MasterVolume), 1f);
            _musicVolume = LoadVolume(nameof(MusicVolume), 0.8f);
            _effectsVolume = LoadVolume(nameof(EffectsVolume), 1f);
            _userInterfaceVolume = LoadVolume(nameof(UserInterfaceVolume), 1f);
            _dangerVolume = LoadVolume(nameof(DangerVolume), 1f);
            _textScale = Mathf.Clamp(
                PlayerPrefs.GetFloat(Prefix + nameof(TextScale), 1f),
                MinimumTextScale,
                MaximumTextScale);
            _screenShakeIntensity = LoadVolume(
                nameof(ScreenShakeIntensity),
                1f);
            _flashIntensity = LoadVolume(nameof(FlashIntensity), 1f);
            _vignetteIntensity = LoadVolume(nameof(VignetteIntensity), 1f);
            _showSoundDirection = GetBool(nameof(ShowSoundDirection), true);
            _showSoundCaptions = GetBool(nameof(ShowSoundCaptions), true);
            _colorVisionMode = (ColorVisionMode)Mathf.Clamp(
                PlayerPrefs.GetInt(Prefix + nameof(ColorMode), 0),
                0,
                (int)ColorVisionMode.HighContrast);
        }

        private static void ApplyRuntimeSettings()
        {
            AudioListener.volume = _masterVolume;
            QualitySettings.vSyncCount = _isVSyncEnabled ? 1 : 0;
            Application.targetFrameRate = _targetFrameRate;
            QualitySettings.SetQualityLevel(_qualityLevel, true);
            ApplyDisplaySettings();
        }

        private static void ApplyDisplaySettings()
        {
            if (Application.isEditor || _resolutionWidth <= 0 ||
                _resolutionHeight <= 0)
            {
                return;
            }

            Screen.SetResolution(
                _resolutionWidth,
                _resolutionHeight,
                _isFullScreen ? FullScreenMode.FullScreenWindow :
                    FullScreenMode.Windowed);
        }

        private static bool SetFloat(
            ref float field,
            float value,
            string key)
        {
            if (Mathf.Approximately(field, value))
            {
                return false;
            }

            field = value;
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private static bool SetInt(ref int field, int value, string key)
        {
            if (field == value)
            {
                return false;
            }

            field = value;
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private static bool SetBool(ref bool field, bool value, string key)
        {
            if (field == value)
            {
                return false;
            }

            field = value;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private static void SetIntEnum<T>(ref T field, T value, string key)
            where T : struct, Enum
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            PlayerPrefs.SetInt(Prefix + key, Convert.ToInt32(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static float LoadVolume(string key, float fallback)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback));
        }

        private static bool GetBool(string key, bool fallback)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;
        }

        private static void SaveAll()
        {
            PlayerPrefs.SetFloat(Prefix + nameof(Brightness), _brightness);
            PlayerPrefs.SetInt(
                Prefix + nameof(IsFullScreen),
                _isFullScreen ? 1 : 0);
            PlayerPrefs.SetInt(
                Prefix + nameof(IsVSyncEnabled),
                _isVSyncEnabled ? 1 : 0);
            PlayerPrefs.SetInt(
                Prefix + nameof(TargetFrameRate),
                _targetFrameRate);
            PlayerPrefs.SetInt(Prefix + nameof(QualityLevel), _qualityLevel);
            PlayerPrefs.SetInt(
                Prefix + nameof(ResolutionWidth),
                _resolutionWidth);
            PlayerPrefs.SetInt(
                Prefix + nameof(ResolutionHeight),
                _resolutionHeight);
            PlayerPrefs.SetFloat(Prefix + nameof(MasterVolume), _masterVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(MusicVolume), _musicVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(EffectsVolume), _effectsVolume);
            PlayerPrefs.SetFloat(
                Prefix + nameof(UserInterfaceVolume),
                _userInterfaceVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(DangerVolume), _dangerVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(TextScale), _textScale);
            PlayerPrefs.SetFloat(
                Prefix + nameof(ScreenShakeIntensity),
                _screenShakeIntensity);
            PlayerPrefs.SetFloat(Prefix + nameof(FlashIntensity), _flashIntensity);
            PlayerPrefs.SetFloat(
                Prefix + nameof(VignetteIntensity),
                _vignetteIntensity);
            PlayerPrefs.SetInt(
                Prefix + nameof(ShowSoundDirection),
                _showSoundDirection ? 1 : 0);
            PlayerPrefs.SetInt(
                Prefix + nameof(ShowSoundCaptions),
                _showSoundCaptions ? 1 : 0);
            PlayerPrefs.SetInt(
                Prefix + nameof(ColorMode),
                (int)_colorVisionMode);
            PlayerPrefs.Save();
        }
    }
}
