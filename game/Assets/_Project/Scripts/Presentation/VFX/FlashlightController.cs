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
        private const float OcclusionNearRingDistance = 0.32f;
        private const int OcclusionRayCount = 49;
        private const int OcclusionHitCapacity = 16;
        private const int OcclusionSortingOrder = 1000;
        private static readonly Color OcclusionColor =
            new(0.46f, 0.88f, 1f, 1f);

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
        private Mesh _occludedConeMesh;
        private MeshRenderer _occludedConeRenderer;
        private Vector3[] _occludedConeVertices;
        private Color[] _occludedConeColors;

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
        /// 네트워크 플레이어의 시야 연출은 소유자에게만 표시한다. 벽 차폐 원뿔은
        /// 런타임 메시라 기존 직렬화된 렌더러 배열에 없으므로 컨트롤러가 직접 끈다.
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

        private void OnDestroy()
        {
            if (_occludedConeMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_occludedConeMesh);
                }
                else
                {
                    DestroyImmediate(_occludedConeMesh);
                }
            }
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
            // 빌더가 프리팹을 편집하는 동안에는 런타임 Mesh를 에셋에 직렬화하지 않는다.
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

            // 정상 생성이 끝난 원뿔만 그대로 사용한다. 스크립트 리로드 중
            // GameObject만 남고 Mesh 컴포넌트가 사라진 경우에는 아래에서 복구한다.
            if (_occludedConeObject != null &&
                _occludedConeMesh != null &&
                _occludedConeRenderer != null)
            {
                HideLegacyCone();
                return;
            }

            if (!_occlusionFilter.useLayerMask)
            {
                ConfigureOcclusionFilter();
            }

            var legacyRenderer =
                _flashlightVisual.GetComponent<SpriteRenderer>();
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

            var meshFilter = coneObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = coneObject.AddComponent<MeshFilter>();
            }

            _occludedConeRenderer =
                coneObject.GetComponent<MeshRenderer>();
            if (_occludedConeRenderer == null)
            {
                _occludedConeRenderer =
                    coneObject.AddComponent<MeshRenderer>();
            }

            // UnityEngine.Object는 삭제 뒤 C# 참조가 남을 수 있으므로 ?? 대신
            // Unity의 null 비교를 사용한다. 그래야 MissingComponentException 없이
            // 리로드 직후에도 컴포넌트를 실제로 다시 붙인다.
            _occludedConeRenderer.sharedMaterial =
                legacyRenderer != null
                    ? legacyRenderer.sharedMaterial
                    : null;
            _occludedConeRenderer.sortingLayerID =
                legacyRenderer != null
                    ? legacyRenderer.sortingLayerID
                    : 0;
            _occludedConeRenderer.sortingOrder = OcclusionSortingOrder;

            _occludedConeMesh = new Mesh
            {
                name = "M_RuntimeFlashlightOcclusion"
            };
            _occludedConeMesh.MarkDynamic();
            var previousRuntimeMesh = meshFilter.sharedMesh;
            if (previousRuntimeMesh != null &&
                previousRuntimeMesh.name == _occludedConeMesh.name)
            {
                Destroy(previousRuntimeMesh);
            }

            meshFilter.sharedMesh = _occludedConeMesh;
            InitializeOccludedConeMesh();

            _occludedConeObject = coneObject;
            HideLegacyCone();
            ApplyFlashlightVisualState();
        }

        private void InitializeOccludedConeMesh()
        {
            var vertexCount = 1 + OcclusionRayCount * 2;
            _occludedConeVertices = new Vector3[vertexCount];
            _occludedConeColors = new Color[vertexCount];
            var uv = new Vector2[vertexCount];
            for (var index = 0; index < uv.Length; index++)
            {
                uv[index] = new Vector2(0.5f, 0.5f);
            }

            var triangles = new int[(OcclusionRayCount - 1) * 9];
            var triangleIndex = 0;
            for (var rayIndex = 0;
                 rayIndex < OcclusionRayCount - 1;
                 rayIndex++)
            {
                var near = 1 + rayIndex * 2;
                var far = near + 1;
                var nextNear = near + 2;
                var nextFar = far + 2;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = near;
                triangles[triangleIndex++] = nextNear;

                triangles[triangleIndex++] = near;
                triangles[triangleIndex++] = far;
                triangles[triangleIndex++] = nextFar;

                triangles[triangleIndex++] = near;
                triangles[triangleIndex++] = nextFar;
                triangles[triangleIndex++] = nextNear;
            }

            _occludedConeMesh.vertices = _occludedConeVertices;
            _occludedConeMesh.colors = _occludedConeColors;
            _occludedConeMesh.uv = uv;
            _occludedConeMesh.triangles = triangles;
        }

        private void UpdateOccludedCone()
        {
            if (_occludedConeMesh == null ||
                _aimPivot == null ||
                !_isFlashlightEnabled ||
                !_isOwnerVisionVisible)
            {
                return;
            }

            var origin = (Vector2)_aimPivot.position;
            _occludedConeVertices[0] = Vector3.zero;
            _occludedConeColors[0] = WithAlpha(OcclusionColor, 0.24f);
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
                var nearDistance = Mathf.Min(
                    OcclusionNearRingDistance,
                    visibleDistance);
                var edgeFade = Mathf.Sin(normalized * Mathf.PI);
                var near = 1 + rayIndex * 2;
                var far = near + 1;
                _occludedConeVertices[near] =
                    localDirection * nearDistance;
                _occludedConeVertices[far] =
                    localDirection * visibleDistance;
                _occludedConeColors[near] =
                    WithAlpha(OcclusionColor, 0.24f * edgeFade);
                _occludedConeColors[far] =
                    WithAlpha(OcclusionColor, 0.07f * edgeFade);
            }

            _occludedConeMesh.vertices = _occludedConeVertices;
            _occludedConeMesh.colors = _occludedConeColors;
            _occludedConeMesh.RecalculateBounds();
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
