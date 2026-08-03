using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonkeyLab.Gameplay.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputActionAsset _runtimeInputActions;
        private InputActionMap _gameplayMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _interactAction;
        private InputAction _flashlightAction;
        private InputAction _useAntidoteAction;
        private InputAction _cancelAction;
        private InputAction _journalAction;
        private bool _isInitialized;

        public event Action InteractPressed;
        public event Action FlashlightPressed;
        public event Action UseAntidotePressed;
        public event Action CancelPressed;

        /// <summary>Tab. 미션 목록과 전자지도를 여닫는다(GDD §7.2).</summary>
        public event Action JournalPressed;

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
            _useAntidoteAction.performed -= HandleUseAntidote;
            _cancelAction.performed -= HandleCancel;
            _journalAction.performed -= HandleJournal;
            Destroy(_runtimeInputActions);
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

            _runtimeInputActions = Instantiate(_inputActions);
            _gameplayMap = _runtimeInputActions.FindActionMap("Gameplay", true);
            _moveAction = _gameplayMap.FindAction("Move", true);
            _lookAction = _gameplayMap.FindAction("Look", true);
            _interactAction = _gameplayMap.FindAction("Interact", true);
            _flashlightAction = _gameplayMap.FindAction("Flashlight", true);
            _useAntidoteAction = _gameplayMap.FindAction("UseAntidote", true);
            _cancelAction = _gameplayMap.FindAction("Cancel", true);
            _journalAction = _gameplayMap.FindAction("Journal", true);
            _interactAction.performed += HandleInteract;
            _flashlightAction.performed += HandleFlashlight;
            _useAntidoteAction.performed += HandleUseAntidote;
            _cancelAction.performed += HandleCancel;
            _journalAction.performed += HandleJournal;
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

        private void HandleUseAntidote(InputAction.CallbackContext context)
        {
            UseAntidotePressed?.Invoke();
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            CancelPressed?.Invoke();
        }

        private void HandleJournal(InputAction.CallbackContext context)
        {
            JournalPressed?.Invoke();
        }
    }
}
