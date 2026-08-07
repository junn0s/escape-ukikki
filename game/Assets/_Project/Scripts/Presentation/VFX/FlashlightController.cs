using System;
using MonkeyLab.Gameplay.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MonkeyLab.Presentation.VFX
{
    public sealed class FlashlightController : MonoBehaviour
    {
        private const float SilhouetteGlowIntensity = 0.006f;
        private const float SilhouetteGlowRadius = 0.5f;
        private const float OccludedConeDistance = 8f;
        private const float OccludedConeHalfAngle = 27f;
        private const float OcclusionRayOriginOffset = 0.08f;
        private const float OcclusionWallClearance = 0.03f;
        private const float OccludedLightFalloffSize = 0.22f;
        private const int OcclusionRayCount = 49;
        private const int OcclusionHitCapacity = 16;
        private static readonly float[] OccludedLightDistances =
        {
            2.6f,
            5f,
            OccludedConeDistance
        };
        private static readonly float[] OccludedLightIntensityWeights =
        {
            0.55f,
            0.30f,
            0.15f
        };
        private static readonly string[] OccludedLightBandNames =
        {
            "Light_Core",
            "Light_Middle",
            "Light_Outer"
        };
        private static readonly Color DefaultFlashlightColor =
            new(0.56f, 0.84f, 0.92f);
        private const float DefaultFlashlightIntensity = 1.1f;

        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerAimController _aim;
        [SerializeField] private Light _flashlight;
        [SerializeField] private Transform _aimPivot;
        [SerializeField] private GameObject _flashlightVisual;
        [SerializeField] private Light2D _personalGlow;
        [SerializeField] private bool _startsEnabled = true;

        private bool _isSubscribed;
        private bool _isInitialized;
        private bool _isFlashlightEnabled;
        private bool _isOwnerVisionVisible = true;
        private readonly RaycastHit2D[] _occlusionHits =
            new RaycastHit2D[OcclusionHitCapacity];
        private ContactFilter2D _occlusionFilter;
        private GameObject _occludedConeObject;
        private Light2D[] _occludedConeLights = Array.Empty<Light2D>();
        private Vector3[][] _occludedConeShapePaths =
            Array.Empty<Vector3[]>();

        public event Action<bool> FlashlightStateChanged;

        public bool IsFlashlightEnabled => _isInitialized
            ? _isFlashlightEnabled
            : _startsEnabled;

        public void Configure(PlayerInputReader input, Light flashlight, bool startsEnabled)
        {
            Unsubscribe();
            _input = input;
            _flashlight = flashlight;
            _startsEnabled = startsEnabled;
            SetFlashlightEnabled(_startsEnabled, notify: false);

            Subscribe();
        }

        public void Configure(
            PlayerInputReader input,
            PlayerAimController aim,
            Transform aimPivot,
            GameObject flashlightVisual,
            bool startsEnabled)
        {
            Unsubscribe();
            _input = input;
            _aim = aim;
            _aimPivot = aimPivot;
            _flashlightVisual = flashlightVisual;
            _startsEnabled = startsEnabled;
            EnsureOccludedCone();
            SetFlashlightEnabled(_startsEnabled, notify: false);

            ApplyAimRotation();
            Subscribe();
        }

        /// <summary>
        /// 소등 시 실루엣용 개인등을 연결한다. GDD 1.6부터 손전등은 감지 조건이
        /// 아니므로 <c>MonsterTarget</c>은 더 이상 필요하지 않다.
        /// </summary>
        public void BindStealthVisibility(Light2D personalGlow)
        {
            _personalGlow = personalGlow;
            SetFlashlightEnabled(IsFlashlightEnabled, notify: false);
        }

        /// <summary>
        /// 네트워크 플레이어의 시야 연출은 소유자에게만 표시한다. 벽 차폐 광원은
        /// 런타임 생성이라 기존 직렬화된 배열에 없으므로 컨트롤러가 직접 끈다.
        /// </summary>
        public void SetOwnerVisionVisible(bool isVisible)
        {
            _isOwnerVisionVisible = isVisible;
            ApplyFlashlightVisualState();
        }

        private void Awake()
        {
            ConfigureOcclusionFilter();
            EnsureInitialized();
            EnsureOccludedCone();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            ApplyAimRotation();
            UpdateOccludedCone();
        }

        private void Toggle()
        {
            SetFlashlightEnabled(!IsFlashlightEnabled, notify: true);
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                SetFlashlightEnabled(_startsEnabled, notify: false);
            }
        }

        private void SetFlashlightEnabled(bool isEnabled, bool notify)
        {
            var changed = !_isInitialized ||
                          _isFlashlightEnabled != isEnabled;
            _isInitialized = true;
            _isFlashlightEnabled = isEnabled;

            if (_flashlight != null)
            {
                _flashlight.enabled = isEnabled;
            }

            EnsureOccludedCone();
            ApplyFlashlightVisualState();

            if (_personalGlow != null)
            {
                _personalGlow.intensity = SilhouetteGlowIntensity;
                _personalGlow.pointLightOuterRadius = SilhouetteGlowRadius;
            }

            if (notify && changed)
            {
                FlashlightStateChanged?.Invoke(isEnabled);
            }
        }

        private void ConfigureOcclusionFilter()
        {
            _occlusionFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            _occlusionFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        }

        private void EnsureOccludedCone()
        {
            // 빌더가 프리팹을 편집하는 동안에는 런타임 광원을 에셋에 직렬화하지 않는다.
            if (!Application.isPlaying)
            {
                return;
            }

            if (_aimPivot == null ||
                _flashlightVisual == null)
            {
                HideLegacyCone();
                return;
            }

            // 정상 생성이 끝난 실제 2D 광원만 그대로 사용한다. 스크립트 리로드 중
            // GameObject만 남고 Light2D가 유실된 경우에는 아래에서 복구한다.
            if (_occludedConeObject != null &&
                HasCompleteOccludedLightSet())
            {
                HideLegacyCone();
                return;
            }

            if (!_occlusionFilter.useLayerMask)
            {
                ConfigureOcclusionFilter();
            }

            var legacyLight =
                _flashlightVisual.GetComponent<Light2D>();
            var existingCone = _aimPivot.Find("OccludedFlashlightCone");
            var coneObject = existingCone != null
                ? existingCone.gameObject
                : new GameObject("OccludedFlashlightCone");
            if (existingCone == null)
            {
                coneObject.transform.SetParent(_aimPivot, false);
            }

            coneObject.transform.localPosition = Vector3.zero;
            coneObject.transform.localRotation = Quaternion.identity;
            coneObject.transform.localScale = Vector3.one;

            // 이전 버전의 Unlit MeshRenderer가 남아 있으면 흰색 막처럼 월드를
            // 덮으므로 반드시 끈다. MeshFilter는 렌더러가 꺼지면 표시되지 않는다.
            if (coneObject.TryGetComponent<MeshRenderer>(
                    out var legacyOcclusionRenderer))
            {
                legacyOcclusionRenderer.enabled = false;
            }

            foreach (var staleLight in
                     coneObject.GetComponentsInChildren<Light2D>(true))
            {
                staleLight.enabled = false;
            }

            _occludedConeLights =
                new Light2D[OccludedLightDistances.Length];
            _occludedConeShapePaths =
                new Vector3[OccludedLightDistances.Length][];
            for (var bandIndex = 0;
                 bandIndex < OccludedLightDistances.Length;
                 bandIndex++)
            {
                var bandTransform = coneObject.transform.Find(
                    OccludedLightBandNames[bandIndex]);
                var bandObject = bandTransform != null
                    ? bandTransform.gameObject
                    : new GameObject(OccludedLightBandNames[bandIndex]);
                if (bandTransform == null)
                {
                    bandObject.transform.SetParent(
                        coneObject.transform,
                        false);
                }

                bandObject.transform.localPosition = Vector3.zero;
                bandObject.transform.localRotation = Quaternion.identity;
                bandObject.transform.localScale = Vector3.one;
                var freeformLight =
                    bandObject.GetComponent<Light2D>() ??
                    bandObject.AddComponent<Light2D>();
                ConfigureOccludedLight(
                    freeformLight,
                    legacyLight,
                    OccludedLightIntensityWeights[bandIndex]);
                _occludedConeLights[bandIndex] = freeformLight;
                _occludedConeShapePaths[bandIndex] =
                    new Vector3[OcclusionRayCount + 1];
                freeformLight.SetShapePath(
                    _occludedConeShapePaths[bandIndex]);
            }

            _occludedConeObject = coneObject;
            HideLegacyCone();
            ApplyFlashlightVisualState();
        }

        private bool HasCompleteOccludedLightSet()
        {
            if (_occludedConeLights.Length !=
                    OccludedLightDistances.Length ||
                _occludedConeShapePaths.Length !=
                    OccludedLightDistances.Length)
            {
                return false;
            }

            for (var index = 0;
                 index < _occludedConeLights.Length;
                 index++)
            {
                if (_occludedConeLights[index] == null ||
                    _occludedConeShapePaths[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConfigureOccludedLight(
            Light2D light,
            Light2D legacyLight,
            float intensityWeight)
        {
            light.lightType = Light2D.LightType.Freeform;
            light.color = legacyLight != null
                ? legacyLight.color
                : DefaultFlashlightColor;
            light.intensity =
                (legacyLight != null
                    ? legacyLight.intensity
                    : DefaultFlashlightIntensity) * intensityWeight;
            light.blendStyleIndex = legacyLight != null
                ? legacyLight.blendStyleIndex
                : 0;
            light.overlapOperation = Light2D.OverlapOperation.Additive;
            light.shapeLightFalloffSize = OccludedLightFalloffSize;
            light.falloffIntensity = 1f;
            light.shadowsEnabled = false;
            light.volumetricEnabled = false;
            light.volumeIntensity = 0f;
            if (legacyLight != null &&
                legacyLight.targetSortingLayers != null)
            {
                light.targetSortingLayers =
                    legacyLight.targetSortingLayers;
            }

            light.enabled = true;
        }

        private void UpdateOccludedCone()
        {
            if (!HasCompleteOccludedLightSet() ||
                _aimPivot == null ||
                !_isFlashlightEnabled ||
                !_isOwnerVisionVisible)
            {
                return;
            }

            var origin = (Vector2)_aimPivot.position;
            for (var bandIndex = 0;
                 bandIndex < _occludedConeShapePaths.Length;
                 bandIndex++)
            {
                _occludedConeShapePaths[bandIndex][0] = Vector3.zero;
            }

            for (var rayIndex = 0;
                 rayIndex < OcclusionRayCount;
                 rayIndex++)
            {
                var normalized = rayIndex / (OcclusionRayCount - 1f);
                var angle = Mathf.Lerp(
                    -OccludedConeHalfAngle,
                    OccludedConeHalfAngle,
                    normalized);
                var localDirection =
                    (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.up);
                var worldDirection =
                    (Vector2)_aimPivot.TransformDirection(localDirection);
                var visibleDistance = ResolveVisibleDistance(
                    origin,
                    worldDirection.normalized);
                for (var bandIndex = 0;
                     bandIndex < _occludedConeShapePaths.Length;
                     bandIndex++)
                {
                    var clippedDistance = Mathf.Min(
                        visibleDistance,
                        OccludedLightDistances[bandIndex]);
                    var shapeDistance = Mathf.Max(
                        OcclusionRayOriginOffset,
                        clippedDistance - OccludedLightFalloffSize);
                    _occludedConeShapePaths[bandIndex][rayIndex + 1] =
                        localDirection * shapeDistance;
                }
            }

            for (var bandIndex = 0;
                 bandIndex < _occludedConeLights.Length;
                 bandIndex++)
            {
                _occludedConeLights[bandIndex].SetShapePath(
                    _occludedConeShapePaths[bandIndex]);
            }
        }

        private float ResolveVisibleDistance(
            Vector2 origin,
            Vector2 direction)
        {
            var rayOrigin = origin + direction * OcclusionRayOriginOffset;
            var rayDistance =
                OccludedConeDistance - OcclusionRayOriginOffset;
            var hitCount = Physics2D.Raycast(
                rayOrigin,
                direction,
                _occlusionFilter,
                _occlusionHits,
                rayDistance);
            var visibleDistance = OccludedConeDistance;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _occlusionHits[index];
                if (!IsOccludingCollider(hit.collider))
                {
                    continue;
                }

                visibleDistance = Mathf.Min(
                    visibleDistance,
                    OcclusionRayOriginOffset + hit.distance -
                    OcclusionWallClearance);
            }

            return Mathf.Clamp(
                visibleDistance,
                OcclusionRayOriginOffset,
                OccludedConeDistance);
        }

        private bool IsOccludingCollider(Collider2D collider)
        {
            if (collider == null ||
                collider.isTrigger ||
                collider.transform == transform ||
                collider.transform.IsChildOf(transform))
            {
                return false;
            }

            // 플레이어와 괴물은 후레시 범위를 잘라 방 가장자리에 검은 줄을 만들지
            // 않는다. 벽·닫힌 문·고정 프롭처럼 정적인 월드 충돌체만 차폐한다.
            var attachedBody = collider.attachedRigidbody;
            return attachedBody == null ||
                   attachedBody.bodyType == RigidbodyType2D.Static;
        }

        private void ApplyFlashlightVisualState()
        {
            var shouldShow =
                _isFlashlightEnabled && _isOwnerVisionVisible;
            if (_flashlightVisual != null)
            {
                _flashlightVisual.SetActive(shouldShow);
            }

            if (!Application.isPlaying)
            {
                return;
            }

            HideLegacyCone();
            if (_occludedConeObject != null)
            {
                _occludedConeObject.SetActive(shouldShow);
            }
        }

        private void HideLegacyCone()
        {
            if (_flashlightVisual == null)
            {
                return;
            }

            if (_flashlightVisual.TryGetComponent<SpriteRenderer>(
                    out var legacyRenderer))
            {
                legacyRenderer.enabled = false;
            }

            if (_flashlightVisual.TryGetComponent<Light2D>(
                    out var legacyLight))
            {
                legacyLight.enabled = false;
            }

            if (_flashlight != null)
            {
                _flashlight.enabled = false;
            }
        }

        private void ApplyAimRotation()
        {
            if (_aimPivot == null || _aim == null)
            {
                return;
            }

            _aimPivot.localRotation = Quaternion.Euler(
                0f,
                0f,
                _aim.AimAngleDegrees);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _input == null)
            {
                return;
            }

            _input.FlashlightPressed += Toggle;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _input == null)
            {
                return;
            }

            _input.FlashlightPressed -= Toggle;
            _isSubscribed = false;
        }
    }
}
