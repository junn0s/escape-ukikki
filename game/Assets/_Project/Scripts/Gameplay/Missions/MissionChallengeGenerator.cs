using System;

namespace MonkeyLab.Gameplay.Missions
{
    public static class MissionChallengeGenerator
    {
        public static int[] CreateShuffledOrder(int itemCount, int seed)
        {
            var order = new int[itemCount];
            for (var index = 0; index < itemCount; index++)
            {
                order[index] = index + 1;
            }

            var random = new Random(seed);
            for (var index = order.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (order[index], order[swapIndex]) =
                    (order[swapIndex], order[index]);
            }

            return order;
        }

        public static int[] CreateSampleCategories(
            int sampleCount,
            int categoryCount,
            int seed)
        {
            var categories = new int[sampleCount];
            for (var index = 0; index < categories.Length; index++)
            {
                categories[index] = index % categoryCount + 1;
            }

            var random = new Random(seed);
            for (var index = categories.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (categories[index], categories[swapIndex]) =
                    (categories[swapIndex], categories[index]);
            }

            return categories;
        }
    }
}
