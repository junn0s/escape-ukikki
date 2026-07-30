using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    public sealed class LocalRoundPhasePrototype : MonoBehaviour
    {
        [SerializeField] private RoundBalanceConfig _config;

        private float _roundStartedAt;
        private bool _isInitialized;
        private bool _isGracePeriodSkipped;
        private bool _hasAuthoritativePhase;
        private RoundPhase _authoritativePhase;
        private float _authoritativeRemainingPhaseSeconds;

        public RoundBalanceConfig Config => _config;
        public bool IsMonsterAggressionEnabled =>
            _hasAuthoritativePhase
                ? _authoritativePhase == RoundPhase.Exploration
                : _isInitialized && _config != null &&
                  RemainingGracePeriodSeconds <= 0f;
        public RoundPhase CurrentPhase
        {
            get
            {
                if (_hasAuthoritativePhase)
                {
                    return _authoritativePhase;
                }

                return IsMonsterAggressionEnabled
                    ? RoundPhase.Exploration
                    : RoundPhase.GracePeriod;
            }
        }
        public float RemainingGracePeriodSeconds
        {
            get
            {
                if (_hasAuthoritativePhase)
                {
                    return _authoritativePhase == RoundPhase.GracePeriod
                        ? _authoritativeRemainingPhaseSeconds
                        : 0f;
                }

                if (!_isInitialized || _config == null || _isGracePeriodSkipped)
                {
                    return 0f;
                }

                return Mathf.Max(
                    0f,
                    _config.InitialGracePeriodSeconds - (Time.time - _roundStartedAt));
            }
        }

        public void Configure(RoundBalanceConfig config)
        {
            _config = config;
        }

        public void ResetForRound()
        {
            _roundStartedAt = Time.time;
            _isInitialized = true;
            _isGracePeriodSkipped = false;
        }

        public void ApplyAuthoritativePhase(
            RoundPhase phase,
            float remainingPhaseSeconds)
        {
            _hasAuthoritativePhase = true;
            _authoritativePhase = phase;
            _authoritativeRemainingPhaseSeconds =
                Mathf.Max(0f, remainingPhaseSeconds);
            _isInitialized = true;
        }

        public void ClearAuthoritativePhase()
        {
            _hasAuthoritativePhase = false;
            ResetForRound();
        }

        public void SkipGracePeriodForDevelopment()
        {
            if (!UnityEngine.Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            _isInitialized = true;
            _isGracePeriodSkipped = true;
        }

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[Round] RoundBalanceConfig is missing.", this);
                return;
            }

            ResetForRound();
        }
    }
}
