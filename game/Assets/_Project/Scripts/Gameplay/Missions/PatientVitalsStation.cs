using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 입원실의 환자 바이탈 기록 미션이다(GDD §10.2). 모니터에 표시된 4자리
    /// 숫자를 키패드에 그대로 입력한다. 서버가 라운드마다 코드를 결정적으로
    /// 생성한다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만
    /// 담당한다.
    /// </summary>
    public sealed class PatientVitalsStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.45f, 0.55f, 0.6f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, string> _externalSubmitRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private NumericCodeMissionRules _rules;
        private string _displayedCode = string.Empty;

        public event Action<PatientVitalsStation> StateChanged;
        public event Action<PatientVitalsStation, GameObject> MissionOpened;

        public NumericCodeMissionRules Rules => _rules ??=
            new NumericCodeMissionRules(_displayedCode);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        /// <summary>모니터에 표시할 목표 숫자다. 서버 값이 아직 없으면 비어 있다.</summary>
        public string DisplayedCode => _displayedCode;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "바이탈 기록 완료"
                : "환자 바이탈 기록";

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
            Action<GameObject, string> submitRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalSubmitRequest = submitRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalSubmitRequest = null;
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

        /// <summary>키패드에 입력을 마쳤을 때 서버에 판정을 요청한다.</summary>
        public void SubmitCode(GameObject interactor, string attempt)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalSubmitRequest?.Invoke(interactor, attempt);
        }

        /// <summary>서버가 확정한 표시 코드와 완료 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            string displayedCode,
            bool isCompleted)
        {
            _displayedCode = displayedCode ?? string.Empty;
            _rules = new NumericCodeMissionRules(_displayedCode);
            if (isCompleted)
            {
                _rules.TrySubmit(_displayedCode);
            }

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
