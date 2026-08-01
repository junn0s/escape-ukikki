using System;
using MonkeyLab.Gameplay.Monsters;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    public sealed class InfectionService : MonoBehaviour
    {
        [SerializeField] private MonsterTarget _target;
        [SerializeField] private MonsterTierRuntime _monsterTierRuntime;

        private bool _isPaused;
        private bool _isSubscribed;
        private bool _isExternallyDriven;

        public event Action<InfectionService, PlayerLifeState> StateChanged;
        public event Action<InfectionService> InfectionStarted;
        public event Action<InfectionService> InfectionCured;
        public event Action<InfectionService> InfectionExpired;

        public PlayerLifeState State { get; private set; } = PlayerLifeState.AliveHealthy;
        public bool IsInfected => State == PlayerLifeState.AliveInfected;
        public bool IsPaused => _isPaused;
        public float DurationAtBiteSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int ToxicityTierAtBite { get; private set; }
        public bool IsExternallyDriven => _isExternallyDriven;

        public void Configure(MonsterTarget target, MonsterTierRuntime monsterTierRuntime)
        {
            Unsubscribe();
            _target = target;
            _monsterTierRuntime = monsterTierRuntime;
            Subscribe();
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
        }

        public void SetExternallyDriven(bool isExternallyDriven)
        {
            _isExternallyDriven = isExternallyDriven;
        }

        public void ApplyAuthoritativeSnapshot(
            PlayerLifeState state,
            float durationAtBiteSeconds,
            float remainingSeconds,
            int toxicityTierAtBite)
        {
            DurationAtBiteSeconds =
                Mathf.Max(0f, durationAtBiteSeconds);
            RemainingSeconds = Mathf.Clamp(
                remainingSeconds,
                0f,
                DurationAtBiteSeconds);
            ToxicityTierAtBite = Mathf.Max(0, toxicityTierAtBite);
            _target?.SetDetectable(
                state == PlayerLifeState.AliveHealthy);
            SetState(state);
        }

        public void Tick(float deltaTime)
        {
            if (_isExternallyDriven || !IsInfected ||
                _isPaused || deltaTime <= 0f)
            {
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
            if (RemainingSeconds > 0f)
            {
                return;
            }

            _target.SetDetectable(false);
            SetState(PlayerLifeState.DeadGhost);
            InfectionExpired?.Invoke(this);
        }

        /// <summary>
        /// 회의 퇴출로 유령이 된다. 감염 사망과 달리 타이머와 무관하게 즉시 전환한다
        /// (GDD §16.4, §17). 이미 유령이면 아무 일도 하지 않는다.
        /// </summary>
        public bool TryExile()
        {
            if (State == PlayerLifeState.DeadGhost)
            {
                return false;
            }

            RemainingSeconds = 0f;
            _isPaused = false;
            _target.SetDetectable(false);
            SetState(PlayerLifeState.DeadGhost);
            return true;
        }

        public bool TryCure()
        {
            if (!IsInfected)
            {
                return false;
            }

            RemainingSeconds = 0f;
            _isPaused = false;
            _target.SetDetectable(true);
            SetState(PlayerLifeState.AliveHealthy);
            InfectionCured?.Invoke(this);
            return true;
        }

        private void Awake()
        {
            if (_target == null)
            {
                Debug.LogError("[Infection] MonsterTarget reference is missing.", this);
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!_isExternallyDriven)
            {
                Tick(Time.deltaTime);
            }
        }

        private void HandleBitten(
            MonsterTarget target,
            MonsterBiteController source,
            bool canBeInfected)
        {
            if (_isExternallyDriven || !canBeInfected ||
                State != PlayerLifeState.AliveHealthy ||
                _monsterTierRuntime == null)
            {
                return;
            }

            ToxicityTierAtBite = _monsterTierRuntime.ToxicityTier;
            DurationAtBiteSeconds = _monsterTierRuntime.CurrentInfectionDurationSeconds;
            RemainingSeconds = DurationAtBiteSeconds;
            _target.SetDetectable(false);
            SetState(PlayerLifeState.AliveInfected);
            InfectionStarted?.Invoke(this);
        }

        private void SetState(PlayerLifeState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            State = nextState;
            StateChanged?.Invoke(this, nextState);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _target == null)
            {
                return;
            }

            _target.Bitten += HandleBitten;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _target == null)
            {
                return;
            }

            _target.Bitten -= HandleBitten;
            _isSubscribed = false;
        }
    }
}
