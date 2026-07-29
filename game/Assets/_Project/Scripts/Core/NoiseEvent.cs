using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 발생한 소음 하나. 서버가 만들고 괴물이 후보로 평가한다.
    /// docs/system-design-document.md §9.1
    /// </summary>
    public readonly struct NoiseEvent
    {
        public int NoiseId { get; }
        public NoiseSourceType SourceType { get; }
        public Vector3 WorldPosition { get; }
        public float PathRadius { get; }
        public NoiseIntensity Intensity { get; }
        public float CreatedTime { get; }

        public NoiseEvent(
            int noiseId,
            NoiseSourceType sourceType,
            Vector3 worldPosition,
            float pathRadius,
            NoiseIntensity intensity,
            float createdTime)
        {
            NoiseId = noiseId;
            SourceType = sourceType;
            WorldPosition = worldPosition;
            PathRadius = pathRadius;
            Intensity = intensity;
            CreatedTime = createdTime;
        }

        public bool IsValid => PathRadius > 0f;
    }
}
