using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class MonsterTarget : MonoBehaviour
    {
        private static readonly HashSet<MonsterTarget> ActiveTargetSet = new();

        [SerializeField] private bool _isDetectable = true;
        [SerializeField] private bool _canBeInfected = true;

        private float _biteProtectionUntil;

        public event Action<MonsterTarget, MonsterBiteController, bool> Bitten;

        public bool IsDetectable => _isDetectable;
        public bool CanBeInfected => _canBeInfected;
        public int BiteCount { get; private set; }
        public static IEnumerable<MonsterTarget> ActiveTargets => ActiveTargetSet;

        public void Configure(bool isDetectable, bool canBeInfected)
        {
            _isDetectable = isDetectable;
            _canBeInfected = canBeInfected;
            ActiveTargetSet.Add(this);
        }

        public void SetDetectable(bool isDetectable)
        {
            _isDetectable = isDetectable;
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

        private void OnEnable()
        {
            ActiveTargetSet.Add(this);
        }

        private void OnDisable()
        {
            ActiveTargetSet.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveTargetSet.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveTargetSet.Clear();
        }
    }
}
