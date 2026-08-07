using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실의 백신 데이터 다운로드 미션이다(GDD §10.2). 버튼을 누르고 있는 동안만
    /// 진행되고 손을 떼면 0으로 초기화된다. 실제 상태 전이는 서버가 판정하고
    /// 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class VaccineDataDownloadStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField]
        private Color _holdingColor = new(0.3f, 0.75f, 0.9f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, bool> _externalHoldRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<VaccineDataDownloadStation> StateChanged;
        public event Action<VaccineDataDownloadStation, GameObject> MissionOpened;

        public HoldButtonMissionRules Rules { get; } = new();
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "다운로드 완료"
                : "다운로드 (누르고 있기)";

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
            Action<GameObject, bool> holdRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalHoldRequest = holdRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalHoldRequest = null;
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

        /// <summary>월드 오브젝트에 처음 상호작용할 때 호출한다. 화면만 연다.</summary>
        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>화면 안 버튼을 누르기 시작할 때 호출한다.</summary>
        public void BeginHold(GameObject interactor)
        {
            SetHolding(interactor, true);
        }

        /// <summary>화면 안 버튼에서 손을 뗄 때 호출한다.</summary>
        public void EndHold(GameObject interactor)
        {
            SetHolding(interactor, false);
        }

        private void SetHolding(GameObject interactor, bool isHolding)
        {
            if (isHolding && !CanInteract(interactor))
            {
                return;
            }

            _externalHoldRequest?.Invoke(interactor, isHolding);
        }

        /// <summary>서버가 확정한 진행 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            float heldSeconds,
            bool isHolding,
            bool isCompleted)
        {
            Rules.ApplyAuthoritativeSnapshot(heldSeconds, isHolding, isCompleted);
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
                : Rules.IsHolding
                    ? _holdingColor
                    : _idleColor;
        }
    }
}
