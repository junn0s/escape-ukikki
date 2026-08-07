using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 격리실의 배선 복구 미션이다(GDD §10.2). 같은 색 전선 4가닥을 좌우로
    /// 연결한다. 격리실 A와 B가 같은 조작을 공유한다. 실제 상태 전이는 서버가
    /// 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class WireConnectStation : MonoBehaviour, IInteractable
    {
        private static readonly int[] DefaultWireColors = { 0, 1, 2, 3 };

        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.3f, 0.3f, 0.35f, 1f);
        [SerializeField]
        private Color _completedColor = new(0.3f, 0.9f, 0.45f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, int, int> _externalConnectRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private WireConnectMissionRules _rules;

        public event Action<WireConnectStation> StateChanged;
        public event Action<WireConnectStation, GameObject> MissionOpened;

        public WireConnectMissionRules Rules => _rules ??=
            new WireConnectMissionRules(DefaultWireColors);
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : Rules.IsCompleted
                ? "배선 복구 완료"
                : $"배선 복구 ({Rules.ConnectedCount}/{Rules.WireCount})";

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
            Action<GameObject, int, int> connectRequest)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalConnectRequest = connectRequest;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalConnectRequest = null;
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

        /// <summary>왼쪽 전선을 오른쪽 단자로 드래그해 놓았을 때 호출한다.</summary>
        public void ConnectWire(
            GameObject interactor,
            int leftIndex,
            int rightIndex)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalConnectRequest?.Invoke(interactor, leftIndex, rightIndex);
        }

        /// <summary>서버가 확정한 연결 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(bool[] connectedFlags)
        {
            Rules.Reset();
            for (var index = 0; index < connectedFlags.Length; index++)
            {
                if (connectedFlags[index])
                {
                    Rules.TryConnect(index, index);
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
