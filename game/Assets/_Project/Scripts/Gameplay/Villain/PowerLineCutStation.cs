using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 메인 전력선 절단 미션이다(GDD §13.2). 가위로 전선 3가닥을 클릭해
    /// 자른다. 퓨즈 교체와 같은 자리의 위장 오브젝트다. 오염된 주사기
    /// 폐기와 같은 N개 클릭 판정을 공유한다. 실제 상태 전이는 서버가
    /// 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class PowerLineCutStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField, Min(1)] private int _wireCount = 3;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.35f, 0.3f, 0.2f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.65f, 0.2f, 0.85f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalCutRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private DragItemsMissionRules _rules;

        public event Action<PowerLineCutStation> StateChanged;
        public event Action<PowerLineCutStation, GameObject> MissionOpened;

        public DragItemsMissionRules Rules => _rules ??=
            new DragItemsMissionRules(_wireCount);
        public VillainMissionKind Kind => VillainMissionKind.MainPowerLineCut;
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        // 생존자에게는 위장 대상과 동일한 문구가 보인다.
        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "퓨즈 교체 완료"
                : "탄 퓨즈 뽑기";

        public void Configure(
            SpriteRenderer stationRenderer,
            int wireCount,
            string roomId)
        {
            _stationRenderer = stationRenderer;
            _wireCount = wireCount;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject, int> cutRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalCutRequest = cutRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalCutRequest = null;
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

        /// <summary>전선 하나를 가위로 클릭했을 때 호출한다.</summary>
        public void CutWire(GameObject interactor, int wireIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalCutRequest?.Invoke(interactor, wireIndex);
        }

        /// <summary>서버가 확정한 절단 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] cutFlags)
        {
            Rules.Reset();
            for (var index = 0; index < cutFlags.Length; index++)
            {
                if (cutFlags[index])
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
