using MonkeyLab.Gameplay.Domain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    public sealed class FuseStationPrototype : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer _stationRenderer;
        [SerializeField] private Light _indicatorLight;
        [SerializeField] private Color _restoredColor = new(0.15f, 1f, 0.35f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private bool _isRestored;

        public string Prompt => "퓨즈 복구";
        public Transform InteractionTransform => transform;

        public void Configure(Renderer stationRenderer, Light indicatorLight)
        {
            _stationRenderer = stationRenderer;
            _indicatorLight = indicatorLight;
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_isRestored && isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _isRestored = true;
            if (_stationRenderer != null)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                _stationRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", _restoredColor);
                _stationRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = _restoredColor;
                _indicatorLight.intensity = 4f;
            }

            Debug.Log($"[Mission] Fuse restored by {interactor.name}.", this);
        }
    }
}
