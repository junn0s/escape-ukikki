using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 백신실 B의 백신 샘플 스캔 미션이다(GDD §10.2). 샘플 3개를 순서대로
    /// 바코드 리더기로 드래그해 스캔한다. 실제 상태 전이는 서버가 판정하고
    /// 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class VaccineSampleScanStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.35f, 0.5f, 0.4f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalScanRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private VaccineSampleScanMissionRules _rules;

        public event Action<VaccineSampleScanStation> StateChanged;
        public event Action<VaccineSampleScanStation, GameObject> MissionOpened;

        public VaccineSampleScanMissionRules Rules => _rules ??=
            new VaccineSampleScanMissionRules(
                _config != null ? _config.VaccineSampleCount : 3);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "샘플 스캔 완료"
                : $"백신 샘플 스캔 ({Rules.ScannedCount}/{Rules.SampleCount})";

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
            Action<GameObject, int> scanRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalScanRequest = scanRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalScanRequest = null;
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

        /// <summary>샘플 하나를 리더기로 드래그했을 때 호출한다.</summary>
        public void ScanSample(GameObject interactor, int sampleIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalScanRequest?.Invoke(interactor, sampleIndex);
        }

        /// <summary>서버가 확정한 스캔 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(int scannedCount)
        {
            Rules.ApplyAuthoritativeSnapshot(scannedCount);
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
