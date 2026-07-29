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

        public void Tick(float deltaTime)
        {
            if (!IsInfected || _isPaused || deltaTime <= 0f)
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

        public bool TryCure()
        {
            if (!IsInfected)
            {
                return false;
            }

            RemainingSeconds = 0f;
            _isPaused = false;
            SetState(PlayerLifeState.AliveHealthy);
            InfectionCured?.Invoke(this);
            return true;
        }

        private void Awake()
        {
            if (_target == null || _monsterTierRuntime == null)
            {
                Debug.LogError("[Infection] Required references are missing.", this);
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
            Tick(Time.deltaTime);
        }

        private void HandleBitten(
            MonsterTarget target,
            MonsterBiteController source,
            bool canBeInfected)
        {
            if (!canBeInfected || State != PlayerLifeState.AliveHealthy ||
                _monsterTierRuntime == null)
            {
                return;
            }

            ToxicityTierAtBite = _monsterTierRuntime.ToxicityTier;
            DurationAtBiteSeconds = _monsterTierRuntime.CurrentInfectionDurationSeconds;
            RemainingSeconds = DurationAtBiteSeconds;
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
