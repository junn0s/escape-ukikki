using System;
using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    /// <summary>
    /// 보안실에 배치되는 CCTV·로그 단말기다. 월드 E 상호작은
    /// 이 객체가 받고, 표시 UI와 네트워크 점유 요청은 Presentation이 처리한다.
    /// </summary>
    public sealed class SecurityTerminalPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _lockedColor =
            new(0.22f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color _availableColor =
            new(0.2f, 0.75f, 1f, 1f);
        [SerializeField] private Color _occupiedColor =
            new(1f, 0.7f, 0.18f, 1f);

        private Func<GameObject, bool> _interactionFilter;
        private bool _isUnlocked;
        private bool _isOccupied;

        public event Action<GameObject> InteractionRequested;

        public string Prompt => !_isUnlocked
            ? "CCTV 단말기 · 프로젝트 50%에서 활성화"
            : _isOccupied
                ? "CCTV 단말기 사용 중"
                : "CCTV와 보안 로그 확인";
        public Transform InteractionTransform => transform;

        public void Configure(SpriteRenderer targetRenderer)
        {
            _renderer = targetRenderer;
            ApplyVisual();
        }

        public void SetInteractionFilter(Func<GameObject, bool> filter)
        {
            _interactionFilter = filter;
        }

        public void ApplyNetworkState(bool isUnlocked, bool isOccupied)
        {
            _isUnlocked = isUnlocked;
            _isOccupied = isOccupied;
            ApplyVisual();
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled &&
                   _isUnlocked &&
                   !_isOccupied &&
                   (_interactionFilter?.Invoke(interactor) ?? true);
        }

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor))
            {
                InteractionRequested?.Invoke(interactor);
            }
        }

        private void Awake()
        {
            _renderer ??= GetComponent<SpriteRenderer>();
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.color = !_isUnlocked
                ? _lockedColor
                : _isOccupied
                    ? _occupiedColor
                    : _availableColor;
        }
    }
}
