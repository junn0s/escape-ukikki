using MonkeyLab.Presentation.Settings;
using UnityEngine;

namespace MonkeyLab.Presentation.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SettingsAudioSource : MonoBehaviour
    {
        [SerializeField] private AudioCategory _category = AudioCategory.Effects;
        [SerializeField, Range(0f, 1f)] private float _baseVolume = 1f;

        private AudioSource _audioSource;

        public void Configure(AudioCategory category, float baseVolume)
        {
            _category = category;
            _baseVolume = Mathf.Clamp01(baseVolume);
            ApplyVolume();
        }

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_baseVolume <= 0f)
            {
                _baseVolume = _audioSource.volume;
            }

            ApplyVolume();
        }

        private void OnEnable()
        {
            LocalGameSettings.Changed += ApplyVolume;
            ApplyVolume();
        }

        private void OnDisable()
        {
            LocalGameSettings.Changed -= ApplyVolume;
        }

        private void ApplyVolume()
        {
            _audioSource ??= GetComponent<AudioSource>();
            _audioSource.volume = _baseVolume *
                                  LocalGameSettings.GetCategoryVolume(_category);
        }
    }
}
