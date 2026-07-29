using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterBiteController : MonoBehaviour
    {
        [SerializeField] private MonsterBalanceConfig _config;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private MonsterTarget _target;

        private float _resolveAt;

        public event Action<MonsterBiteController, MonsterTarget> BiteStarted;
        public event Action<MonsterBiteController, MonsterTarget, MonsterBiteResult> BiteFinished;

        public bool IsPending { get; private set; }
        public MonsterTarget Target => _target;

        public void Configure(
            MonsterBalanceConfig config,
            MonsterSenses senses,
            MonsterTarget target)
        {
            _config = config;
            _senses = senses;
            _target = target;
        }

        public MonsterBiteResult TryBegin(float currentTime)
        {
            if (IsPending)
            {
                return MonsterBiteResult.Pending;
            }

            if (_config == null || _senses == null || _target == null ||
                _target.IsBiteProtected(currentTime) ||
                !_senses.IsTargetInBiteRangeWithLineOfSight())
            {
                return MonsterBiteResult.None;
            }

            IsPending = true;
            _resolveAt = currentTime + _config.BiteWindupSeconds;
            BiteStarted?.Invoke(this, _target);
            return MonsterBiteResult.Pending;
        }

        public MonsterBiteResult Tick(float currentTime)
        {
            if (!IsPending)
            {
                return MonsterBiteResult.None;
            }

            if (currentTime < _resolveAt)
            {
                return MonsterBiteResult.Pending;
            }

            IsPending = false;
            MonsterBiteResult result;
            if (!_senses.IsTargetInBiteRangeWithLineOfSight())
            {
                result = MonsterBiteResult.Miss;
            }
            else if (!_target.TryReceiveBite(this, currentTime, _config.BiteProtectionSeconds))
            {
                result = MonsterBiteResult.Protected;
            }
            else
            {
                result = MonsterBiteResult.Hit;
            }

            BiteFinished?.Invoke(this, _target, result);
            return result;
        }

        public void Cancel()
        {
            IsPending = false;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
