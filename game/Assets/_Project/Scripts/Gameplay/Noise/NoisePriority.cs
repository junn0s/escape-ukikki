using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Noise
{
    public static class NoisePriority
    {
        private const float DistanceEqualityTolerance = 0.001f;
        private const double TimeEqualityTolerance = 0.0001d;

        public static bool TrySelectBest(
            IReadOnlyList<NoiseCandidate> candidates,
            out NoiseCandidate bestCandidate)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            bestCandidate = default;
            var hasCandidate = false;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.IsWithinRadius)
                {
                    continue;
                }

                if (!hasCandidate || HasHigherPriority(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        public static bool HasHigherPriority(NoiseCandidate candidate, NoiseCandidate current)
        {
            var distanceDifference = candidate.PathDistance - current.PathDistance;
            if (Math.Abs(distanceDifference) > DistanceEqualityTolerance)
            {
                return distanceDifference < 0f;
            }

            if (candidate.Noise.Intensity != current.Noise.Intensity)
            {
                return candidate.Noise.Intensity > current.Noise.Intensity;
            }

            var timeDifference = candidate.Noise.CreatedTime - current.Noise.CreatedTime;
            if (Math.Abs(timeDifference) > TimeEqualityTolerance)
            {
                return timeDifference > 0d;
            }

            return candidate.Noise.NoiseId < current.Noise.NoiseId;
        }
    }
}
