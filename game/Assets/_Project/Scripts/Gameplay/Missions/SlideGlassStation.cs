using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 실험실의 슬라이드 글라스 닦기 미션이다(GDD §10.2). 얼룩 3개를 각각 여러 번
    /// 문질러 지운다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와 요청만
    /// 담당한다.
    /// </summary>
    public sealed class SlideGlassStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalScrubRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private ScrubStainsMissionRules _rules;

        public event Action<SlideGlassStation> StateChanged;
        public event Action<SlideGlassStation, GameObject> MissionOpened;

        public ScrubStainsMissionRules Rules => _rules ??=
            new ScrubStainsMissionRules(
                _config != null ? _config.SlideGlassStainCount : 3,
                _config != null ? _config.SlideGlassScrubsPerStain : 5);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "유리 세척 완료"
                : $"유리 세척 ({Rules.CleanedCount}/{Rules.StainCount})";

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
            Action<GameObject, int> scrubRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalScrubRequest = scrubRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalScrubRequest = null;
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

        /// <summary>얼룩 하나를 문지를 때마다 호출한다.</summary>
        public void ScrubStain(GameObject interactor, int stainIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalScrubRequest?.Invoke(interactor, stainIndex);
        }

        /// <summary>서버가 확정한 문지름 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(int[] scrubCounts)
        {
            Rules.Reset();
            for (var index = 0; index < scrubCounts.Length; index++)
            {
                for (var scrub = 0; scrub < scrubCounts[index]; scrub++)
                {
                    Rules.TryScrub(index);
                }
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
