using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Presentation.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FuseFailureFeedback : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float ClipDurationSeconds = 0.65f;
        private const float PrimaryFrequency = 96f;
        private const float SecondaryFrequency = 213f;

        [SerializeField] private FuseStationPrototype _station;
        [SerializeField] private AudioSource _audioSource;

        private AudioClip _failureClip;
        private bool _isSubscribed;

        public void Configure(
            FuseStationPrototype station,
            AudioSource audioSource)
        {
            Unsubscribe();
            _station = station;
            _audioSource = audioSource;
            Subscribe();
        }

        private void Awake()
        {
            _audioSource ??= GetComponent<AudioSource>();
            EnsureFailureClip();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_failureClip != null)
            {
                Destroy(_failureClip);
            }
        }

        private void HandleMissionFailed(
            FuseStationPrototype station,
            int submittedFuseId,
            int expectedFuseId)
        {
            if (_audioSource == null)
            {
                return;
            }

            EnsureFailureClip();
            _audioSource.PlayOneShot(_failureClip);
        }

        private void EnsureFailureClip()
        {
            if (_failureClip != null)
            {
                return;
            }

            var sampleCount =
                Mathf.CeilToInt(SampleRate * ClipDurationSeconds);
            var samples = new float[sampleCount];
            var noiseState = 0x6D2B79F5u;
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)SampleRate;
                var normalizedTime = time / ClipDurationSeconds;
                var attack = Mathf.Clamp01(normalizedTime * 28f);
                var decay = Mathf.Pow(1f - normalizedTime, 1.7f);

                noiseState = noiseState * 1664525u + 1013904223u;
                var noise =
                    ((noiseState >> 9) & 0x7FFFFF) / 4194303.5f - 1f;
                var buzz =
                    Mathf.Sin(time * PrimaryFrequency * Mathf.PI * 2f) *
                    0.42f;
                var spark =
                    Mathf.Sin(time * SecondaryFrequency * Mathf.PI * 2f) *
                    0.24f;
                samples[index] =
                    Mathf.Clamp((buzz + spark + noise * 0.34f) *
                                attack * decay, -1f, 1f);
            }

            _failureClip = AudioClip.Create(
                "SFX_FuseFailure_Prototype",
                sampleCount,
                1,
                SampleRate,
                false);
            _failureClip.SetData(samples, 0);
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
    }
}
