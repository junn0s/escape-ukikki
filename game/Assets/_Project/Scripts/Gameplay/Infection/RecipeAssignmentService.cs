using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 라운드 시작 시 생존자별 개인 레시피를 후보 지점에 배치한다.
    /// docs/map-level-design.md §7.2에 따라 생존자마다 서로 다른 후보를 쓴다.
    /// 후보 목록에서 백신실을 제외하는 것은 배치를 만드는 쪽(씬 빌더)의 책임이다.
    /// </summary>
    public static class RecipeAssignmentService
    {
        /// <summary>
        /// 서버에서만 호출한다. 같은 seed와 같은 입력이면 항상 같은 결과를 준다.
        /// </summary>
        /// <returns>후보가 생존자 수보다 적으면 false를 반환하고 배치하지 않는다.</returns>
        public static bool TryAssign(
            IReadOnlyList<ulong> survivorClientIds,
            int candidateCount,
            int seed,
            IDictionary<ulong, int> destination)
        {
            if (survivorClientIds == null || destination == null ||
                candidateCount < survivorClientIds.Count)
            {
                return false;
            }

            destination.Clear();
            if (survivorClientIds.Count == 0)
            {
                return true;
            }

            var candidates = new int[candidateCount];
            for (var index = 0; index < candidateCount; index++)
            {
                candidates[index] = index;
            }

            // 결정적 셔플이라 호스트와 EditMode 테스트가 같은 배치를 만든다.
            var random = (uint)(seed == 0 ? 1 : seed);
            for (var index = candidateCount - 1; index > 0; index--)
            {
                random = NextRandom(random);
                var swapIndex = (int)(random % (uint)(index + 1));
                (candidates[index], candidates[swapIndex]) =
                    (candidates[swapIndex], candidates[index]);
            }

            for (var index = 0; index < survivorClientIds.Count; index++)
            {
                destination[survivorClientIds[index]] = candidates[index];
            }

            return true;
        }

        private static uint NextRandom(uint state)
        {
            // Xorshift32. Unity 난수를 쓰지 않아 서버·테스트 결과가 일치한다.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
