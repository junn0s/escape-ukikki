using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class FlashlightController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerAimController _aim;
        [SerializeField] private Light _flashlight;
        [SerializeField] private Transform _aimPivot;
        [SerializeField] private GameObject _flashlightVisual;
        [SerializeField] private bool _startsEnabled = true;

        private bool _isSubscribed;

        public void Configure(PlayerInputReader input, Light flashlight, bool startsEnabled)
        {
            Unsubscribe();
            _input = input;
            _flashlight = flashlight;
            _startsEnabled = startsEnabled;
            if (_flashlight != null)
            {
                _flashlight.enabled = _startsEnabled;
            }

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
            if (_flashlightVisual != null)
            {
                _flashlightVisual.SetActive(_startsEnabled);
            }

            ApplyAimRotation();
            Subscribe();
        }

        private void OnEnable()
        {
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
            if (_flashlight != null)
            {
                _flashlight.enabled = !_flashlight.enabled;
            }

            if (_flashlightVisual != null)
            {
                _flashlightVisual.SetActive(!_flashlightVisual.activeSelf);
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
