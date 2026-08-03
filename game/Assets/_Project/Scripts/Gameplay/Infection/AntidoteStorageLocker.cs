using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 지정 보관 칸이다. 완성된 해독제를 숨겨 둘 수 있다(GDD §14.3).
    /// 바닥 자유 드롭은 MVP에서 지원하지 않으므로 해독제가 플레이어 밖에 존재하는
    /// 유일한 장소이며, 감염 사망 시 소지품도 이곳으로 옮겨진다(SDD §13.3).
    /// 칸 상태는 접근한 플레이어에게만 공개한다(SDD §12.3).
    /// </summary>
    public sealed class AntidoteStorageLocker : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _lockerRenderer;
        [SerializeField] private AntidoteBalanceConfig _config;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _emptyColor = new(0.4f, 0.4f, 0.48f, 1f);
        [SerializeField]
        private Color _stockedColor = new(0.35f, 0.75f, 0.6f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;
        private object _authorityOwner;
        private string _interactionFeedback;

        public event Action<AntidoteStorageLocker> StoredCountChanged;

        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;
        public int StoredCount { get; private set; }
        public object InteractionAuthorityOwner => _authorityOwner;
        public int SlotCapacity =>
            _config != null ? _config.StorageLockerSlotCount : 0;
        public bool HasFreeSlot => StoredCount < SlotCapacity;

        public string Prompt => !string.IsNullOrEmpty(_interactionFeedback)
            ? _interactionFeedback
            : StoredCount > 0
                ? $"보관함 ({StoredCount}/{SlotCapacity})"
                : "보관함 (비어 있음)";

        public void Configure(
            SpriteRenderer lockerRenderer,
            AntidoteBalanceConfig config,
            string roomId)
        {
            _lockerRenderer = lockerRenderer;
            _config = config;
            _roomId = roomId;
        }

        public void SetInteractionAuthority(
            object authorityOwner,
            Func<GameObject, bool> canInteract,
            Action<GameObject> requestInteraction)
        {
            _authorityOwner = authorityOwner;
            _externalCanInteract = canInteract;
            _externalInteractionRequest = requestInteraction;
        }

        public void ClearInteractionAuthority(object authorityOwner)
        {
            if (_authorityOwner != authorityOwner)
            {
                return;
            }

            _authorityOwner = null;
            _externalCanInteract = null;
            _externalInteractionRequest = null;
        }

        public void ApplyInteractionFeedback(
            AntidoteRejectionReason rejectionReason)
        {
            _interactionFeedback =
                AntidoteInteractionFeedback.ToPrompt(rejectionReason);
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
            if (!CanInteract(interactor))
            {
                return;
            }

            // 보관인지 인출인지는 서버가 요청자의 소지 상태를 보고 결정한다.
            _externalInteractionRequest?.Invoke(interactor);
        }

        /// <summary>서버가 확정한 보관 수량을 반영한다.</summary>
        public void ApplyAuthoritativeStoredCount(int storedCount)
        {
            var clamped = Mathf.Clamp(storedCount, 0, Mathf.Max(0, SlotCapacity));
            if (clamped == StoredCount)
            {
                return;
            }

            StoredCount = clamped;
            ClearInteractionFeedback();
            ApplyVisuals();
            StoredCountChanged?.Invoke(this);
        }

        private void Awake()
        {
            if (_lockerRenderer == null)
            {
                _lockerRenderer = GetComponent<SpriteRenderer>();
            }

            if (_config == null)
            {
                Debug.LogError(
                    "[Antidote] Storage locker balance config is missing.",
                    this);
            }

            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (_lockerRenderer == null)
            {
                return;
            }

            _lockerRenderer.color =
                StoredCount > 0 ? _stockedColor : _emptyColor;
        }
    }
}
