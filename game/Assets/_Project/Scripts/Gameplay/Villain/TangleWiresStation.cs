using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 보안 카메라 선 꼬기 미션이다(GDD §13.2). 전선 4가닥을 색과 무관하게
    /// 모두 '단락' 단자로 꽂는다. CCTV 화면 닦기와 같은 자리의 위장
    /// 오브젝트다. 실제 상태 전이는 서버가 판정하고 이 컴포넌트는 표시와
    /// 요청만 담당한다.
    /// </summary>
    public sealed class TangleWiresStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField, Min(1)] private int _wireCount = 4;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.65f, 0.2f, 0.85f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int> _externalPlugRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private TangleWiresMissionRules _rules;

        public event Action<TangleWiresStation> StateChanged;
        public event Action<TangleWiresStation, GameObject> MissionOpened;

        public TangleWiresMissionRules Rules => _rules ??=
            new TangleWiresMissionRules(_wireCount);
        public VillainMissionKind Kind => VillainMissionKind.SecurityWireTangle;
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        // 생존자에게는 위장 대상과 동일한 문구가 보인다.
        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "화면 세척 완료"
                : "CCTV 화면 닦기";

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
            Action<GameObject, int> plugRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalPlugRequest = plugRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalPlugRequest = null;
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

        /// <summary>전선 하나를 단락 단자로 드래그해 놓았을 때 호출한다.</summary>
        public void PlugWire(GameObject interactor, int wireIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalPlugRequest?.Invoke(interactor, wireIndex);
        }

        /// <summary>서버가 확정한 배치 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] pluggedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < pluggedFlags.Length; index++)
            {
                if (pluggedFlags[index])
                {
                    Rules.TryPlug(index);
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
