using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 개인 레시피 후보 지점이다. docs/map-level-design.md §7.2의 후보 목록에 배치한다.
    /// 라운드마다 서버가 생존자별로 서로 다른 후보 하나를 배정하고,
    /// 배정받은 본인만 이 지점에서 레시피를 발견할 수 있다(GDD §14.2).
    /// 레시피는 개인 정보이므로 어떤 후보가 누구 것인지는 복제하지 않는다.
    /// </summary>
    public sealed class RecipeNotePrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _noteRenderer;
        [SerializeField] private int _candidateIndex;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _idleColor = new(0.85f, 0.82f, 0.6f, 1f);
        [SerializeField]
        private Color _discoveredColor = new(0.4f, 0.4f, 0.4f, 1f);

        private Func<GameObject, bool> _externalCanInteract;
        private Action<GameObject> _externalInteractionRequest;
        private object _authorityOwner;

        public int CandidateIndex => _candidateIndex;
        public string RoomId => _roomId;
        public Transform InteractionTransform => transform;

        /// <summary>로컬 플레이어가 이 후보에서 자기 레시피를 이미 얻었는지다.</summary>
        public bool IsDiscoveredByLocalPlayer { get; private set; }

        public string Prompt =>
            IsDiscoveredByLocalPlayer ? "확인한 기록" : "기록 살펴보기";

        public void Configure(
            SpriteRenderer noteRenderer,
            int candidateIndex,
            string roomId)
        {
            _noteRenderer = noteRenderer;
            _candidateIndex = candidateIndex;
            _roomId = roomId;
        }

        /// <summary>
        /// 권위 주체를 명시적으로 받는다. 후보마다 다른 대리자를 받기 때문에
        /// 대리자의 <c>Target</c>으로는 소유자를 판별할 수 없다.
        /// </summary>
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

        public bool CanInteract(GameObject interactor)
        {
            // 남의 후보인지 여부를 상호작용 가능 여부로 노출하면 레시피 위치가 새어 나간다.
            // 누구나 살펴볼 수 있게 두고 실제 배정 여부는 서버가 본인에게만 알린다.
            return isActiveAndEnabled &&
                   (_externalCanInteract?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor) || IsDiscoveredByLocalPlayer)
            {
                return;
            }

            _externalInteractionRequest?.Invoke(interactor);
        }

        /// <summary>서버가 이 후보를 로컬 플레이어의 레시피로 확정했을 때 호출한다.</summary>
        public void ApplyLocalDiscovery()
        {
            IsDiscoveredByLocalPlayer = true;
            ApplyVisuals();
        }

        private void Awake()
        {
            if (_noteRenderer == null)
            {
                _noteRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (_noteRenderer == null)
            {
                return;
            }

            _noteRenderer.color = IsDiscoveredByLocalPlayer
                ? _discoveredColor
                : _idleColor;
        }
    }
}
