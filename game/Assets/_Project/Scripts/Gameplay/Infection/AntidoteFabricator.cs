using System;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 해독제 제작기 한 대의 상태와 남은 시간이다.
    /// docs/system-design-document.md §12.1의 Idle → Producing → Ready → Idle을 따른다.
    /// MonoBehaviour가 아니므로 서버에서만 갱신하고 테스트에서 직접 검증한다.
    /// </summary>
    public sealed class AntidoteFabricator
    {
        public const ulong NoCrafterClientId = ulong.MaxValue;

        public event Action<AntidoteFabricator> StateChanged;

        public FabricatorState State { get; private set; } =
            FabricatorState.Idle;

        /// <summary>제작을 시작한 생존자다. 완성품 소유권과는 무관하다.</summary>
        public ulong CrafterClientId { get; private set; } = NoCrafterClientId;

        public float RemainingSeconds { get; private set; }

        /// <summary>회의 중에는 제작 타이머가 정지한다(GDD §16.2, SDD §4 상태표).</summary>
        public bool IsPaused { get; private set; }

        public float TotalDurationSeconds { get; private set; }

        public float NormalizedProgress =>
            TotalDurationSeconds > 0f
                ? Mathf.Clamp01(
                    1f - (RemainingSeconds / TotalDurationSeconds))
                : 0f;

        public bool TryBeginCraft(ulong crafterClientId, float durationSeconds)
        {
            if (State != FabricatorState.Idle || durationSeconds <= 0f)
            {
                return false;
            }

            CrafterClientId = crafterClientId;
            TotalDurationSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
            State = FabricatorState.Producing;
            StateChanged?.Invoke(this);
            return true;
        }

        public void SetPaused(bool isPaused)
        {
            IsPaused = isPaused;
        }

        public void Tick(float deltaSeconds)
        {
            if (State != FabricatorState.Producing || IsPaused ||
                deltaSeconds <= 0f)
            {
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0f)
            {
                return;
            }

            State = FabricatorState.Ready;
            StateChanged?.Invoke(this);
        }

        /// <summary>완성품을 가져간다. 선착순 판정은 호출자(서버)가 담당한다.</summary>
        public bool TryCollect()
        {
            if (State != FabricatorState.Ready)
            {
                return false;
            }

            State = FabricatorState.Idle;
            CrafterClientId = NoCrafterClientId;
            RemainingSeconds = 0f;
            TotalDurationSeconds = 0f;
            StateChanged?.Invoke(this);
            return true;
        }

        public void Reset()
        {
            if (State == FabricatorState.Idle && RemainingSeconds <= 0f)
            {
                return;
            }

            State = FabricatorState.Idle;
            CrafterClientId = NoCrafterClientId;
            RemainingSeconds = 0f;
            TotalDurationSeconds = 0f;
            IsPaused = false;
            StateChanged?.Invoke(this);
        }

        /// <summary>클라이언트가 서버 복제 값을 그대로 반영할 때 사용한다.</summary>
        public void ApplyAuthoritativeSnapshot(
            FabricatorState state,
            float remainingSeconds,
            float totalDurationSeconds)
        {
            var hasChanged =
                State != state ||
                !Mathf.Approximately(RemainingSeconds, remainingSeconds);
            State = state;
            RemainingSeconds = remainingSeconds;
            TotalDurationSeconds = totalDurationSeconds;
            if (hasChanged)
            {
                StateChanged?.Invoke(this);
            }
        }
    }
}
