using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterTarget : MonoBehaviour
    {
        [SerializeField] private bool _isDetectable = true;
        [SerializeField] private bool _canBeInfected = true;

        private float _biteProtectionUntil;

        public event Action<MonsterTarget, MonsterBiteController, bool> Bitten;

        public bool IsDetectable => _isDetectable;
        public bool CanBeInfected => _canBeInfected;
        public int BiteCount { get; private set; }

        public void Configure(bool isDetectable, bool canBeInfected)
        {
            _isDetectable = isDetectable;
            _canBeInfected = canBeInfected;
        }

        public bool IsBiteProtected(float currentTime)
        {
            return currentTime < _biteProtectionUntil;
        }

        public bool TryReceiveBite(
            MonsterBiteController source,
            float currentTime,
            float protectionSeconds)
        {
            if (!_isDetectable || IsBiteProtected(currentTime))
            {
                return false;
            }

            _biteProtectionUntil = currentTime + protectionSeconds;
            BiteCount++;
            Bitten?.Invoke(this, source, _canBeInfected);
            return true;
        }

        private void Awake()
        {
            _biteProtectionUntil = float.NegativeInfinity;
            BiteCount = 0;
        }
    }
}
