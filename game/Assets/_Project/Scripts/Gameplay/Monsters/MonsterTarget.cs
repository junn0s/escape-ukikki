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
        [SerializeField] private bool _isIlluminated = true;
        [SerializeField] private bool _isMovingAudibly;

        private float _biteProtectionUntil;

        public event Action<MonsterTarget, MonsterBiteController, bool> Bitten;
        public event Action<MonsterTarget, MonsterBiteController> BitePresented;

        public bool IsDetectable => _isDetectable;
        public bool CanBeInfected => _canBeInfected;
        public bool IsIlluminated => _isIlluminated;
        public bool IsMovingAudibly => _isMovingAudibly;
        public bool IsExposedToProximity =>
            _isIlluminated || _isMovingAudibly;
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

        /// <summary>
        /// 평상시 근접 감지에 사용하는 손전등 노출 상태다.
        /// 소음 현장 급습은 이 값과 무관하게 판정한다(GDD §12.1).
        /// </summary>
        public void SetIlluminated(bool isIlluminated)
        {
            _isIlluminated = isIlluminated;
        }

        /// <summary>
        /// 이동 중 발걸음으로 평상시 근접 감지에 노출되는 상태다.
        /// 정지 판정 뒤 해제하며, 월드 소음 사건은 별도로 발행하지 않는다.
        /// </summary>
        public void SetMovingAudibly(bool isMovingAudibly)
        {
            _isMovingAudibly = isMovingAudibly;
        }

        public bool CanBeDetectedBy(MonsterDetectionType detectionType)
        {
            return _isDetectable &&
                   (detectionType == MonsterDetectionType.NoiseAmbush ||
                    IsExposedToProximity);
        }

        /// <summary>
        /// 괴물의 감지와 물림 연출은 유지하되, 감염만 선택적으로 막는다
        /// (GDD §5.2, SDD §10.3). 이 값을 역할 배정 후 권한 정보의 유일한 원본으로 사용한다.
        /// </summary>
        public void SetCanBeInfected(bool canBeInfected)
        {
            _canBeInfected = canBeInfected;
        }

        /// <summary>
        /// 서버가 확정한 물림 시각 사건을 원격 클라이언트에 재생한다.
        /// 감염, 보호 시간, 서버 이미 판정했으므로 여기서 건드리지 않는다.
        /// </summary>
        public void PresentReplicatedBite()
        {
            BitePresented?.Invoke(this, null);
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
            BitePresented?.Invoke(this, source);
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
