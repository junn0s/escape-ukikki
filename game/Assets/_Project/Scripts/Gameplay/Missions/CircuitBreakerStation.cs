using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 전력 복구실의 차단기 올리기 미션이다(GDD §10.2). 내려간 스위치 4개를
    /// 각각 클릭해 올린다. 오염된 주사기 폐기와 같은 N개 클릭 판정을
    /// 공유한다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class CircuitBreakerStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField, Min(1)] private int _switchCount = 4;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.3f, 0.35f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalFlipRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DragItemsMissionRules _rules;

        public event Action<CircuitBreakerStation> StateChanged;
        public event Action<CircuitBreakerStation, GameObject> MissionOpened;

        public DragItemsMissionRules Rules => _rules ??=
            new DragItemsMissionRules(_switchCount);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "차단기 복구 완료"
                : $"차단기 올리기 ({Rules.PlacedCount}/{Rules.ItemCount})";

        public void Configure(
            SpriteRenderer stationRenderer,
            SurvivorMissionBalanceConfig config,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject, int> flipRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalFlipRequest = flipRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalFlipRequest = null;
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
            var canInteractLocally = isActiveAndEnabled && !Rules.IsCompleted;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>스위치 하나를 클릭했을 때 호출한다.</summary>
        public void FlipSwitch(GameObject interactor, int switchIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalFlipRequest?.Invoke(interactor, switchIndex);
        }

        /// <summary>서버가 확정한 스위치 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] flippedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < flippedFlags.Length; index++)
            {
                if (flippedFlags[index])
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
