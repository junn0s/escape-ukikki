using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 격리실 A의 에어록 압력 조절 미션이다(GDD §10.2). 다이얼을 돌려 눈금을
    /// 0에 맞춘다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만
    /// 담당한다.
    /// </summary>
    public sealed class AirlockDialStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.4f, 0.5f, 0.55f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, float> _externalRotateRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DialToZeroMissionRules _rules;

        public event Action<AirlockDialStation> StateChanged;
        public event Action<AirlockDialStation, GameObject> MissionOpened;

        public DialToZeroMissionRules Rules => _rules ??=
            new DialToZeroMissionRules(
                _config != null ? _config.AirlockDialToleranceDegrees : 8f);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "압력 조절 완료"
                : "에어록 압력 조절";

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
            Action<GameObject, float> rotateRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalRotateRequest = rotateRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalRotateRequest = null;
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

        /// <summary>다이얼을 드래그해 돌릴 때마다 델타 각도를 보낸다.</summary>
        public void RotateDial(GameObject interactor, float deltaDegrees)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalRotateRequest?.Invoke(interactor, deltaDegrees);
        }

        /// <summary>서버가 확정한 다이얼 각도를 반영한다.</summary>
        public void ApplyAuthoritativeState(float angleDegrees)
        {
            Rules.SetAngle(angleDegrees);
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
