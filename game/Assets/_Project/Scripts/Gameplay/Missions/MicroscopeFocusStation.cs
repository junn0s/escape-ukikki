using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 실험실 B의 현미경 렌즈 초점 미션이다(GDD §10.2). 슬라이더를 밀어 올려
    /// 초록 안전선 구간에서 확정한다. 실제 상태 전이는 서버가 판정하고
    /// 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class MicroscopeFocusStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.4f, 0.45f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, float> _externalPushRequest;
        private Action<GameObject> _externalConfirmRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private SliderToRangeMissionRules _rules;

        public event Action<MicroscopeFocusStation> StateChanged;
        public event Action<MicroscopeFocusStation, GameObject> MissionOpened;

        public SliderToRangeMissionRules Rules => _rules ??=
            new SliderToRangeMissionRules(
                _config != null
                    ? _config.MicroscopeFocusTargetMinNormalized
                    : 0.55f,
                _config != null
                    ? _config.MicroscopeFocusTargetMaxNormalized
                    : 0.7f);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "초점 조정 완료"
                : "현미경 렌즈 초점";

        public void Configure(
            SpriteRenderer stationRenderer,
            SurvivorMissionBalanceConfig config,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _config = config;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject, float> pushRequest,
            Action<GameObject> confirmRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalPushRequest = pushRequest;
            _externalConfirmRequest = confirmRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalPushRequest = null;
            _externalConfirmRequest = null;
        }

        public void ApplyInteractionFeedback(string feedback)
        {
            _interactionFeedback = feedback;
        }

        public void ClearInteractionFeedback()
        {
            _interactionFeedback = string.Empty;
        }

        public bool CanInteract(GameObject interactor)
        {
            var canInteractLocally = _config != null && isActiveAndEnabled &&
                                      !Rules.IsCompleted;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>슬라이더를 밀어 올릴 때마다 델타(0~1 정규화)를 보낸다.</summary>
        public void PushSlider(GameObject interactor, float deltaNormalized)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalPushRequest?.Invoke(interactor, deltaNormalized);
        }

        /// <summary>현재 위치를 목표 구간으로 확정할 때 호출한다.</summary>
        public void ConfirmFocus(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalConfirmRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 슬라이더 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            float positionNormalized,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(positionNormalized, isCompleted);
            ClearInteractionFeedback();
            ApplyVisuals();
            StateChanged?.Invoke(this);
        }

        private void Awake()
        {
            if (_stationRenderer == null)
            {
                _stationRenderer = GetComponent<SpriteRenderer>();
            }

            if (_config == null)
            {
                Debug.LogError(
                    "[Mission] Survivor mission balance config is missing.",
                    this);
            }
        }

        private void ApplyVisuals()
        {
            if (_stationRenderer == null)
            {
                return;
            }

            _stationRenderer.color = Rules.IsCompleted
                ? _completedColor
                : _idleColor;
        }
    }
}
