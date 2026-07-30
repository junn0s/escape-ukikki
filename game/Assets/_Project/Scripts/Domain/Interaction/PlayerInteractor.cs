using MonkeyLab.Core;
using MonkeyLab.Gameplay.Players;
using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    /// <summary>
    /// 주변에서 상호작용 대상을 찾아 프롬프트를 노출하고, E 입력을 대상에 전달한다.
    ///
    /// 검색은 매 프레임 Find 계열 API를 쓰지 않고 Physics2D 오버랩으로 처리한다
    /// (project-structure.md §7). 결과 배열은 재사용해 GC 할당을 피한다.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaxOverlapResults = 8;

        [SerializeField] private SO_GameBalance _balance;
        [SerializeField] private PlayerInputReader _input;

        [Tooltip("상호작용 대상이 속한 레이어")]
        [SerializeField] private LayerMask _interactableMask = ~0;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[MaxOverlapResults];
        private IInteractable _current;

        /// <summary>현재 조준 중인 대상. 없으면 null.</summary>
        public IInteractable CurrentTarget => _current;

        /// <summary>HUD가 읽을 프롬프트. 대상이 없으면 빈 문자열.</summary>
        public string CurrentPrompt => _current?.InteractionPrompt ?? string.Empty;

        /// <summary>대상이 바뀔 때 발생. UI는 이 이벤트만 구독한다.</summary>
        public event System.Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (_balance == null)
            {
                Debug.LogError($"[{nameof(PlayerInteractor)}] {nameof(_balance)} 미할당", this);
                enabled = false;
                return;
            }

            if (_input == null)
            {
                Debug.LogError($"[{nameof(PlayerInteractor)}] {nameof(_input)} 미할당", this);
                enabled = false;
            }
        }

        private void Update()
        {
            IInteractable found = FindNearest();

            if (!ReferenceEquals(found, _current))
            {
                _current = found;
                TargetChanged?.Invoke(_current);
            }

            if (_current != null && _input.InteractPressedThisFrame)
            {
                _current.TryBeginInteract(gameObject);
            }
        }

        private IInteractable FindNearest()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = _interactableMask
            };

            int count = Physics2D.OverlapCircle(
                transform.position,
                _balance.InteractionRange,
                filter,
                _overlapBuffer);

            IInteractable best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];
                if (hit == null || !hit.TryGetComponent(out IInteractable candidate))
                {
                    continue;
                }

                if (candidate.IsOccupied || !candidate.CanInteract(gameObject))
                {
                    continue;
                }

                Transform point = candidate.InteractionPoint != null
                    ? candidate.InteractionPoint
                    : hit.transform;

                float sqr = (point.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (_balance == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.78f, 0.78f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _balance.InteractionRange);
        }
    }
}
