using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 상호작용 가능한 설치물 뒤에 깔리는 테두리다. 플레이어가 다가올수록
    /// 두꺼워지고 밝아져서, 이것이 그냥 상자가 아니라 조작할 수 있는 대상임을 알린다.
    ///
    /// 테두리는 본체보다 조금 큰 판을 뒤에 깔아 만든다. 여백이 넓어질수록
    /// 테두리가 실제로 두꺼워 보인다. 9-slice 링을 키우면 굵기는 그대로인 채
    /// 바깥으로 밀려나기만 해서 "두꺼워진다"로 읽히지 않는다.
    ///
    /// 판정에 관여하지 않는 로컬 표현이며 색은 아트 가이드 §1.2의
    /// "상호작용 가능 오브젝트는 약한 청록 발광"을 따른다.
    /// </summary>
    public sealed class InteractableHighlight : MonoBehaviour
    {
        private static readonly List<InteractableHighlight> ActiveHighlights =
            new();

        /// <summary>멀리서도 조작 대상임은 알 수 있어야 하므로 0이 아니다.</summary>
        private const float IdleMargin = 0.05f;

        private const float FocusedMargin = 0.24f;
        private const float BlendSpeed = 9f;

        private static readonly Color IdleColor =
            new(0.20f, 0.82f, 0.86f, 0.22f);
        private static readonly Color FocusedColor =
            new(0.42f, 0.98f, 1f, 0.92f);

        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private Vector2 _baseSize = Vector2.one;

        private float _proximity;
        private float _displayedProximity;

        public static IReadOnlyList<InteractableHighlight> All =>
            ActiveHighlights;

        public void Configure(SpriteRenderer outline, Vector2 baseSize)
        {
            _outline = outline;
            _baseSize = baseSize;
            ApplyImmediate();
        }

        /// <param name="proximity">0이면 멀고 1이면 상호작용 사거리 안이다.</param>
        public void SetProximity(float proximity)
        {
            _proximity = Mathf.Clamp01(proximity);
        }

        private void OnEnable()
        {
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
            _outline.transform.localScale = new Vector3(
                _baseSize.x + margin * 2f,
                _baseSize.y + margin * 2f,
                1f);
            _outline.color = Color.Lerp(
                IdleColor,
                FocusedColor,
                _displayedProximity);
        }
    }
}
