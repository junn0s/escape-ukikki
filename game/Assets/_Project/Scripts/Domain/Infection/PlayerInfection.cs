using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 플레이어 한 명의 감염 상태를 씬에 연결한다.
    /// 실제 규칙은 Core의 InfectionState가 가지고, 여기서는 시간 진행과 이벤트만 다룬다.
    ///
    /// GDD §14.1 / SDD §13
    /// </summary>
    public sealed class PlayerInfection : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;

        [Tooltip("현재 독성 강화 단계 (0~2). 물린 시점 값으로 제한시간이 고정된다")]
        [SerializeField] private int _toxicityLevel;

        private readonly InfectionState _state = new();
        private float _biteProtectionUntil;

        public bool IsInfected => _state.IsInfected;
        public bool IsDead => _state.IsDead;
        public float RemainingSeconds => _state.RemainingSeconds;

        /// <summary>회의 중에는 타이머가 멈춘다 (GDD §14.1).</summary>
        public bool IsTimerPaused { get; set; }

        public int ToxicityLevel
        {
            get => _toxicityLevel;
            set => _toxicityLevel = Mathf.Clamp(value, 0, 2);
        }

        /// <summary>감염이 시작될 때 (남은 시간).</summary>
        public event System.Action<float> Infected;

        /// <summary>감염이 해제될 때.</summary>
        public event System.Action Cured;

        /// <summary>타이머가 0이 되어 사망할 때.</summary>
        public event System.Action Died;

        private void Awake()
        {
            if (_balance == null)
            {
                Debug.LogError($"[{nameof(PlayerInfection)}] {nameof(_balance)} 미할당", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (IsTimerPaused)
            {
                return;
            }

            if (_state.Tick(Time.deltaTime))
            {
                Debug.Log("[Infection] 감염 타이머 종료 — 사망");
                Died?.Invoke();
            }
        }

        /// <summary>
        /// 괴물에게 물렸을 때 호출한다.
        /// 물기 보호 중이거나 이미 감염 상태면 아무 일도 일어나지 않는다.
        /// </summary>
        /// <returns>이번 호출로 새로 감염됐으면 true</returns>
        public bool TryBite()
        {
            if (Time.time < _biteProtectionUntil)
            {
                return false;
            }

            // 물린 시점의 독성 단계로 제한시간을 고정한다.
            float duration = _balance.GetInfectionSeconds(_toxicityLevel);

            if (!_state.TryInfect(duration))
            {
                // 이미 감염 중이면 타이머를 건드리지 않되 보호 시간은 준다.
                _biteProtectionUntil = Time.time + _balance.VictimBiteProtectionSeconds;
                return false;
            }

            _biteProtectionUntil = Time.time + _balance.VictimBiteProtectionSeconds;

            Debug.Log($"[Infection] 감염 시작 — {duration}초 (독성 단계 {_toxicityLevel})");
            Infected?.Invoke(duration);
            return true;
        }

        /// <summary>해독제를 사용한다.</summary>
        public bool TryCure()
        {
            if (!_state.TryCure())
            {
                return false;
            }

            Debug.Log("[Infection] 치료 완료");
            Cured?.Invoke();
            return true;
        }
    }
}
