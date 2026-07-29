using System;
using MonkeyLab.Gameplay.Player;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    public sealed class AntidoteService : MonoBehaviour
    {
        private const float MovementCancelThreshold = 0.01f;

        [SerializeField] private AntidoteBalanceConfig _config;
        [SerializeField] private InfectionService _infectionService;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMotor _motor;

        private bool _isSubscribed;
        private float _useStartedAt;

        public event Action<AntidoteService> InventoryChanged;
        public event Action<AntidoteService> UseStarted;
        public event Action<AntidoteService> UseCancelled;
        public event Action<AntidoteService> UseCompleted;

        public AntidoteBalanceConfig Config => _config;
        public int CarriedCount { get; private set; }
        public bool HasAntidote => CarriedCount > 0;
        public bool IsUsing { get; private set; }
        public float UseProgressNormalized { get; private set; }

        public void Configure(
            AntidoteBalanceConfig config,
            InfectionService infectionService,
            PlayerInputReader input,
            PlayerMotor motor)
        {
            Unsubscribe();
            _config = config;
            _infectionService = infectionService;
            _input = input;
            _motor = motor;
            Subscribe();
        }

        public bool TryAddAntidote()
        {
            if (_config == null || CarriedCount >= _config.MaxCarryCount)
            {
                return false;
            }

            CarriedCount++;
            InventoryChanged?.Invoke(this);
            return true;
        }

        public bool TryBeginUse(float currentTime)
        {
            if (IsUsing || !HasAntidote || _infectionService == null ||
                !_infectionService.IsInfected || _motor == null || !_motor.IsMovementEnabled)
            {
                return false;
            }

            IsUsing = true;
            UseProgressNormalized = 0f;
            _useStartedAt = currentTime;
            UseStarted?.Invoke(this);
            return true;
        }

        public void TickUse(float currentTime, Vector2 movementInput)
        {
            if (!IsUsing)
            {
                return;
            }

            if (movementInput.sqrMagnitude > MovementCancelThreshold * MovementCancelThreshold)
            {
                CancelUse();
                return;
            }

            var duration = _config.UseDurationSeconds;
            UseProgressNormalized = Mathf.Clamp01((currentTime - _useStartedAt) / duration);
            if (UseProgressNormalized < 1f)
            {
                return;
            }

            if (!_infectionService.TryCure())
            {
                CancelUse();
                return;
            }

            IsUsing = false;
            UseProgressNormalized = 0f;
            CarriedCount--;
            InventoryChanged?.Invoke(this);
            UseCompleted?.Invoke(this);
        }

        public void CancelUse()
        {
            if (!IsUsing)
            {
                return;
            }

            IsUsing = false;
            UseProgressNormalized = 0f;
            UseCancelled?.Invoke(this);
        }

        private void Awake()
        {
            if (_config == null || _infectionService == null || _input == null || _motor == null)
            {
                Debug.LogError("[Antidote] Required references are missing.", this);
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelUse();
        }

        private void Update()
        {
            TickUse(Time.time, _input?.Move ?? Vector2.zero);
        }

        private void HandleUsePressed()
        {
            if (_input.Move.sqrMagnitude > MovementCancelThreshold * MovementCancelThreshold)
            {
                return;
            }

            TryBeginUse(Time.time);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _input == null)
            {
                return;
            }

            _input.UseAntidotePressed += HandleUsePressed;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _input == null)
            {
                return;
            }

            _input.UseAntidotePressed -= HandleUsePressed;
            _isSubscribed = false;
        }
    }
}
