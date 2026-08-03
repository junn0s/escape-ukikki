using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 화면의 손상 로그 조각을 키보드 명령으로 순서대로 복구한다.
    /// 틀린 키는 서버에서 즉시 실패로 판정한다.
    /// </summary>
    public sealed class ServerLogRecoveryMissionInstance
    {
        private readonly int[] _requiredTokens;

        public ServerLogRecoveryMissionInstance(
            IReadOnlyList<int> requiredTokens)
        {
            if (requiredTokens == null)
            {
                throw new ArgumentNullException(nameof(requiredTokens));
            }

            if (requiredTokens.Count < FuseMissionInstance.MinimumFuseCount ||
                requiredTokens.Count > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredTokens));
            }

            _requiredTokens = new int[requiredTokens.Count];
            for (var index = 0; index < requiredTokens.Count; index++)
            {
                var token = requiredTokens[index];
                if (token < 1 || token > requiredTokens.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(requiredTokens));
                }

                _requiredTokens[index] = token;
            }
        }

        public MissionState State { get; private set; } =
            MissionState.Assigned;
        public int ProgressIndex { get; private set; }
        public int TokenCount => _requiredTokens.Length;

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                ProgressIndex = 0;
                State = MissionState.InProgress;
            }
        }

        public int GetRequiredToken(int index)
        {
            return index >= 0 && index < _requiredTokens.Length
                ? _requiredTokens[index]
                : 0;
        }

        public FuseMissionInputResult SubmitToken(int token)
        {
            if (State != MissionState.InProgress)
            {
                return FuseMissionInputResult.Ignored;
            }

            if (_requiredTokens[ProgressIndex] != token)
            {
                State = MissionState.Failed;
                return FuseMissionInputResult.Failed;
            }

            ProgressIndex++;
            if (ProgressIndex < _requiredTokens.Length)
            {
                return FuseMissionInputResult.Accepted;
            }

            State = MissionState.Completed;
            return FuseMissionInputResult.Completed;
        }

        public void Cancel()
        {
            if (State == MissionState.InProgress)
            {
                State = MissionState.Cancelled;
            }
        }
    }
}
