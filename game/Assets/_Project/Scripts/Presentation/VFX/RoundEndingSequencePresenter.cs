using System;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 라운드 결과를 월드 엔딩으로 표현한다.
    /// 프로젝트 완료는 헬기 도착, 시간 초과는 RX-9 가스 방출,
    /// 빌런 퇴출은 가스 배관 차단을 재생한다(GDD §4.3).
    /// </summary>
    public sealed class RoundEndingSequencePresenter : MonoBehaviour
    {
        private enum SequenceKind : byte
        {
            None = 0,
            Helicopter = 1,
            GasRelease = 2,
            GasShutdown = 3,
            LaboratoryOverrun = 4
        }

        [SerializeField] private Transform _helicopterRoot;
        [SerializeField] private SpriteRenderer[] _gasRenderers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private TopDownCamera _worldCamera;
        [SerializeField] private PresentationAssetCatalog _assetCatalog;
        [SerializeField] private Vector3 _helicopterApproachPosition;
        [SerializeField] private Vector3 _helicopterLandingPosition;
        [SerializeField] private Vector3 _helicopterDeparturePosition;
        [SerializeField, Min(0.1f)] private float _approachSeconds = 4.5f;
        [SerializeField, Min(0.1f)] private float _hoverSeconds = 3f;
        [SerializeField, Min(0.1f)] private float _departureSeconds = 3.5f;
        [SerializeField, Min(0.1f)] private float _gasWarningSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float _gasExpansionSeconds = 4f;

        private NetworkRoundState _roundState;
        private SequenceKind _sequenceKind;
        private float _sequenceStartedAt;
        private bool _hasStartedForCurrentRound;
        private GameObject _spawnedHelicopter;
        private readonly GameObject[] _spawnedGasVfx = new GameObject[2];
        private Vector3[] _gasBaseScales = Array.Empty<Vector3>();
        private Transform _previousCameraTarget;
        private bool _isUsingGasPrefab;

        public event Action<EndingPresentationCue> CueRaised;

        public EndingPresentationCue CurrentCue { get; private set; }

        public void Configure(
            Transform helicopterRoot,
            SpriteRenderer[] gasRenderers,
            AudioSource audioSource,
            TopDownCamera worldCamera,
            Vector3 helicopterApproachPosition,
            Vector3 helicopterLandingPosition,
            Vector3 helicopterDeparturePosition,
            PresentationAssetCatalog assetCatalog = null)
        {
            _helicopterRoot = helicopterRoot;
            _gasRenderers = gasRenderers ?? Array.Empty<SpriteRenderer>();
            _audioSource = audioSource;
            _worldCamera = worldCamera;
            _helicopterApproachPosition = helicopterApproachPosition;
            _helicopterLandingPosition = helicopterLandingPosition;
            _helicopterDeparturePosition = helicopterDeparturePosition;
            _assetCatalog = assetCatalog;
            CaptureGasBaseScales();
            PrepareVisuals();
        }

        private void OnEnable()
        {
            NetworkRoundState.CurrentChanged += BindRound;
            BindRound();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            UnbindRound();
            RestoreCameraTarget();
        }

        private void OnDestroy()
        {
            if (_spawnedHelicopter != null)
            {
                Destroy(_spawnedHelicopter);
            }

            for (var index = 0; index < _spawnedGasVfx.Length; index++)
            {
                if (_spawnedGasVfx[index] != null)
                {
                    Destroy(_spawnedGasVfx[index]);
                }
            }
        }

        private void Update()
        {
            if (!_hasStartedForCurrentRound)
            {
                TryStartSequence();
            }

            var elapsed = Time.unscaledTime - _sequenceStartedAt;
            switch (_sequenceKind)
            {
                case SequenceKind.Helicopter:
                    AnimateHelicopter(elapsed);
                    break;
                case SequenceKind.GasRelease:
                    AnimateGasRelease(elapsed);
                    break;
                case SequenceKind.GasShutdown:
                    AnimateGasShutdown(elapsed);
                    break;
                case SequenceKind.LaboratoryOverrun:
                    AnimateLaboratoryOverrun(elapsed);
                    break;
            }
        }

        private void BindRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += HandleRoundStateChanged;
            }

            _hasStartedForCurrentRound = false;
            _sequenceKind = SequenceKind.None;
            PrepareVisuals();
            TryStartSequence();
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
            TryStartSequence();
        }

        private void TryStartSequence()
        {
            if (_hasStartedForCurrentRound ||
                _roundState == null ||
                _roundState.Phase != RoundPhase.RoundResult ||
                _roundState.Outcome == RoundOutcome.None)
            {
                return;
            }

            _hasStartedForCurrentRound = true;
            _sequenceStartedAt = Time.unscaledTime;
            _sequenceKind = ResolveSequence(
                _roundState.Outcome,
                _roundState.EndReason);
            switch (_sequenceKind)
            {
                case SequenceKind.Helicopter:
                    ActivateHelicopter();
                    SetEndingCameraTarget(GetHelicopterTransform());
                    RaiseCue(EndingPresentationCue.HelicopterApproach);
                    PlayOneShot(_assetCatalog?.HelicopterApproachClip);
                    break;
                case SequenceKind.GasRelease:
                    SetEndingCameraTarget(GetFirstGasTransform());
                    RaiseCue(EndingPresentationCue.GasWarning);
                    break;
                case SequenceKind.GasShutdown:
                    SetGasVisible(true);
                    SetEndingCameraTarget(GetFirstGasTransform());
                    RaiseCue(EndingPresentationCue.GasShutdown);
                    SpawnGasVfx(_assetCatalog?.GasShutdownVfxPrefab);
                    break;
                case SequenceKind.LaboratoryOverrun:
                    RaiseCue(EndingPresentationCue.LaboratoryOverrun);
                    break;
            }
        }

        private static SequenceKind ResolveSequence(
            RoundOutcome outcome,
            RoundEndReason reason)
        {
            if (outcome == RoundOutcome.SurvivorsWin)
            {
                return reason == RoundEndReason.ProjectCompleted
                    ? SequenceKind.Helicopter
                    : SequenceKind.GasShutdown;
            }

            return reason == RoundEndReason.TimeExpired
                ? SequenceKind.GasRelease
                : SequenceKind.LaboratoryOverrun;
        }

        private void AnimateHelicopter(float elapsed)
        {
            var root = GetHelicopterTransform();
            if (root == null)
            {
                return;
            }

            if (elapsed < _approachSeconds)
            {
                var normalized = Mathf.SmoothStep(
                    0f,
                    1f,
                    elapsed / _approachSeconds);
                root.position = Vector3.Lerp(
                    _helicopterApproachPosition,
                    _helicopterLandingPosition,
                    normalized);
                return;
            }

            if (elapsed < _approachSeconds + _hoverSeconds)
            {
                if (CurrentCue != EndingPresentationCue.HelicopterLanded)
                {
                    RaiseCue(EndingPresentationCue.HelicopterLanded);
                }

                var bob = Mathf.Sin(elapsed * 4.8f) * 0.12f;
                root.position = _helicopterLandingPosition +
                                new Vector3(0f, bob, 0f);
                return;
            }

            var departureElapsed =
                elapsed - _approachSeconds - _hoverSeconds;
            var departureNormalized = Mathf.Clamp01(
                departureElapsed / _departureSeconds);
            root.position = Vector3.Lerp(
                _helicopterLandingPosition,
                _helicopterDeparturePosition,
                Mathf.SmoothStep(0f, 1f, departureNormalized));
            if (departureNormalized >= 1f &&
                CurrentCue != EndingPresentationCue.HelicopterDeparted)
            {
                RaiseCue(EndingPresentationCue.HelicopterDeparted);
            }
        }

        private void AnimateGasRelease(float elapsed)
        {
            if (elapsed < _gasWarningSeconds)
            {
                SetGasAlpha(
                    Mathf.PingPong(elapsed * 2.5f, 1f) * 0.3f);
                return;
            }

            if (CurrentCue == EndingPresentationCue.GasWarning)
            {
                RaiseCue(EndingPresentationCue.GasReleased);
                PlayOneShot(_assetCatalog?.GasReleasedClip);
                SpawnGasVfx(_assetCatalog?.GasVfxPrefab);
            }

            var normalized = Mathf.Clamp01(
                (elapsed - _gasWarningSeconds) / _gasExpansionSeconds);
            SetGasAlpha(Mathf.Lerp(0.25f, 0.9f, normalized));
            SetGasScale(Mathf.Lerp(0.35f, 1.8f, normalized));
            if (normalized >= 1f &&
                CurrentCue != EndingPresentationCue.GasSaturated)
            {
                RaiseCue(EndingPresentationCue.GasSaturated);
            }
        }

        private void AnimateGasShutdown(float elapsed)
        {
            var normalized = Mathf.Clamp01(elapsed / _gasExpansionSeconds);
            SetGasAlpha(Mathf.Lerp(0.45f, 0f, normalized));
            SetGasScale(Mathf.Lerp(1f, 0.2f, normalized));
        }

        private void AnimateLaboratoryOverrun(float elapsed)
        {
            // 전멸은 가스 방출과 다른 결말이므로 RX-9 연출을 재생하지 않는다.
        }

        private void PrepareVisuals()
        {
            if (_helicopterRoot != null)
            {
                _helicopterRoot.gameObject.SetActive(false);
                _helicopterRoot.position = _helicopterApproachPosition;
            }

            _isUsingGasPrefab = false;
            SetGasVisible(false);
            SetGasScale(1f);
            CurrentCue = EndingPresentationCue.None;
        }

        private void ActivateHelicopter()
        {
            if (_assetCatalog?.HelicopterPrefab != null)
            {
                _spawnedHelicopter = Instantiate(
                    _assetCatalog.HelicopterPrefab,
                    _helicopterApproachPosition,
                    Quaternion.identity,
                    transform);
                if (_helicopterRoot != null)
                {
                    _helicopterRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (_helicopterRoot != null)
            {
                _helicopterRoot.gameObject.SetActive(true);
                _helicopterRoot.position = _helicopterApproachPosition;
            }
        }

        private Transform GetHelicopterTransform()
        {
            return _spawnedHelicopter != null
                ? _spawnedHelicopter.transform
                : _helicopterRoot;
        }

        private Transform GetFirstGasTransform()
        {
            for (var index = 0; index < _gasRenderers.Length; index++)
            {
                if (_gasRenderers[index] != null)
                {
                    return _gasRenderers[index].transform;
                }
            }

            return null;
        }

        private void SetEndingCameraTarget(Transform target)
        {
            if (_worldCamera == null || target == null)
            {
                return;
            }

            _previousCameraTarget ??= _worldCamera.Target;
            _worldCamera.SetTarget(target, true);
        }

        private void RestoreCameraTarget()
        {
            if (_worldCamera != null && _previousCameraTarget != null)
            {
                _worldCamera.SetTarget(_previousCameraTarget, true);
            }

            _previousCameraTarget = null;
        }

        private void SpawnGasVfx(GameObject prefab)
        {
            if (prefab == null)
            {
                _isUsingGasPrefab = false;
                SetGasVisible(true);
                return;
            }

            _isUsingGasPrefab = true;
            var count = Mathf.Min(
                _spawnedGasVfx.Length,
                _gasRenderers.Length);
            for (var index = 0; index < count; index++)
            {
                if (_gasRenderers[index] == null)
                {
                    continue;
                }

                _spawnedGasVfx[index] = Instantiate(
                    prefab,
                    _gasRenderers[index].transform.position,
                    Quaternion.identity,
                    transform);
                _gasRenderers[index].enabled = false;
            }
        }

        private void SetGasVisible(bool isVisible)
        {
            for (var index = 0; index < _gasRenderers.Length; index++)
            {
                if (_gasRenderers[index] != null)
                {
                    _gasRenderers[index].enabled = isVisible;
                }
            }
        }

        private void SetGasAlpha(float alpha)
        {
            if (_isUsingGasPrefab)
            {
                return;
            }

            SetGasVisible(alpha > 0.001f);
            for (var index = 0; index < _gasRenderers.Length; index++)
            {
                var renderer = _gasRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                var color = renderer.color;
                color.a = Mathf.Clamp01(alpha);
                renderer.color = color;
            }
        }

        private void SetGasScale(float scale)
        {
            if (_isUsingGasPrefab)
            {
                return;
            }

            for (var index = 0; index < _gasRenderers.Length; index++)
            {
                if (_gasRenderers[index] != null)
                {
                    _gasRenderers[index].transform.localScale =
                        (_gasBaseScales.Length > index
                            ? _gasBaseScales[index]
                            : Vector3.one) * scale;
                }
            }
        }

        private void CaptureGasBaseScales()
        {
            _gasBaseScales = new Vector3[_gasRenderers.Length];
            for (var index = 0; index < _gasRenderers.Length; index++)
            {
                _gasBaseScales[index] = _gasRenderers[index] != null
                    ? _gasRenderers[index].transform.localScale
                    : Vector3.one;
            }
        }

        private void RaiseCue(EndingPresentationCue cue)
        {
            CurrentCue = cue;
            CueRaised?.Invoke(cue);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
    }
}
