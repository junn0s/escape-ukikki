using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 자기 플레이어와의 거리로 주변 설치물의 테두리 강조를 갱신한다.
    ///
    /// 강조는 보는 사람마다 달라야 하므로 반드시 소유자 인스턴스에서만 돈다.
    /// 원격 플레이어가 이걸 돌리면 남이 다가갈 때 내 화면의 설치물이 켜진다.
    /// </summary>
    public sealed class InteractableHighlightDriver : MonoBehaviour
    {
        /// <summary>이 거리 밖에서는 은은한 기본 테두리만 남는다.</summary>
        [SerializeField, Min(0.5f)] private float _revealRangeMeters = 4.5f;

        /// <summary>이 거리 안이면 최대 강조다. 실제 상호작용 사거리와 맞춘다.</summary>
        [SerializeField, Min(0.1f)] private float _focusRangeMeters = 1.5f;

        /// <summary>매 프레임 전부 훑을 필요는 없다.</summary>
        [SerializeField, Min(0.02f)] private float _scanIntervalSeconds = 0.08f;

        private float _nextScanTime;

        public void Configure(float focusRangeMeters)
        {
            _focusRangeMeters = Mathf.Max(0.1f, focusRangeMeters);
        }

        private void OnDisable()
        {
            // 소유권을 잃거나 화면을 나갈 때 켜둔 강조를 남기지 않는다.
            foreach (var highlight in InteractableHighlight.All)
            {
                highlight.SetProximity(0f);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + _scanIntervalSeconds;
            var origin = (Vector2)transform.position;
            var highlights = InteractableHighlight.All;
            for (var index = 0; index < highlights.Count; index++)
            {
                var highlight = highlights[index];
                if (highlight == null)
                {
                    continue;
                }

                var distance = Vector2.Distance(
                    origin,
                    highlight.transform.position);
                highlight.SetProximity(
                    Mathf.InverseLerp(
                        _revealRangeMeters,
                        _focusRangeMeters,
                        distance));
            }
        }
    }
}
