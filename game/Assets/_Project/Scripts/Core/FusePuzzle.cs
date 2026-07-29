using System;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 퓨즈 순서 맞추기 미니게임의 순수 로직.
    /// GDD §10.2 "퓨즈를 표시된 순서에 맞게 삽입", 실패하면 Medium 소음.
    ///
    /// MonoBehaviour와 분리해 Unity 없이 검증한다
    /// (technical-design-document.md §11 IMissionInstance 형태).
    /// </summary>
    public sealed class FusePuzzle
    {
        private readonly int[] _correctOrder;
        private int _nextIndex;

        public FusePuzzle(int[] correctOrder)
        {
            if (correctOrder == null || correctOrder.Length == 0)
            {
                throw new ArgumentException("퓨즈 순서가 비어 있다.", nameof(correctOrder));
            }

            _correctOrder = (int[])correctOrder.Clone();
        }

        /// <summary>퓨즈 개수.</summary>
        public int SlotCount => _correctOrder.Length;

        /// <summary>지금까지 올바르게 넣은 개수.</summary>
        public int Progress => _nextIndex;

        public bool IsComplete { get; private set; }

        /// <summary>직전 입력으로 실패했는지. 실패 시 진행이 초기화된다.</summary>
        public bool HasFailedLastInput { get; private set; }

        /// <summary>다음에 넣어야 할 퓨즈 번호. 완료 상태면 -1.</summary>
        public int ExpectedFuseId => IsComplete ? -1 : _correctOrder[_nextIndex];

        /// <summary>
        /// 퓨즈 하나를 넣는다.
        /// </summary>
        /// <returns>올바른 순서였으면 true</returns>
        public bool TryInsert(int fuseId)
        {
            if (IsComplete)
            {
                HasFailedLastInput = false;
                return false;
            }

            if (fuseId != _correctOrder[_nextIndex])
            {
                // 실패하면 진행 상황을 초기화한다 (GDD §10.1).
                _nextIndex = 0;
                HasFailedLastInput = true;
                return false;
            }

            _nextIndex++;
            HasFailedLastInput = false;

            if (_nextIndex >= _correctOrder.Length)
            {
                IsComplete = true;
            }

            return true;
        }

        /// <summary>미션을 중단했을 때 호출한다. 진행 상황을 버린다.</summary>
        public void Reset()
        {
            _nextIndex = 0;
            IsComplete = false;
            HasFailedLastInput = false;
        }
    }
}
