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
        [SerializeField, Min(0.1f)]
        private float _itemPickupRangeMeters = 1.2f;

        public string Id => _id;
        public float GeneralInteractionRangeMeters =>
            Mathf.Max(0.1f, _generalInteractionRangeMeters);
        public float ExclusiveOccupancyTimeoutSeconds =>
            Mathf.Max(1f, _exclusiveOccupancyTimeoutSeconds);

        /// <summary>
        /// 완성된 해독제와 레시피 쪽지를 집을 수 있는 거리다.
        /// docs/balance-and-telemetry.md §3의 "아이템 획득 거리 1.2m"에 해당한다.
        /// </summary>
        public float ItemPickupRangeMeters =>
            Mathf.Max(0.1f, _itemPickupRangeMeters);
    }
}
