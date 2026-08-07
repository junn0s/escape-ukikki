using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실 B의 냉동고 온도 조절 미션이다(GDD §10.2). 위/아래 버튼으로
    /// 목표 온도에 맞추고 일정 시간 유지한다. 실제 상태 전이는 서버가
    /// 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class FreezerTemperatureStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.55f, 0.7f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalAdjustRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private FreezerTemperatureMissionRules _rules;

        public event Action<FreezerTemperatureStation> StateChanged;
        public event Action<FreezerTemperatureStation, GameObject> MissionOpened;

        public FreezerTemperatureMissionRules Rules => _rules ??=
            new FreezerTemperatureMissionRules(
                _config != null ? _config.FreezerTargetTemperature : -20,
                _config != null ? _config.FreezerMinTemperature : -30,
                _config != null ? _config.FreezerMaxTemperature : 10);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "냉동고 온도 조절 완료"
                : "냉동고 온도 조절";

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
            Action<GameObject, int> adjustRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalAdjustRequest = adjustRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalAdjustRequest = null;
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

        /// <summary>위/아래 버튼을 누를 때마다 ±1도 조정 요청을 보낸다.</summary>
        public void AdjustTemperature(GameObject interactor, int deltaDegrees)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalAdjustRequest?.Invoke(interactor, deltaDegrees);
        }

        /// <summary>서버가 확정한 냉동고 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            int currentTemperature,
            float heldSecondsAtTarget,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(
                currentTemperature,
                heldSecondsAtTarget,
                isCompleted);
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
