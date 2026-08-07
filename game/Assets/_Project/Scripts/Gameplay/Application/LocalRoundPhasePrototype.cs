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
        /// <summary>
        /// 회의 중에는 공격뿐 아니라 순찰·추격·물기 준비 시간까지 전부 멈춘다
        /// (system-design-document.md §3).
        /// </summary>
        public bool IsWorldSimulationPaused =>
            _hasAuthoritativePhase &&
            _authoritativePhase is RoundPhase.MeetingDiscussion or
                RoundPhase.MeetingVote or RoundPhase.MeetingResult;
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
