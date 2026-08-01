using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 스피커 리모컨 수치다.
    /// 필드 이름은 docs/balance-and-telemetry.md §6 표의 키와 맞춘다.
    /// 소음 반경은 NoiseBalanceConfig의 Large(40m)를 그대로 쓰므로 여기서 중복 정의하지 않는다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/Speaker Balance Config",
        fileName = "SO_SpeakerBalance_Default")]
    public sealed class SpeakerBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "speaker_default";
        [SerializeField, Min(0f)]
        private float _speakerCooldownSeconds = 45f;
        [SerializeField, Min(0.1f)]
        private float _speakerPlaybackSeconds = 3f;

        public string Id => _id;

        public float SpeakerCooldownSeconds =>
            Mathf.Max(0f, _speakerCooldownSeconds);

        public float SpeakerPlaybackSeconds =>
            Mathf.Max(0.1f, _speakerPlaybackSeconds);
    }
}
