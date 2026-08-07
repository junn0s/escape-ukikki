using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 실험실의 시약병 분류 미션이다(GDD §10.2). 빨강·파랑·노랑 시약병을 같은 색
    /// 칸으로 드래그한다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class ReagentSortingStation : MonoBehaviour, IInteractable
    {
        private static readonly int[] DefaultTargetBinIndices = { 0, 1, 2 };

        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.5f, 0.4f, 0.6f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int, int> _externalSortRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private SortReagentsMissionRules _rules;

        public event Action<ReagentSortingStation> StateChanged;
        public event Action<ReagentSortingStation, GameObject> MissionOpened;

        public SortReagentsMissionRules Rules => _rules ??=
            new SortReagentsMissionRules(DefaultTargetBinIndices);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "시약병 분류 완료"
                : $"시약병 분류 ({Rules.SortedCount}/{Rules.ReagentCount})";

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
            Action<GameObject, int, int> sortRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalSortRequest = sortRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalSortRequest = null;
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

        /// <summary>시약병 하나를 칸으로 드래그해 놓았을 때 호출한다.</summary>
        public void PlaceReagent(
            GameObject interactor,
            int reagentIndex,
            int binIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalSortRequest?.Invoke(interactor, reagentIndex, binIndex);
        }

        /// <summary>서버가 확정한 분류 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] sortedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < sortedFlags.Length; index++)
            {
                if (sortedFlags[index])
                {
                    Rules.TrySort(index, Rules.GetTargetBinIndex(index));
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
