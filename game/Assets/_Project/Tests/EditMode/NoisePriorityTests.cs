using MonkeyLab.Gameplay.Noise;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class NoisePriorityTests
    {
        [Test]
        public void ShorterPathWinsBeforeIntensityAndTime()
        {
            var farther = Candidate(1, 10f, NoiseIntensity.Large, 20d);
            var nearer = Candidate(2, 6f, NoiseIntensity.Small, 10d);

            var selected = NoisePriority.TrySelectBest(new[] { farther, nearer }, out var best);

            Assert.That(selected, Is.True);
            Assert.That(best.Noise.NoiseId, Is.EqualTo(2));
        }

        [Test]
        public void StrongerNoiseWinsWhenDistanceTies()
        {
            var small = Candidate(1, 6f, NoiseIntensity.Small, 20d);
            var medium = Candidate(2, 6f, NoiseIntensity.Medium, 10d);

            NoisePriority.TrySelectBest(new[] { small, medium }, out var best);

            Assert.That(best.Noise.NoiseId, Is.EqualTo(2));
        }

        [Test]
        public void NewerNoiseWinsWhenDistanceAndIntensityTie()
        {
            var older = Candidate(1, 6f, NoiseIntensity.Medium, 10d);
            var newer = Candidate(2, 6f, NoiseIntensity.Medium, 20d);

            NoisePriority.TrySelectBest(new[] { older, newer }, out var best);

            Assert.That(best.Noise.NoiseId, Is.EqualTo(2));
        }

        [Test]
        public void LowerIdWinsFinalTie()
        {
            var largerId = Candidate(2, 6f, NoiseIntensity.Medium, 20d);
            var smallerId = Candidate(1, 6f, NoiseIntensity.Medium, 20d);

            NoisePriority.TrySelectBest(new[] { largerId, smallerId }, out var best);

            Assert.That(best.Noise.NoiseId, Is.EqualTo(1));
        }

        [Test]
        public void CandidateOutsidePathRadiusIsIgnored()
        {
            var outside = Candidate(1, 15f, NoiseIntensity.Medium, 20d);

            var selected = NoisePriority.TrySelectBest(new[] { outside }, out _);

            Assert.That(selected, Is.False);
        }

        private static NoiseCandidate Candidate(
            long id,
            float pathDistance,
            NoiseIntensity intensity,
            double createdTime)
        {
            var radius = intensity switch
            {
                NoiseIntensity.Small => 8f,
                NoiseIntensity.Medium => 14f,
                _ => 24f
            };
            var noise = new NoiseEventData(
                id,
                NoiseSourceType.MissionFailure,
                Vector3.zero,
                "power",
                radius,
                intensity,
                createdTime,
                0f);
            return new NoiseCandidate(noise, pathDistance);
        }
    }
}
