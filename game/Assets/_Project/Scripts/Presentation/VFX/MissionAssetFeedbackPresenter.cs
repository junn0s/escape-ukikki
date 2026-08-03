using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Network;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 미션 완료·실패 사건을 최종 오디오와 VFX 프리팩 슬롯에 연결한다.
    /// 에셋 목록이 비어 있으면 기존 프로토타입 피드백만 유지한다.
    /// </summary>
    public sealed class MissionAssetFeedbackPresenter : MonoBehaviour
    {
        private const float SpawnedVfxLifetimeSeconds = 6f;

        [SerializeField] private FuseStationPrototype _station;
        [SerializeField] private NetworkFuseStationAuthority _authority;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private PresentationAssetCatalog _assetCatalog;

        private bool _isSubscribed;

        public void Configure(
            FuseStationPrototype station,
            AudioSource audioSource,
            Transform spawnPoint,
            PresentationAssetCatalog assetCatalog = null)
        {
            Unsubscribe();
            _station = station;
            _authority = station != null
                ? station.GetComponent<NetworkFuseStationAuthority>()
                : null;
            _audioSource = audioSource;
            _spawnPoint = spawnPoint;
            _assetCatalog = assetCatalog;
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

            if (_authority != null)
            {
                _authority.PublicMissionCompleted += HandleMissionCompleted;
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

            if (_authority != null)
            {
                _authority.PublicMissionCompleted -= HandleMissionCompleted;
            }
            _station.MissionFailed -= HandleMissionFailed;
            _isSubscribed = false;
        }

        private void HandleMissionCompleted()
        {
            Play(
                _assetCatalog?.MissionSuccessClip,
                _assetCatalog?.MissionSuccessVfxPrefab);
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedValue,
            int expectedValue)
        {
            Play(
                _assetCatalog?.MissionFailureClip,
                _assetCatalog?.MissionFailureVfxPrefab);
        }

        private void Play(AudioClip clip, GameObject vfxPrefab)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }

            if (vfxPrefab == null)
            {
                return;
            }

            var spawnPoint = _spawnPoint != null ? _spawnPoint : transform;
            var instance = Instantiate(
                vfxPrefab,
                spawnPoint.position,
                spawnPoint.rotation);
            Destroy(instance, SpawnedVfxLifetimeSeconds);
        }
    }
}
