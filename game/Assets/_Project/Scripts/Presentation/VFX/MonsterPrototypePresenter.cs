using MonkeyLab.Gameplay.Monsters;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class MonsterPrototypePresenter : MonoBehaviour
    {
        private static readonly Color PatrolColor = new(0.68f, 0.10f, 0.12f);
        private static readonly Color IdleColor = new(0.92f, 0.40f, 0.08f);
        private static readonly Color InvestigateColor = new(1f, 0.82f, 0.08f);
        private static readonly Color SearchColor = new(0.56f, 0.18f, 0.76f);

        [SerializeField] private MonsterBrain _brain;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Light _indicatorLight;

        private MaterialPropertyBlock _propertyBlock;
        private bool _isSubscribed;

        public void Configure(MonsterBrain brain, Renderer targetRenderer, Light indicatorLight)
        {
            Unsubscribe();
            _brain = brain;
            _renderer = targetRenderer;
            _indicatorLight = indicatorLight;
            Subscribe();
            ApplyState(_brain.State);
        }

        private void OnEnable()
        {
            Subscribe();
            if (_brain != null)
            {
                ApplyState(_brain.State);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_isSubscribed || _brain == null)
            {
                return;
            }

            _brain.StateChanged += HandleStateChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _brain == null)
            {
                return;
            }

            _brain.StateChanged -= HandleStateChanged;
            _isSubscribed = false;
        }

        private void HandleStateChanged(MonsterBrain brain, MonsterState state)
        {
            ApplyState(state);
        }

        private void ApplyState(MonsterState state)
        {
            var color = state switch
            {
                MonsterState.InvestigateNoise => InvestigateColor,
                MonsterState.Search => SearchColor,
                MonsterState.RoomIdle => IdleColor,
                _ => PatrolColor
            };

            if (_renderer != null)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _renderer.SetPropertyBlock(_propertyBlock);
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = color;
                _indicatorLight.intensity = state == MonsterState.InvestigateNoise ? 5f : 2f;
            }
        }
    }
}
