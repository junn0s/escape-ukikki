using System;

namespace MonkeyLab.Gameplay.Noise
{
    public readonly struct NoiseCandidate
    {
        public NoiseCandidate(NoiseEventData noise, float pathDistance)
        {
            if (pathDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(pathDistance));
            }

            Noise = noise;
            PathDistance = pathDistance;
        }

        public NoiseEventData Noise { get; }
        public float PathDistance { get; }
        public bool IsWithinRadius => PathDistance <= Noise.PathRadius;
    }
}
