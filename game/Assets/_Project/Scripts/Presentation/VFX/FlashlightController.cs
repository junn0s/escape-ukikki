using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class FlashlightController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Light _flashlight;
        [SerializeField] private bool _startsEnabled = true;

        public void Configure(PlayerInputReader input, Light flashlight, bool startsEnabled)
        {
            _input = input;
            _flashlight = flashlight;
            _startsEnabled = startsEnabled;
            if (_flashlight != null)
            {
                _flashlight.enabled = _startsEnabled;
            }
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.FlashlightPressed += Toggle;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.FlashlightPressed -= Toggle;
            }
        }

        private void Toggle()
        {
            if (_flashlight != null)
            {
                _flashlight.enabled = !_flashlight.enabled;
            }
        }
    }
}
