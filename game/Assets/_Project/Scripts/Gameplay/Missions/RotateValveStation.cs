using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 액체 보관실의 밸브다. 생존자의 밸브 잠그기(GDD §10.2, 시계 방향 3바퀴)와
    /// 빌런의 밸브 압력 풀기(§13.2, 반시계 방향 3바퀴)가 같은 오브젝트를
    /// 반대 방향으로 조작한다. 두 미션은 독립적으로 진행되며 서버가 요청자의
    /// 역할에 따라 어느 쪽 누적치에 더할지 결정한다. 실제 상태 전이는 서버가
    /// 판정하고 이 컴포넌트는 표시와 요청만 담당한다.
    /// </summary>
    public sealed class RotateValveStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _stationRenderer;
        [SerializeField] private SurvivorMissionBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.35f, 0.45f, 0.5f, 1f);
        [SerializeField]
        private Color _lockedColor = new(0.3f, 0.9f, 0.45f, 1f);
        [SerializeField]
        private Color _loosenedColor = new(0.65f, 0.2f, 0.85f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject, float> _externalRotateRequest;
        private object _authorityOwner;
        private string _interactionFeedback;
        private RotateValveMissionRules _lockRules;
        private RotateValveMissionRules _loosenRules;

        public event Action<RotateValveStation> StateChanged;
        public event Action<RotateValveStation, GameObject> MissionOpened;

        /// <summary>생존자의 잠그기 진행이다(시계 방향).</summary>
        public RotateValveMissionRules LockRules => _lockRules ??=
            new RotateValveMissionRules(
                _config != null ? _config.ValveLockTurns : 3f,
                isClockwise: true);

        /// <summary>빌런의 풀기 진행이다(반시계 방향).</summary>
        public RotateValveMissionRules LoosenRules => _loosenRules ??=
            new RotateValveMissionRules(
                _config != null ? _config.ValveLockTurns : 3f,
                isClockwise: false);

        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public object InteractionAuthorityOwner => _authorityOwner;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : "밸브 조작";

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
            var canInteractLocally = _config != null && isActiveAndEnabled;
            return canInteractLocally &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            MissionOpened?.Invoke(this, interactor);
        }

        /// <summary>
        /// 밸브를 드래그해 돌릴 때마다 델타 각도(부호 있음)를 보낸다. 서버가
        /// 요청자의 역할에 따라 잠그기 또는 풀기 진행에 반영한다.
        /// </summary>
        public void RotateValve(GameObject interactor, float deltaDegrees)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _externalRotateRequest?.Invoke(interactor, deltaDegrees);
        }

        /// <summary>서버가 확정한 두 진행 상태를 반영한다.</summary>
        public void ApplyAuthoritativeState(
            float lockedDegrees,
            bool isLocked,
            float loosenedDegrees,
            bool isLoosened)
        {
            LockRules.ApplyAuthoritativeSnapshot(lockedDegrees, isLocked);
            LoosenRules.ApplyAuthoritativeSnapshot(loosenedDegrees, isLoosened);
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

            _stationRenderer.color = LoosenRules.IsCompleted
                ? _loosenedColor
                : LockRules.IsCompleted
                    ? _lockedColor
                    : _idleColor;
        }
    }
}
