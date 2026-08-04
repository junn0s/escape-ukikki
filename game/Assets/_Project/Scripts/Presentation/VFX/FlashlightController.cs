using System;
using MonkeyLab.Gameplay.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class FlashlightController : MonoBehaviour
    {
        private const float SilhouetteGlowIntensity = 0.006f;
        private const float SilhouetteGlowRadius = 0.5f;

        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerAimController _aim;
        [SerializeField] private Light _flashlight;
        [SerializeField] private Transform _aimPivot;
        [SerializeField] private GameObject _flashlightVisual;
        [SerializeField] private Light2D _personalGlow;
        [SerializeField] private bool _startsEnabled = true;

        private bool _isSubscribed;
        private bool _isInitialized;
        private bool _isFlashlightEnabled;

        public event Action<bool> FlashlightStateChanged;

        public bool IsFlashlightEnabled => _isInitialized
            ? _isFlashlightEnabled
            : _startsEnabled;

        public void Configure(PlayerInputReader input, Light flashlight, bool startsEnabled)
        {
            Unsubscribe();
            _input = input;
            _flashlight = flashlight;
            _startsEnabled = startsEnabled;
            SetFlashlightEnabled(_startsEnabled, notify: false);

            Subscribe();
        }

        public void Configure(
            PlayerInputReader input,
            PlayerAimController aim,
            Transform aimPivot,
            GameObject flashlightVisual,
            bool startsEnabled)
        {
            Unsubscribe();
            _input = input;
            _aim = aim;
            _aimPivot = aimPivot;
            _flashlightVisual = flashlightVisual;
            _startsEnabled = startsEnabled;
            SetFlashlightEnabled(_startsEnabled, notify: false);

            ApplyAimRotation();
            Subscribe();
        }

        /// <summary>
        /// 소등 시 실루엣용 개인등을 연결한다. GDD 1.6부터 손전등은 감지 조건이
        /// 아니므로 <c>MonsterTarget</c>은 더 이상 필요하지 않다.
        /// </summary>
        public void BindStealthVisibility(Light2D personalGlow)
        {
            _personalGlow = personalGlow;
            SetFlashlightEnabled(IsFlashlightEnabled, notify: false);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            ApplyAimRotation();
        }

        private void Toggle()
        {
            SetFlashlightEnabled(!IsFlashlightEnabled, notify: true);
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                SetFlashlightEnabled(_startsEnabled, notify: false);
            }
        }

        private void SetFlashlightEnabled(bool isEnabled, bool notify)
        {
            var changed = !_isInitialized ||
                          _isFlashlightEnabled != isEnabled;
            _isInitialized = true;
            _isFlashlightEnabled = isEnabled;

            if (_flashlight != null)
            {
                _flashlight.enabled = isEnabled;
            }

            if (_flashlightVisual != null)
            {
                _flashlightVisual.SetActive(isEnabled);
            }

            if (_personalGlow != null)
            {
                _personalGlow.intensity = SilhouetteGlowIntensity;
                _personalGlow.pointLightOuterRadius = SilhouetteGlowRadius;
            }

            if (notify && changed)
            {
                FlashlightStateChanged?.Invoke(isEnabled);
            }
        }

        private void ApplyAimRotation()
        {
            if (_aimPivot == null || _aim == null)
            {
                return;
            }

            _aimPivot.localRotation = Quaternion.Euler(
                0f,
                0f,
                _aim.AimAngleDegrees);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _input == null)
            {
                return;
            }

            _input.FlashlightPressed += Toggle;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _input == null)
            {
                return;
            }

            _input.FlashlightPressed -= Toggle;
            _isSubscribed = false;
        }
    }
}
