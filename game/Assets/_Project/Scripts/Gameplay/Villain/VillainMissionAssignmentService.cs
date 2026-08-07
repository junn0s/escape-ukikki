using System;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 라운드 시작 시 빌런에게 배정할 미션 4종을 6종 중 무작위로 고른다(GDD §13.2).
    /// </summary>
    public static class VillainMissionAssignmentService
    {
        public const int AssignedMissionCount = 4;
        public const int TotalMissionCount = 6;

        /// <summary>
        /// 서버에서만 호출한다. 같은 seed면 항상 같은 배정을 준다(EditMode 테스트용).
        /// </summary>
        public static VillainMissionKind[] Assign(int seed)
        {
            var allKinds = (VillainMissionKind[])Enum.GetValues(
                typeof(VillainMissionKind));
            var random = (uint)(seed == 0 ? 1 : seed);
            for (var index = allKinds.Length - 1; index > 0; index--)
            {
                random = NextRandom(random);
                var swapIndex = (int)(random % (uint)(index + 1));
                (allKinds[index], allKinds[swapIndex]) =
                    (allKinds[swapIndex], allKinds[index]);
            }

            var assigned = new VillainMissionKind[AssignedMissionCount];
            Array.Copy(allKinds, assigned, AssignedMissionCount);
            return assigned;
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
