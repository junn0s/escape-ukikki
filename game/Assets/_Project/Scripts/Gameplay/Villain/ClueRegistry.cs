using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 라운드 동안 생성된 현장 단서를 보관한다.
    /// docs/game-design-document.md §15.1에 따라 한 번 생성된 단서는
    /// 라운드가 끝날 때까지 절대 비활성화되지 않는다.
    /// 자동 소멸 타이머를 두지 않는 것이 이 타입의 핵심 계약이다.
    /// </summary>
    public sealed class ClueRegistry
    {
        private readonly Dictionary<int, ClueState> _states = new();

        public event Action<int, ClueKind, ClueState> ClueStateChanged;

        public int ActiveClueCount => _states.Count;

        public ClueState GetState(int clueId)
        {
            return _states.TryGetValue(clueId, out var state)
                ? state
                : ClueState.Inactive;
        }

        public bool IsActive(int clueId)
        {
            return GetState(clueId) != ClueState.Inactive;
        }

        /// <summary>
        /// 단서를 활성화한다. 이미 활성인 단서는 상태를 되돌리지 않고 false를 반환한다.
        /// 같은 축을 2회 강화하면 두 번째 위치에 별도 단서가 생기므로
        /// 단서마다 고유한 clueId를 쓴다(SDD §14.2).
        /// </summary>
        public bool TryActivate(int clueId, ClueKind kind)
        {
            if (_states.ContainsKey(clueId))
            {
                return false;
            }

            _states[clueId] = ClueState.ActiveUninspected;
            ClueStateChanged?.Invoke(
                clueId,
                kind,
                ClueState.ActiveUninspected);
            return true;
        }

        /// <summary>
        /// 조사 표시를 남긴다. 활성 상태가 아니면 아무 일도 하지 않는다.
        /// 조사는 통계용이며 단서를 사라지게 하지 않는다.
        /// </summary>
        public bool TryMarkInspected(int clueId, ClueKind kind)
        {
            if (!_states.TryGetValue(clueId, out var state) ||
                state != ClueState.ActiveUninspected)
            {
                return false;
            }

            _states[clueId] = ClueState.ActiveInspected;
            ClueStateChanged?.Invoke(clueId, kind, ClueState.ActiveInspected);
            return true;
        }

        public int CountByState(ClueState state)
        {
            var count = 0;
            foreach (var pair in _states)
            {
                if (pair.Value == state)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>라운드 종료 시에만 호출한다.</summary>
        public void ResetForNewRound()
        {
            _states.Clear();
        }
    }
}
