using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Noise
{
    public sealed class NoiseService : MonoBehaviour
    {
        private const float InstantaneousDurationSeconds = 0f;

        [SerializeField] private NoiseBalanceConfig _config;

        private long _nextNoiseId = 1;

        public event Action<NoiseEventData> NoiseEmitted;

        public NoiseBalanceConfig Config => _config;

        public void Configure(NoiseBalanceConfig config)
        {
            _config = config;
        }

        public NoiseEventData EmitNoise(
            NoiseSourceType sourceType,
            Vector3 worldPosition,
            string roomId,
            NoiseIntensity intensity)
        {
            if (_config == null)
            {
                throw new InvalidOperationException("NoiseBalanceConfig is required before emitting noise.");
            }

            var noise = new NoiseEventData(
                _nextNoiseId++,
                sourceType,
                worldPosition,
                roomId,
                _config.GetPathRadius(intensity),
                intensity,
                Time.timeAsDouble,
                InstantaneousDurationSeconds);
            NoiseEmitted?.Invoke(noise);
            Debug.Log(
                $"[Noise] id={noise.NoiseId} source={noise.SourceType} room={noise.RoomId} " +
                $"intensity={noise.Intensity} radius={noise.PathRadius:0.#}m.",
                this);
            return noise;
        }
    }
}
