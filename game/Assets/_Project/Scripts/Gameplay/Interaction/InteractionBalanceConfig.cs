using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Interaction Balance Config",
        fileName = "SO_InteractionBalance_Default")]
    public sealed class InteractionBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "interaction_default";
        [SerializeField, Min(0.1f)]
        private float _generalInteractionRangeMeters = 1.5f;
        [SerializeField, Min(1f)]
        private float _exclusiveOccupancyTimeoutSeconds = 10f;

        public string Id => _id;
        public float GeneralInteractionRangeMeters =>
            Mathf.Max(0.1f, _generalInteractionRangeMeters);
        public float ExclusiveOccupancyTimeoutSeconds =>
            Mathf.Max(1f, _exclusiveOccupancyTimeoutSeconds);
    }
}
