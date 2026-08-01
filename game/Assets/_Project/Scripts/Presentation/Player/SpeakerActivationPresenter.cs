using System;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using UnityEngine;

namespace MonkeyLab.Presentation.Player
{
    /// <summary>
    /// 스피커가 울린 방의 재생 연출을 처리한다.
    /// 네트워크 콜백에서 UI를 직접 찾지 않고 이 프레젠터가 중계한다.
    /// </summary>
    public sealed class SpeakerActivationPresenter : MonoBehaviour
    {
        [SerializeField] private SpeakerPlacement[] _speakers =
            Array.Empty<SpeakerPlacement>();

        private NetworkSpeakerAuthority _speakerAuthority;

        public void Configure(SpeakerPlacement[] speakers)
        {
            _speakers = speakers ?? Array.Empty<SpeakerPlacement>();
        }

        private void OnEnable()
        {
            NetworkSpeakerAuthority.CurrentChanged += BindAuthority;
            BindAuthority();
        }

        private void OnDisable()
        {
            NetworkSpeakerAuthority.CurrentChanged -= BindAuthority;
            UnbindAuthority();
        }

        private void BindAuthority()
        {
            UnbindAuthority();
            _speakerAuthority = NetworkSpeakerAuthority.Current;
            if (_speakerAuthority != null)
            {
                _speakerAuthority.SpeakerActivated += HandleSpeakerActivated;
            }
        }

        private void UnbindAuthority()
        {
            if (_speakerAuthority != null)
            {
                _speakerAuthority.SpeakerActivated -= HandleSpeakerActivated;
            }

            _speakerAuthority = null;
        }

        private void HandleSpeakerActivated(
            string roomId,
            float playbackSeconds)
        {
            if (_speakers == null)
            {
                return;
            }

            for (var index = 0; index < _speakers.Length; index++)
            {
                var speaker = _speakers[index];
                if (speaker != null && speaker.RoomId == roomId)
                {
                    speaker.PlayActivationFeedback(playbackSeconds);
                    return;
                }
            }
        }
    }
}
