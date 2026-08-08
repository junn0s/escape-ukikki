using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 상호작용 가능한 설치물의 알파 실루엣을 복제한 테두리다. 플레이어가
    /// 다가올수록 조금 두꺼워지고 밝아져 배경 장식과 구분된다.
    ///
    /// 판정에 관여하지 않는 로컬 표현이며 색은 아트 가이드 §1.2의
    /// "상호작용 가능 오브젝트는 약한 청록 발광"을 따른다.
    /// </summary>
    public sealed class InteractableHighlight : MonoBehaviour
    {
        private static readonly List<InteractableHighlight> ActiveHighlights =
            new();

        /// <summary>멀리서도 조작 대상임은 알 수 있어야 하므로 0이 아니다.</summary>
        private const float IdleMargin = 0.04f;

        private const float FocusedMargin = 0.16f;
        private const float IdleSilhouetteScale = 1.015f;
        private const float FocusedSilhouetteScale = 1.06f;
        private const float BlendSpeed = 9f;

        private static readonly Color IdleColor =
            new(0.20f, 0.82f, 0.86f, 0.03f);
        private static readonly Color FocusedColor =
            new(0.42f, 0.98f, 1f, 0.65f);

        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private Vector2 _baseSize = Vector2.one;
        [SerializeField] private bool _usesSilhouette;

        private float _proximity;
        private float _displayedProximity;

        public static IReadOnlyList<InteractableHighlight> All =>
            ActiveHighlights;

        public void Configure(SpriteRenderer outline, Vector2 baseSize)
        {
            _outline = outline;
            _baseSize = baseSize;
            _usesSilhouette = false;
            UpgradeLegacyFinalEquipmentHighlight();
            ApplyImmediate();
        }

        /// <param name="proximity">0이면 멀고 1이면 상호작용 사거리 안이다.</param>
        public void SetProximity(float proximity)
        {
            _proximity = Mathf.Clamp01(proximity);
        }

        private void OnEnable()
        {
            UpgradeLegacyFinalEquipmentHighlight();
            ActiveHighlights.Add(this);
            _displayedProximity = _proximity;
            ApplyImmediate();
        }

        private void OnDisable()
        {
            ActiveHighlights.Remove(this);
        }

        private void LateUpdate()
        {
            if (Mathf.Approximately(_displayedProximity, _proximity))
            {
                return;
            }

            _displayedProximity = Mathf.MoveTowards(
                _displayedProximity,
                _proximity,
                BlendSpeed * Time.unscaledDeltaTime);
            ApplyImmediate();
        }

        private void ApplyImmediate()
        {
            if (_outline == null)
            {
                return;
            }

            var margin = Mathf.Lerp(
                IdleMargin,
                FocusedMargin,
                _displayedProximity);
            if (_usesSilhouette)
            {
                var scale = Mathf.Lerp(
                    IdleSilhouetteScale,
                    FocusedSilhouetteScale,
                    _displayedProximity);
                _outline.transform.localScale = new Vector3(
                    _baseSize.x * scale,
                    _baseSize.y * scale,
                    1f);
            }
            else
            {
                _outline.transform.localScale = new Vector3(
                    _baseSize.x + margin * 2f,
                    _baseSize.y + margin * 2f,
                    1f);
            }

            _outline.color = Color.Lerp(
                IdleColor,
                FocusedColor,
                _displayedProximity);
        }

        private void UpgradeLegacyFinalEquipmentHighlight()
        {
            var source = transform.Find("FinalEquipmentVisual")?
                .GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null || _outline == null)
            {
                return;
            }

            ApplySilhouetteSource(source);
        }

        private void ApplySilhouetteSource(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || _outline == null)
            {
                _usesSilhouette = false;
                return;
            }

            _outline.sprite = source.sprite;
            _outline.drawMode = SpriteDrawMode.Simple;
            _outline.transform.SetParent(transform, worldPositionStays: false);
            _outline.transform.localPosition = source.transform.localPosition;
            _outline.transform.localRotation = source.transform.localRotation;
            _baseSize = new Vector2(
                source.transform.localScale.x,
                source.transform.localScale.y);
            _outline.sortingOrder = source.sortingOrder - 1;
            _usesSilhouette = true;
        }
    }
}
