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
        private bool _isExternallyDriven;
        private float _useStartedAt;

        public event Action<AntidoteService> InventoryChanged;
        public event Action<AntidoteService> CodeStateChanged;
        public event Action<AntidoteService> UseStarted;
        public event Action<AntidoteService> UseCancelled;
        public event Action<AntidoteService> UseCompleted;

        public AntidoteBalanceConfig Config => _config;
        public int CarriedCount { get; private set; }
        public bool HasAntidote => CarriedCount > 0;

        /// <summary>유효한 배합 코드를 보유했는지다. 제작 시작의 전제 조건이다(GDD §14.2).</summary>
        public bool HasValidCode { get; private set; }

        /// <summary>
        /// 발급받은 배합 코드 문자열이다. 본인에게만 전송되며 저장되지 않는다(GDD §14.2).
        /// PC 화면을 닫으면 UI 쪽에서 표시를 지운다.
        /// </summary>
        public string IssuedCode { get; private set; } = string.Empty;
        public bool IsUsing { get; private set; }
        public float UseProgressNormalized { get; private set; }

        /// <summary>
        /// 온라인에서는 소지 수량과 치료 확정을 서버가 판정한다.
        /// 이 값이 참이면 사용 완료 시점에 <see cref="UseCompleted"/>만 알리고
        /// 소비와 치료는 <c>NetworkAntidoteInventoryAuthority</c>가 수행한다.
        /// </summary>
        public bool IsExternallyDriven => _isExternallyDriven;

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

        public void SetExternallyDriven(bool isExternallyDriven)
        {
            _isExternallyDriven = isExternallyDriven;
        }

        /// <summary>
        /// 서버가 확정한 배합 코드 보유 여부와 코드 문자열을 반영한다(GDD §14.2).
        /// 유효한 코드가 없으면 제작을 시작할 수 없다. 코드는 본인에게만 전송된다.
        /// </summary>
        public void ApplyAuthoritativeCodeState(bool hasValidCode, string issuedCode)
        {
            var normalizedCode = issuedCode ?? string.Empty;
            if (HasValidCode == hasValidCode && IssuedCode == normalizedCode)
            {
                return;
            }

            HasValidCode = hasValidCode;
            IssuedCode = hasValidCode ? normalizedCode : string.Empty;
            CodeStateChanged?.Invoke(this);
        }

        /// <summary>서버가 확정한 소지 수량을 반영한다.</summary>
        public void ApplyAuthoritativeCarriedCount(int carriedCount)
        {
            var clamped = _config != null
                ? Mathf.Clamp(carriedCount, 0, _config.MaxCarryCount)
                : Mathf.Max(0, carriedCount);
            if (clamped == CarriedCount)
            {
                return;
            }

            CarriedCount = clamped;
            if (!HasAntidote)
            {
                CancelUse();
            }

            InventoryChanged?.Invoke(this);
        }

        public bool TryAddAntidote()
        {
            if (_isExternallyDriven || _config == null ||
                CarriedCount >= _config.MaxCarryCount)
            {
                return false;
            }

            CarriedCount++;
            InventoryChanged?.Invoke(this);
            return true;
        }

        public bool TryRemoveAntidote()
        {
            if (_isExternallyDriven || CarriedCount <= 0)
            {
                return false;
            }

            CarriedCount--;
            if (!HasAntidote)
            {
                CancelUse();
            }

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

            // 온라인에서는 소비와 치료를 서버가 확정한다. 여기서는 완료만 알린다.
            if (_isExternallyDriven)
            {
                IsUsing = false;
                UseProgressNormalized = 0f;
                UseCompleted?.Invoke(this);
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
