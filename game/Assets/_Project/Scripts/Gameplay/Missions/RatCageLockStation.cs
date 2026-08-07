using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 실험실 B의 실험용 쥐 케이지 잠그기 미션이다(GDD §10.2). 열린 자물쇠
    /// 아이콘 4개를 각각 클릭해 잠근다. 차단기 올리기와 같은 N개 클릭
    /// 판정을 공유한다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는
    /// 표시와 요청만 담당한다.
    /// </summary>
    public sealed class RatCageLockStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.4f, 0.35f, 0.3f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalLockRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DragItemsMissionRules _rules;

        public event Action<RatCageLockStation> StateChanged;
        public event Action<RatCageLockStation, GameObject> MissionOpened;

        public DragItemsMissionRules Rules => _rules ??=
            new DragItemsMissionRules(
                _config != null ? _config.RatCageLockCount : 4);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "케이지 잠금 완료"
                : $"케이지 잠그기 ({Rules.PlacedCount}/{Rules.ItemCount})";

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
            Action<GameObject, int> lockRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalLockRequest = lockRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalLockRequest = null;
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

        /// <summary>자물쇠 하나를 클릭했을 때 호출한다.</summary>
        public void LockCage(GameObject interactor, int lockIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalLockRequest?.Invoke(interactor, lockIndex);
        }

        /// <summary>서버가 확정한 잠금 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] lockedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < lockedFlags.Length; index++)
            {
                if (lockedFlags[index])
                {
                    Rules.TryPlaceItem(index);
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
