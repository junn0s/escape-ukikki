using System;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Network;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 프로젝트 단계를 실제 월드 조명과 탐색 표시로 반영한다.
    /// 25%는 유도등·방 표지판, 50%는 보안실 조명, 75%는 탈출 경로,
    /// 100%는 옥상·비상문 최종 조명을 점등한다.
    /// </summary>
    public sealed class ProjectMilestoneWorldPresenter : MonoBehaviour
    {
        private const float BaselineGlobalIntensity = 0f;
        private const float MaximumGuideIntensity = 0.16f;
        private const float MaximumSecurityIntensity = 0.24f;
        private const float MaximumExitIntensity = 0.36f;
        private const float MaximumFlickerAmount = 0.025f;
        private const float GuideOuterRadius = 1.35f;
        private const float SecurityOuterRadius = 3f;
        private const float ExitOuterRadius = 2.5f;

        [SerializeField] private Light2D _globalEmergencyLight;
        [SerializeField] private Light2D[] _guideLights = Array.Empty<Light2D>();
        [SerializeField] private Light2D[] _securityLights = Array.Empty<Light2D>();
        [SerializeField] private Light2D[] _exitLights = Array.Empty<Light2D>();
        [SerializeField] private SpriteRenderer[] _guideIndicators =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private SpriteRenderer[] _exitMarkers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private TextMesh[] _roomLabels = Array.Empty<TextMesh>();
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private PresentationAssetCatalog _assetCatalog;
        [SerializeField, Min(0f)] private float _darkIntensity = 0f;
        [SerializeField, Min(0f)] private float _guideIntensity =
            MaximumGuideIntensity;
        [SerializeField, Min(0f)] private float _securityIntensity =
            MaximumSecurityIntensity;
        [SerializeField, Min(0f)] private float _exitIntensity =
            MaximumExitIntensity;
        [SerializeField, Min(0.01f)] private float _transitionSpeed = 1.6f;
        [SerializeField, Min(0f)] private float _flickerAmount =
            MaximumFlickerAmount;

        private readonly Color _darkLabelColor =
            new(0.12f, 0.20f, 0.23f, 0.10f);
        private readonly Color _restoredLabelColor =
            new(0.55f, 0.9f, 1f, 0.95f);

        private NetworkRoundState _roundState;
        private ProjectMilestone _appliedMilestone = (ProjectMilestone)byte.MaxValue;

        public ProjectMilestone AppliedMilestone => _appliedMilestone;

        public void Configure(
            Light2D[] guideLights,
            Light2D[] securityLights,
            Light2D[] exitLights,
            SpriteRenderer[] guideIndicators,
            SpriteRenderer[] exitMarkers,
            TextMesh[] roomLabels,
            AudioSource audioSource,
            PresentationAssetCatalog assetCatalog = null,
            Light2D globalEmergencyLight = null)
        {
            _globalEmergencyLight = globalEmergencyLight;
            _guideLights = guideLights ?? Array.Empty<Light2D>();
            _securityLights = securityLights ?? Array.Empty<Light2D>();
            _exitLights = exitLights ?? Array.Empty<Light2D>();
            _guideIndicators = guideIndicators ?? Array.Empty<SpriteRenderer>();
            _exitMarkers = exitMarkers ?? Array.Empty<SpriteRenderer>();
            _roomLabels = roomLabels ?? Array.Empty<TextMesh>();
            _audioSource = audioSource;
            _assetCatalog = assetCatalog;
            ApplyDarknessProfile();
            ApplyAssetOverrides();
            ApplyImmediate(ProjectMilestone.None);
        }

        private void OnEnable()
        {
            ApplyDarknessProfile();
            ApplyAssetOverrides();
            NetworkRoundState.CurrentChanged += BindRound;
            BindRound();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            UnbindRound();
        }

        private void Update()
        {
            var milestone = _roundState != null
                ? _roundState.ProjectMilestone
                : ProjectMilestone.None;
            if (milestone != _appliedMilestone)
            {
                ApplyMilestone(milestone);
            }

            AnimateLights(milestone);
            AnimateExitMarkers(milestone);
        }

        private void BindRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += HandleRoundStateChanged;
            }

            HandleRoundStateChanged();
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= HandleRoundStateChanged;
            }

            _roundState = null;
        }

        private void HandleRoundStateChanged()
        {
            ApplyMilestone(
                _roundState != null
                    ? _roundState.ProjectMilestone
                    : ProjectMilestone.None);
        }

        private void ApplyMilestone(ProjectMilestone milestone)
        {
            var previous = _appliedMilestone;
            _appliedMilestone = milestone;
            var hasFacilityGuidance =
                milestone >= ProjectMilestone.FacilityGuidance;
            var hasExitGuidance =
                milestone >= ProjectMilestone.ExitGuidance;

            for (var index = 0; index < _roomLabels.Length; index++)
            {
                if (_roomLabels[index] != null)
                {
                    _roomLabels[index].color = hasFacilityGuidance
                        ? _restoredLabelColor
                        : _darkLabelColor;
                }
            }

            for (var index = 0; index < _guideIndicators.Length; index++)
            {
                if (_guideIndicators[index] != null)
                {
                    _guideIndicators[index].color = hasFacilityGuidance
                        ? new Color(0.25f, 0.9f, 1f, 0.9f)
                        : new Color(0.04f, 0.12f, 0.14f, 0.18f);
                }
            }

            for (var index = 0; index < _exitMarkers.Length; index++)
            {
                if (_exitMarkers[index] != null)
                {
                    _exitMarkers[index].enabled = hasExitGuidance;
                }
            }

            if (previous < ProjectMilestone.FacilityGuidance &&
                hasFacilityGuidance)
            {
                PlayOneShot(_assetCatalog?.PowerRestoredClip);
            }

            if (previous < ProjectMilestone.ExitGuidance && hasExitGuidance)
            {
                PlayOneShot(_assetCatalog?.ExitRevealedClip);
            }
        }

        private void AnimateLights(ProjectMilestone milestone)
        {
            var flicker = Mathf.Sin(Time.unscaledTime * 11.3f) *
                          _flickerAmount;
            AnimateLightGroup(
                _guideLights,
                milestone >= ProjectMilestone.FacilityGuidance
                    ? _guideIntensity + flicker
                    : _darkIntensity);
            AnimateLightGroup(
                _securityLights,
                milestone >= ProjectMilestone.SecurityAccess
                    ? _securityIntensity
                    : _darkIntensity);
            AnimateLightGroup(
                _exitLights,
                milestone >= ProjectMilestone.Completed
                    ? _exitIntensity
                    : milestone >= ProjectMilestone.ExitGuidance
                        ? _guideIntensity
                        : 0f);
        }

        private void AnimateLightGroup(Light2D[] lights, float targetIntensity)
        {
            for (var index = 0; index < lights.Length; index++)
            {
                var light = lights[index];
                if (light == null)
                {
                    continue;
                }

                light.intensity = Mathf.MoveTowards(
                    light.intensity,
                    Mathf.Max(0f, targetIntensity),
                    _transitionSpeed * Time.unscaledDeltaTime);
            }
        }

        private void AnimateExitMarkers(ProjectMilestone milestone)
        {
            if (milestone < ProjectMilestone.ExitGuidance)
            {
                return;
            }

            var alpha = 0.72f +
                        (Mathf.Sin(Time.unscaledTime * 4.2f) * 0.5f + 0.5f) *
                        0.28f;
            for (var index = 0; index < _exitMarkers.Length; index++)
            {
                var marker = _exitMarkers[index];
                if (marker == null)
                {
                    continue;
                }

                var color = marker.color;
                color.a = alpha;
                marker.color = color;
            }
        }

        private void ApplyImmediate(ProjectMilestone milestone)
        {
            ApplyDarknessProfile();
            _appliedMilestone = (ProjectMilestone)byte.MaxValue;
            ApplyMilestone(milestone);
            SetLightIntensity(_guideLights, _darkIntensity);
            SetLightIntensity(_securityLights, _darkIntensity);
            SetLightIntensity(_exitLights, 0f);
        }

        private void ApplyDarknessProfile()
        {
            _guideIntensity = Mathf.Min(
                _guideIntensity,
                MaximumGuideIntensity);
            _securityIntensity = Mathf.Min(
                _securityIntensity,
                MaximumSecurityIntensity);
            _exitIntensity = Mathf.Min(
                _exitIntensity,
                MaximumExitIntensity);
            _flickerAmount = Mathf.Min(
                _flickerAmount,
                MaximumFlickerAmount);

            ResolveGlobalEmergencyLight();
            if (_globalEmergencyLight != null)
            {
                _globalEmergencyLight.intensity = BaselineGlobalIntensity;
                _globalEmergencyLight.color =
                    new Color(0.10f, 0.16f, 0.22f);
            }

            ConfigurePointLightGroup(
                _guideLights,
                innerRadius: 0.18f,
                outerRadius: GuideOuterRadius);
            ConfigurePointLightGroup(
                _securityLights,
                innerRadius: 0.8f,
                outerRadius: SecurityOuterRadius);
            ConfigurePointLightGroup(
                _exitLights,
                innerRadius: 0.65f,
                outerRadius: ExitOuterRadius);
        }

        private void ResolveGlobalEmergencyLight()
        {
            if (_globalEmergencyLight != null)
            {
                return;
            }

            foreach (var light in GetComponentsInChildren<Light2D>(true))
            {
                if (light != null &&
                    light.lightType == Light2D.LightType.Global)
                {
                    _globalEmergencyLight = light;
                    return;
                }
            }
        }

        private static void ConfigurePointLightGroup(
            Light2D[] lights,
            float innerRadius,
            float outerRadius)
        {
            foreach (var light in lights)
            {
                if (light == null ||
                    light.lightType != Light2D.LightType.Point)
                {
                    continue;
                }

                light.pointLightInnerRadius = innerRadius;
                light.pointLightOuterRadius = outerRadius;
            }
        }

        private void ApplyAssetOverrides()
        {
            if (_assetCatalog == null)
            {
                return;
            }

            if (_assetCatalog.GuideLightSprite != null)
            {
                SetSprite(_guideIndicators, _assetCatalog.GuideLightSprite);
            }

            if (_assetCatalog.ExitMarkerSprite != null)
            {
                SetSprite(_exitMarkers, _assetCatalog.ExitMarkerSprite);
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private static void SetLightIntensity(
            Light2D[] lights,
            float intensity)
        {
            for (var index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null)
                {
                    lights[index].intensity = intensity;
                }
            }
        }

        private static void SetSprite(
            SpriteRenderer[] renderers,
            Sprite sprite)
        {
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].sprite = sprite;
                }
            }
        }
    }
}
