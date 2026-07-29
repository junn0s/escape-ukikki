using MonkeyLab.Core;
using MonkeyLab.Gameplay.Infection;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    /// <summary>
    /// 물기 판정. 준비 시간을 두고, 판정 시점에 거리를 다시 확인한 뒤 감염을 적용한다.
    ///
    /// SDD §10.3: 애니메이션 이벤트가 아니라 서버 타이밍 데이터로 판정한다.
    /// M1은 로컬이지만 같은 구조를 유지해 M3에서 서버로 옮기기 쉽게 한다.
    /// </summary>
    public sealed class MonsterBiteController : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;
        [SerializeField] private MonsterSenses _senses;
        [SerializeField] private Transform _player;

        private float _nextBiteAllowedTime;
        private float _windupEndTime;
        private bool _isWindingUp;

        /// <summary>물기 애니메이션을 시작할 때 발생.</summary>
        public event System.Action BiteStarted;

        /// <summary>판정이 성립해 감염이 적용됐을 때 발생.</summary>
        public event System.Action BiteLanded;

        private void Awake()
        {
            if (_balance == null || _senses == null)
            {
                Debug.LogError($"[{nameof(MonsterBiteController)}] 필수 참조 미할당", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                return;
            }

            if (_isWindingUp)
            {
                ResolveWindup();
                return;
            }

            TryStartBite();
        }

        private void TryStartBite()
        {
            if (Time.time < _nextBiteAllowedTime)
            {
                return;
            }

            if (!IsWithinBiteRange())
            {
                return;
            }

            _isWindingUp = true;
            _windupEndTime = Time.time + _balance.BiteWindupSeconds;

            BiteStarted?.Invoke();
            Debug.Log("[Monster] 물기 준비");
        }

        private void ResolveWindup()
        {
            if (Time.time < _windupEndTime)
            {
                return;
            }

            _isWindingUp = false;
            _nextBiteAllowedTime = Time.time + _balance.BiteRecoverySeconds;

            // 판정 프레임에 대상이 여전히 범위 안이어야 감염이 적용된다 (SDD §10.3).
            if (!IsWithinBiteRange())
            {
                Debug.Log("[Monster] 물기 빗나감 — 대상이 범위를 벗어남");
                return;
            }

            if (!_player.TryGetComponent(out PlayerInfection infection))
            {
                return;
            }

            if (infection.TryBite())
            {
                BiteLanded?.Invoke();
            }
        }

        private bool IsWithinBiteRange()
        {
            float range = _balance.BiteRange;
            return (_player.position - transform.position).sqrMagnitude <= range * range;
        }
    }
}
