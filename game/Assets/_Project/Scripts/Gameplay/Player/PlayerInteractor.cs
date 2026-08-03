using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Player
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaxOverlapCount = 24;

        [SerializeField] private PlayerInputReader _input;
        [SerializeField, Min(0.1f)] private float _interactionRange = 1.5f;
        [SerializeField, Min(0.02f)] private float _scanIntervalSeconds = 0.1f;

        private readonly Collider2D[] _overlaps = new Collider2D[MaxOverlapCount];
        private ContactFilter2D _contactFilter;
        private IInteractable _currentTarget;
        private float _nextScanTime;

        public string CurrentPrompt => _currentTarget?.Prompt ?? string.Empty;
        public bool HasTarget => _currentTarget != null;
        public Transform CurrentTargetTransform =>
            _currentTarget?.InteractionTransform;

        public void Configure(PlayerInputReader input, float interactionRange)
        {
            _input = input;
            _interactionRange = interactionRange;
        }

        private void Awake()
        {
            _contactFilter = new ContactFilter2D
            {
                useTriggers = true
            };
            _contactFilter.SetLayerMask(Physics2D.AllLayers);
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.InteractPressed += HandleInteract;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.InteractPressed -= HandleInteract;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + _scanIntervalSeconds;
            ScanForTarget();
        }

        private void ScanForTarget()
        {
            _currentTarget = null;
            var bestSqrDistance = float.PositiveInfinity;
            var count = Physics2D.OverlapCircle(
                transform.position,
                _interactionRange,
                _contactFilter,
                _overlaps);

            for (var index = 0; index < count; index++)
            {
                var interactable = FindInteractable(_overlaps[index]);
                if (interactable == null || !interactable.CanInteract(gameObject))
                {
                    continue;
                }

                var delta = interactable.InteractionTransform.position - transform.position;
                var sqrDistance = delta.sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                _currentTarget = interactable;
            }
        }

        private void HandleInteract()
        {
            if (_currentTarget == null || !_currentTarget.CanInteract(gameObject))
            {
                return;
            }

            _currentTarget.Interact(gameObject);
            ScanForTarget();
        }

        private static IInteractable FindInteractable(Collider2D source)
        {
            var behaviours = source.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                {
                    return interactable;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}
