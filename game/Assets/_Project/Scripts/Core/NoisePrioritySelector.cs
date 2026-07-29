using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 괴물 하나가 여러 소음 중 어떤 것에 반응할지 고르는 규칙.
    /// docs/system-design-document.md §9.2의 5단계 비교를 그대로 구현한다.
    ///
    /// 마지막 NoiseId 비교는 취향이 아니라 결정성 요구다. 동점 상황에서 순서가 흔들리면
    /// 호스트와 클라이언트의 재생 결과가 갈라진다.
    /// </summary>
    public static class NoisePrioritySelector
    {
        /// <summary>
        /// 경로 거리가 반경 이내인 후보 중 우선순위가 가장 높은 소음을 고른다.
        /// 유효 후보가 없으면 false를 반환한다.
        /// </summary>
        /// <param name="candidates">평가할 소음 목록</param>
        /// <param name="pathDistances">candidates와 같은 순서의 NavMesh 경로 거리. 도달 불가는 음수</param>
        public static bool TrySelect(
            IReadOnlyList<NoiseEvent> candidates,
            IReadOnlyList<float> pathDistances,
            out NoiseEvent selected)
        {
            selected = default;

            if (candidates == null || pathDistances == null)
            {
                return false;
            }

            if (candidates.Count != pathDistances.Count)
            {
                throw new System.ArgumentException(
                    $"후보 {candidates.Count}개와 거리 {pathDistances.Count}개의 수가 다르다.",
                    nameof(pathDistances));
            }

            bool found = false;
            float bestDistance = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                NoiseEvent candidate = candidates[i];
                float distance = pathDistances[i];

                // 1. 경로 거리가 반경 이내인지. 음수는 도달 불가를 의미한다.
                if (!candidate.IsValid || distance < 0f || distance > candidate.PathRadius)
                {
                    continue;
                }

                if (!found || IsHigherPriority(candidate, distance, selected, bestDistance))
                {
                    selected = candidate;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsHigherPriority(
            in NoiseEvent candidate,
            float candidateDistance,
            in NoiseEvent current,
            float currentDistance)
        {
            // 2. 경로 거리가 더 짧은 후보
            if (!Mathf.Approximately(candidateDistance, currentDistance))
            {
                return candidateDistance < currentDistance;
            }

            // 3. 강도가 더 큰 후보
            if (candidate.Intensity != current.Intensity)
            {
                return candidate.Intensity > current.Intensity;
            }

            // 4. 발생 시각이 더 최근인 후보
            if (!Mathf.Approximately(candidate.CreatedTime, current.CreatedTime))
            {
                return candidate.CreatedTime > current.CreatedTime;
            }

            // 5. NoiseId가 작은 후보 (결정성 확보용 최종 규칙)
            return candidate.NoiseId < current.NoiseId;
        }
    }
}
