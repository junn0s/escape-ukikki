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
        private MonsterTarget _activeTarget;

        public event Action<MonsterBiteController, MonsterTarget> BiteStarted;
        public event Action<MonsterBiteController, MonsterTarget, MonsterBiteResult> BiteFinished;

        public bool IsPending { get; private set; }
        public MonsterTarget Target => _activeTarget != null
            ? _activeTarget
            : _senses != null
                ? _senses.Target
                : _target;

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

            var target = _senses != null ? _senses.Target : _target;
            if (_config == null || _senses == null || target == null ||
                target.IsBiteProtected(currentTime) ||
                !_senses.IsTargetInBiteRange(target))
            {
                return MonsterBiteResult.None;
            }

            _activeTarget = target;
            IsPending = true;
            _resolveAt = currentTime + _config.BiteWindupSeconds;
            BiteStarted?.Invoke(this, _activeTarget);
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
            var resolvedTarget = _activeTarget;
            if (resolvedTarget == null ||
                !_senses.IsTargetInBiteRange(resolvedTarget))
            {
                result = MonsterBiteResult.Miss;
            }
            else if (!resolvedTarget.TryReceiveBite(
                         this,
                         currentTime,
                         _config.BiteProtectionSeconds))
            {
                result = MonsterBiteResult.Protected;
            }
            else
            {
                result = MonsterBiteResult.Hit;
            }

            BiteFinished?.Invoke(this, resolvedTarget, result);
            _activeTarget = null;
            return result;
        }

        public void Cancel()
        {
            IsPending = false;
            _activeTarget = null;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
