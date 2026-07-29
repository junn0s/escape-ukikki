using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonkeyLab.Gameplay.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputActionMap _gameplayMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _interactAction;
        private InputAction _flashlightAction;
        private bool _isInitialized;

        public event Action InteractPressed;
        public event Action FlashlightPressed;

        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 PointerPosition => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        public void Configure(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            _gameplayMap?.Enable();
        }

        private void OnDisable()
        {
            _gameplayMap?.Disable();
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            _interactAction.performed -= HandleInteract;
            _flashlightAction.performed -= HandleFlashlight;
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_inputActions == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset is missing.", this);
                enabled = false;
                return;
            }

            _gameplayMap = _inputActions.FindActionMap("Gameplay", true);
            _moveAction = _gameplayMap.FindAction("Move", true);
            _lookAction = _gameplayMap.FindAction("Look", true);
            _interactAction = _gameplayMap.FindAction("Interact", true);
            _flashlightAction = _gameplayMap.FindAction("Flashlight", true);
            _interactAction.performed += HandleInteract;
            _flashlightAction.performed += HandleFlashlight;
            _isInitialized = true;
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            InteractPressed?.Invoke();
        }

        private void HandleFlashlight(InputAction.CallbackContext context)
        {
            FlashlightPressed?.Invoke();
        }
    }
}
