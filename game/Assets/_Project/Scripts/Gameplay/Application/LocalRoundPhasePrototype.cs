using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    public sealed class LocalRoundPhasePrototype : MonoBehaviour
    {
        [SerializeField] private RoundBalanceConfig _config;

        private float _roundStartedAt;
        private bool _isInitialized;

        public RoundBalanceConfig Config => _config;
        public bool IsMonsterAggressionEnabled =>
            _isInitialized && _config != null && RemainingGracePeriodSeconds <= 0f;
        public float RemainingGracePeriodSeconds => !_isInitialized || _config == null
            ? 0f
            : Mathf.Max(0f, _config.InitialGracePeriodSeconds - (Time.time - _roundStartedAt));

        public void Configure(RoundBalanceConfig config)
        {
            _config = config;
        }

        public void ResetForRound()
        {
            _roundStartedAt = Time.time;
            _isInitialized = true;
        }

        public void SkipGracePeriodForDevelopment()
        {
            if (!UnityEngine.Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            _roundStartedAt = Time.time - (_config?.InitialGracePeriodSeconds ?? 0f);
            _isInitialized = true;
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
