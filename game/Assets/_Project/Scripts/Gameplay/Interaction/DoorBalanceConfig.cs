using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Door Balance Config",
        fileName = "SO_DoorBalance_Default")]
    public sealed class DoorBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "door_default";
        [SerializeField, Min(0.1f)]
        private float _openSpeedMetersPerSecond = 8f;
        [SerializeField, Min(0f)]
        private float _closeDelaySeconds = 0.75f;
        [SerializeField, Min(1f)]
        private float _sensorDepthMeters = 4f;
        [SerializeField, Min(0.1f)]
        private float _panelSlideDistanceMeters = 2.15f;

        public string Id => _id;
        public float OpenSpeedMetersPerSecond =>
            Mathf.Max(0.1f, _openSpeedMetersPerSecond);
        public float CloseDelaySeconds =>
            Mathf.Max(0f, _closeDelaySeconds);
        public float SensorDepthMeters =>
            Mathf.Max(1f, _sensorDepthMeters);
        public float PanelSlideDistanceMeters =>
            Mathf.Max(0.1f, _panelSlideDistanceMeters);
    }
}
