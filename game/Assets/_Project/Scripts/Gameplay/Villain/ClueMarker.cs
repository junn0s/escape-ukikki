using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Villain
{
    /// <summary>
    /// 월드에 놓인 현장 단서 한 개다.
    /// 생성 전에는 숨어 있고, 활성화되면 라운드가 끝날 때까지 그대로 남는다.
    /// 자동 소멸 타이머를 두지 않는다(GDD §15.1).
    /// </summary>
    public sealed class ClueMarker : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private ClueKind _kind;
        [SerializeField] private int _clueId;
        [SerializeField] private string _roomId;
        [SerializeField] private Color _activeColor = new(0.95f, 0.15f, 0.15f, 0.85f);

        private readonly List<SpriteRenderer> _effectRenderers = new();
        private ClueState _state = ClueState.Inactive;

        public event Action<ClueMarker> StateChanged;

        public ClueKind Kind => _kind;
        public int ClueId => _clueId;
        public string RoomId => _roomId;
        public ClueState State => _state;
        public bool IsActive => _state != ClueState.Inactive;

        public string DisplayName => _kind switch
        {
            ClueKind.VentRedSmoke => "환풍구의 붉은 연기",
            ClueKind.BrokenQuarantineLock => "파손된 격리실 잠금장치",
            ClueKind.EmptySyringe => "바닥의 빈 주사기",
            ClueKind.SpeakerRedLed => "스피커의 붉은 LED",
            _ => "현장 단서"
        };

        public void Configure(
            SpriteRenderer markerRenderer,
            ClueKind kind,
            int clueId,
            string roomId)
        {
            _renderer = markerRenderer;
            _kind = kind;
            _clueId = clueId;
            _roomId = roomId;
        }

        private void Awake()
        {
            if (_renderer == null)
            {
                Debug.LogError("[Clue] Marker renderer is missing.", this);
                return;
            }

            CreateKindPresentation();
            ApplyState();
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            switch (_kind)
            {
                case ClueKind.VentRedSmoke:
                    AnimateRedSmoke();
                    break;
                case ClueKind.BrokenQuarantineLock:
                    AnimateBrokenLock();
                    break;
                case ClueKind.EmptySyringe:
                    AnimateEmptySyringe();
                    break;
                case ClueKind.SpeakerRedLed:
                    AnimateSpeakerLed();
                    break;
            }
        }

        /// <summary>
        /// 서버가 통보한 상태를 반영한다. 한 번 활성화되면 Inactive로 되돌리지 않는다.
        /// </summary>
        public void ApplyState(ClueState state)
        {
            if (_state == state ||
                (IsActive && state == ClueState.Inactive))
            {
                return;
            }

            _state = state;
            ApplyState();
            StateChanged?.Invoke(this);
        }

        private void ApplyState()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.enabled = IsActive;
            for (var index = 0; index < _effectRenderers.Count; index++)
            {
                if (_effectRenderers[index] != null)
                {
                    _effectRenderers[index].enabled = IsActive;
                }
            }

            if (!IsActive)
            {
                return;
            }

            // 조사된 단서는 약간 어둡게 표시해 이미 확인했음을 알린다.
            var color = ResolveKindColor();
            color.a *= InspectionAlpha;

            _renderer.color = color;
        }

        private float InspectionAlpha =>
            _state == ClueState.ActiveInspected ? 0.6f : 1f;

        private Color ResolveKindColor()
        {
            return _kind switch
            {
                ClueKind.VentRedSmoke =>
                    new Color(0.52f, 0.035f, 0.025f, 0.62f),
                ClueKind.BrokenQuarantineLock =>
                    new Color(1f, 0.46f, 0.04f, 0.96f),
                ClueKind.EmptySyringe =>
                    new Color(0.66f, 0.92f, 1f, 0.96f),
                ClueKind.SpeakerRedLed =>
                    new Color(1f, 0.04f, 0.025f, 1f),
                _ => _activeColor
            };
        }

        private void CreateKindPresentation()
        {
            switch (_kind)
            {
                case ClueKind.VentRedSmoke:
                    for (var index = 0; index < 6; index++)
                    {
                        CreateEffectPart(
                            $"SmokePuff_{index + 1:00}",
                            new Vector3(0f, -0.32f, 0f),
                            Vector3.one * 0.18f,
                            new Color(0.95f, 0.04f, 0.025f, 0f));
                    }
                    break;
                case ClueKind.BrokenQuarantineLock:
                    CreateEffectPart(
                        "BrokenLock_Left",
                        new Vector3(-0.28f, 0.02f, 0f),
                        new Vector3(0.42f, 0.9f, 1f),
                        new Color(0.38f, 0.06f, 0.025f, 0.95f),
                        18f);
                    CreateEffectPart(
                        "BrokenLock_Right",
                        new Vector3(0.28f, -0.03f, 0f),
                        new Vector3(0.42f, 0.9f, 1f),
                        new Color(0.55f, 0.08f, 0.025f, 0.95f),
                        -16f);
                    CreateEffectPart(
                        "BrokenLock_WarningSlash",
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0.12f, 1.25f, 1f),
                        new Color(1f, 0.12f, 0.03f, 0.9f),
                        42f);
                    break;
                case ClueKind.EmptySyringe:
                    CreateEffectPart(
                        "Syringe_Needle",
                        new Vector3(0.72f, 0f, 0f),
                        new Vector3(0.72f, 0.07f, 1f),
                        new Color(0.85f, 0.98f, 1f, 0.95f));
                    CreateEffectPart(
                        "Syringe_Plunger",
                        new Vector3(-0.56f, 0f, 0f),
                        new Vector3(0.18f, 0.72f, 1f),
                        new Color(0.38f, 0.74f, 0.86f, 0.95f));
                    CreateEffectPart(
                        "Syringe_Residue",
                        new Vector3(0.15f, -0.34f, 0f),
                        new Vector3(0.2f, 0.2f, 1f),
                        new Color(0.45f, 1f, 0.64f, 0.72f));
                    break;
            }
        }

        private SpriteRenderer CreateEffectPart(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            float localRotationDegrees = 0f)
        {
            var partObject = new GameObject(objectName);
            partObject.transform.SetParent(transform, worldPositionStays: false);
            partObject.transform.localPosition = localPosition;
            partObject.transform.localScale = localScale;
            partObject.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                localRotationDegrees);
            var partRenderer = partObject.AddComponent<SpriteRenderer>();
            partRenderer.sprite = _renderer.sprite;
            partRenderer.sharedMaterial = _renderer.sharedMaterial;
            partRenderer.sortingLayerID = _renderer.sortingLayerID;
            partRenderer.sortingOrder = _renderer.sortingOrder + 1;
            partRenderer.color = color;
            partRenderer.enabled = IsActive;
            _effectRenderers.Add(partRenderer);
            return partRenderer;
        }

        private void AnimateRedSmoke()
        {
            var time = Time.unscaledTime;
            for (var index = 0; index < _effectRenderers.Count; index++)
            {
                var puff = _effectRenderers[index];
                if (puff == null)
                {
                    continue;
                }

                var life = Mathf.Repeat(
                    time * 0.23f + index / (float)_effectRenderers.Count,
                    1f);
                puff.transform.localPosition = new Vector3(
                    Mathf.Sin(time * 1.8f + index * 1.7f) *
                    (0.08f + life * 0.18f),
                    -0.36f + life * 0.95f,
                    0f);
                var scale = 0.14f + life * 0.42f;
                puff.transform.localScale = Vector3.one * scale;
                puff.color = new Color(
                    0.95f,
                    0.035f,
                    0.02f,
                    (1f - life) * 0.58f * InspectionAlpha);
            }
        }

        private void AnimateBrokenLock()
        {
            var pulse = 0.72f +
                        (Mathf.Sin(Time.unscaledTime * 4.8f) * 0.5f + 0.5f) *
                        0.28f;
            for (var index = 0; index < _effectRenderers.Count; index++)
            {
                var part = _effectRenderers[index];
                if (part == null)
                {
                    continue;
                }

                var color = part.color;
                color.a = pulse * InspectionAlpha;
                part.color = color;
            }
        }

        private void AnimateEmptySyringe()
        {
            var glint = 0.62f +
                        (Mathf.Sin(Time.unscaledTime * 3.1f) * 0.5f + 0.5f) *
                        0.38f;
            for (var index = 0; index < _effectRenderers.Count; index++)
            {
                var part = _effectRenderers[index];
                if (part == null)
                {
                    continue;
                }

                var color = part.color;
                color.a = glint * InspectionAlpha;
                part.color = color;
            }
        }

        private void AnimateSpeakerLed()
        {
            var color = ResolveKindColor();
            color.a =
                (0.48f +
                 (Mathf.Sin(Time.unscaledTime * 6.4f) * 0.5f + 0.5f) *
                 0.52f) * InspectionAlpha;
            _renderer.color = color;
        }
    }
}
