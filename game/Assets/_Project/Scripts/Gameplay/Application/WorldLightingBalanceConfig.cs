using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/World Lighting Balance Config",
        fileName = "SO_WorldLightingBalance_Default")]
    public sealed class WorldLightingBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "world_lighting_default";
        [SerializeField, Range(0f, 1f)]
        private float _darkGlobalIntensityRatio = 0f;
        [SerializeField, Range(0f, 1f)]
        private float _restoredLightIntensityRatio = 0.15f;

        public string Id => _id;
        public float DarkGlobalIntensityRatio =>
            Mathf.Clamp01(_darkGlobalIntensityRatio);
        public float RestoredLightIntensityRatio =>
            Mathf.Clamp01(_restoredLightIntensityRatio);
    }
}
