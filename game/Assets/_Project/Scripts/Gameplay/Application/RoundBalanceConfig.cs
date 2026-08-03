using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Round", fileName = "SO_RoundBalance_Default")]
    public sealed class RoundBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "round_default";
        [SerializeField, Min(0f)] private float _roleRevealSeconds = 5f;
        [SerializeField, Min(0f)] private float _initialGracePeriodSeconds = 30f;
        [SerializeField, Min(1f)] private float _explorationDurationSeconds = 900f;
        [SerializeField, Min(0f)] private float _resultDisplaySeconds = 5f;
        [SerializeField, Min(1)] private int _projectMaximumPoints = 10000;
        [SerializeField, Min(1)] private int _survivorPersonalBudgetPoints = 2000;
        [SerializeField, Min(1)] private int _defaultAssignedMissionCount = 5;
        [SerializeField, Min(1)] private int _difficultAssignedMissionCount = 4;
        [SerializeField, Min(1)] private int _minimumMissionKindCount = 3;
        [SerializeField, Min(0f)] private float _firstMeetingLockSeconds = 120f;
        [SerializeField, Min(0f)] private float _meetingCooldownSeconds = 120f;
        [SerializeField, Min(1)] private int _maximumMeetingCount = 3;
        [SerializeField, Min(1f)] private float _meetingDiscussionSeconds = 90f;
        [SerializeField, Min(1f)] private float _meetingVoteSeconds = 30f;
        [SerializeField, Min(0f)] private float _meetingResultSeconds = 5f;
        [SerializeField, Min(0f)] private float _postMeetingBiteProtectionSeconds = 2f;
        [SerializeField, Min(1)] private int _chatMessageMaximumLength = 80;
        [SerializeField, Min(0f)] private float _chatMessageIntervalSeconds = 1f;
        [SerializeField, Min(1)] private int _chatHistoryMaximumCount = 60;
        [SerializeField, Min(0f)] private float _disconnectGraceSeconds = 30f;

        public string Id => _id;
        public float RoleRevealSeconds => _roleRevealSeconds;
        public float InitialGracePeriodSeconds => _initialGracePeriodSeconds;
        public float ExplorationDurationSeconds => _explorationDurationSeconds;
        public float ResultDisplaySeconds => _resultDisplaySeconds;
        public int ProjectMaximumPoints => _projectMaximumPoints;
        public int SurvivorPersonalBudgetPoints =>
            _survivorPersonalBudgetPoints;
        public int DefaultAssignedMissionCount =>
            _defaultAssignedMissionCount;
        public int DifficultAssignedMissionCount => Mathf.Min(
            _difficultAssignedMissionCount,
            DefaultAssignedMissionCount);
        public int MinimumMissionKindCount => Mathf.Min(
            _minimumMissionKindCount,
            DifficultAssignedMissionCount);
        public float FirstMeetingLockSeconds => _firstMeetingLockSeconds;
        public float MeetingCooldownSeconds => _meetingCooldownSeconds;
        public int MaximumMeetingCount => _maximumMeetingCount;
        public float MeetingDiscussionSeconds => _meetingDiscussionSeconds;
        public float MeetingVoteSeconds => _meetingVoteSeconds;
        public float MeetingResultSeconds => _meetingResultSeconds;
        public float PostMeetingBiteProtectionSeconds =>
            _postMeetingBiteProtectionSeconds;

        /// <summary>토론 채팅 한 줄의 최대 글자 수다(docs/ui-ux-design.md §11.1).</summary>
        public int ChatMessageMaximumLength => _chatMessageMaximumLength;

        /// <summary>같은 플레이어의 연속 전송 최소 간격이다. 도배를 막는다.</summary>
        public float ChatMessageIntervalSeconds => _chatMessageIntervalSeconds;

        /// <summary>서버가 보관하고 복제하는 최근 메시지 수다.</summary>
        public int ChatHistoryMaximumCount => _chatHistoryMaximumCount;

        /// <summary>
        /// 연결이 끊긴 참가자의 복귀를 기다리는 시간이다(GDD §19.2).
        /// 이 시간이 지나기 전에는 승패 판정을 확정하지 않는다.
        /// </summary>
        public float DisconnectGraceSeconds => _disconnectGraceSeconds;
    }
}
