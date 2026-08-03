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

        /// <summary>
        /// 물림 없이 보호를 부여한다. 회의가 끝나 탐색이 재개될 때,
        /// 회의 직전에 옆에 서 있던 괴물이 즉시 물어 버리는 상황을 막는다
        /// (docs/balance-and-telemetry.md §2 "회의 종료 물기 보호 2초",
        /// docs/qa-and-playtest-plan.md §4.9).
        /// 이미 더 긴 보호가 걸려 있으면 줄이지 않는다.
        /// </summary>
        public void ApplyBiteProtection(
            float currentTime,
            float protectionSeconds)
        {
            if (protectionSeconds <= 0f)
            {
                return;
            }

            var protectedUntil = currentTime + protectionSeconds;
            if (protectedUntil > _biteProtectionUntil)
            {
                _biteProtectionUntil = protectedUntil;
            }
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
