using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Gameplay.Noise
{
    public sealed class FuseFailureNoiseEmitter : MonoBehaviour
    {
        [SerializeField] private FuseStationPrototype _station;
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private string _roomId = "power";

        private bool _isSubscribed;

        public NoiseService NoiseService => _noiseService;

        public void Configure(
            FuseStationPrototype station,
            NoiseService noiseService,
            string roomId)
        {
            Unsubscribe();
            _station = station;
            _noiseService = noiseService;
            _roomId = roomId;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_isSubscribed || _station == null)
            {
                return;
            }

            _station.MissionFailed += HandleMissionFailed;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _station == null)
            {
                return;
            }

            _station.MissionFailed -= HandleMissionFailed;
            _isSubscribed = false;
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedFuseId,
            int expectedFuseId)
        {
            EmitFailureNoise();
        }

        public void EmitFailureNoise()
        {
            if (_noiseService == null)
            {
                Debug.LogError("[Noise] Fuse failure cannot emit noise because NoiseService is missing.", this);
                return;
            }

            _noiseService.EmitNoise(
                NoiseSourceType.MissionFailure,
                _station.transform.position,
                _roomId,
                NoiseIntensity.Medium);
        }
    }
}
