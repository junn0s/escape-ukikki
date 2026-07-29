using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Noise
{
    public readonly struct NoiseEventData
    {
        public NoiseEventData(
            long noiseId,
            NoiseSourceType sourceType,
            Vector3 worldPosition,
            string roomId,
            float pathRadius,
            NoiseIntensity intensity,
            double createdTime,
            float duration)
        {
            if (noiseId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(noiseId));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
            }

            if (pathRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(pathRadius));
            }

            if (duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            NoiseId = noiseId;
            SourceType = sourceType;
            WorldPosition = worldPosition;
            RoomId = roomId;
            PathRadius = pathRadius;
            Intensity = intensity;
            CreatedTime = createdTime;
            Duration = duration;
        }

        public long NoiseId { get; }
        public NoiseSourceType SourceType { get; }
        public Vector3 WorldPosition { get; }
        public string RoomId { get; }
        public float PathRadius { get; }
        public NoiseIntensity Intensity { get; }
        public double CreatedTime { get; }
        public float Duration { get; }
    }
}
