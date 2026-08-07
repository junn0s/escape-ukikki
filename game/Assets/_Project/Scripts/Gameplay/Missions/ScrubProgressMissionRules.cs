using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 화면을 문질러 누적 진행률을 채우는 미션의 순수 판정이다(GDD §10.2
    /// CCTV 화면 닦기). 얼룩마다 개별 판정을 하는 슬라이드 글라스 닦기와
    /// 달리 문지른 횟수를 단일 진행률로 누적한다. 서버에서만 갱신하고
    /// 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class ScrubProgressMissionRules
    {
        private readonly int _requiredScrubs;

        public ScrubProgressMissionRules(int requiredScrubs)
        {
            _requiredScrubs = requiredScrubs;
        }

        public int ScrubCount { get; private set; }
        public bool IsCompleted { get; private set; }

        public float GetProgressNormalized()
        {
            return _requiredScrubs > 0
                ? Mathf.Clamp01((float)ScrubCount / _requiredScrubs)
                : 0f;
        }

        public bool TryScrub()
        {
            if (IsCompleted)
            {
                return false;
            }

            ScrubCount++;
            if (ScrubCount < _requiredScrubs)
            {
                return false;
            }

            ScrubCount = _requiredScrubs;
            IsCompleted = true;
            return true;
        }

        public void Reset()
        {
            ScrubCount = 0;
            IsCompleted = false;
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(int scrubCount, bool isCompleted)
        {
            ScrubCount = scrubCount;
            IsCompleted = isCompleted;
        }
    }
}
