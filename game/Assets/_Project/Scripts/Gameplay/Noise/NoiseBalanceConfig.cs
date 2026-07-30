using UnityEngine;

namespace MonkeyLab.Gameplay.Noise
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Noise", fileName = "SO_NoiseBalance_Default")]
    public sealed class NoiseBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "noise_default";
        [SerializeField, Min(0.1f)] private float _smallPathRadius = 12f;
        [SerializeField, Min(0.1f)] private float _mediumPathRadius = 30f;
        [SerializeField, Min(0.1f)] private float _largePathRadius = 40f;

        public string Id => _id;
        public float SmallPathRadius => _smallPathRadius;
        public float MediumPathRadius => _mediumPathRadius;
        public float LargePathRadius => _largePathRadius;

        public float GetPathRadius(NoiseIntensity intensity)
        {
            return intensity switch
            {
                NoiseIntensity.Small => _smallPathRadius,
                NoiseIntensity.Medium => _mediumPathRadius,
                NoiseIntensity.Large => _largePathRadius,
                _ => _mediumPathRadius
            };
        }
    }
}
