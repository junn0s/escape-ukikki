using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 월드에 놓인 현장 단서 한 개다.
    /// 생성 전에는 숨어 있고, 활성화되면 라운드가 끝날 때까지 그대로 남는다.
    /// 자동 소멸 타이머를 두지 않는다(GDD §15.1).
    /// </summary>
    public sealed class ClueMarker : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private ClueKind _kind;
        [SerializeField] private int _clueId;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _activeColor = new(0.95f, 0.15f, 0.15f, 0.85f);

        private ClueState _state = ClueState.Inactive;

        public event Action<ClueMarker> StateChanged;

        public ClueKind Kind => _kind;
        public int ClueId => _clueId;
        public string RoomId => _roomId;
        public ClueState State => _state;
        public bool IsActive => _state != ClueState.Inactive;

        public string DisplayName => _kind switch
        {
            ClueKind.VentRedSmoke => "환풍구의 붉은 연기",
            ClueKind.BrokenQuarantineLock => "파손된 격리실 잠금장치",
            ClueKind.EmptySyringe => "바닥의 빈 주사기",
            ClueKind.SpeakerRedLed => "스피커의 붉은 LED",
            _ => "현장 단서"
        };

        public void Configure(
            SpriteRenderer markerRenderer,
            ClueKind kind,
            int clueId,
            string roomId)
        {
            _renderer = markerRenderer;
            _kind = kind;
            _clueId = clueId;
            _roomId = roomId;
        }

        private void Awake()
        {
            if (_renderer == null)
            {
                Debug.LogError("[Clue] Marker renderer is missing.", this);
                return;
            }

            ApplyState();
        }

        /// <summary>
        /// 서버가 통보한 상태를 반영한다. 한 번 활성화되면 Inactive로 되돌리지 않는다.
        /// </summary>
        public void ApplyState(ClueState state)
        {
            if (_state == state ||
                (IsActive && state == ClueState.Inactive))
            {
                return;
            }

            _state = state;
            ApplyState();
            StateChanged?.Invoke(this);
        }

        private void ApplyState()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.enabled = IsActive;
            if (!IsActive)
            {
                return;
            }

            // 조사된 단서는 약간 어둡게 표시해 이미 확인했음을 알린다.
            var color = _activeColor;
            if (_state == ClueState.ActiveInspected)
            {
                color.a *= 0.6f;
            }

            _renderer.color = color;
        }
    }
}
