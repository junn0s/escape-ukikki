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
        [SerializeField, Min(0f)] private float _firstMeetingLockSeconds = 120f;
        [SerializeField, Min(0f)] private float _meetingCooldownSeconds = 120f;
        [SerializeField, Min(1)] private int _maximumMeetingCount = 3;
        [SerializeField, Min(1f)] private float _meetingDiscussionSeconds = 90f;
        [SerializeField, Min(1f)] private float _meetingVoteSeconds = 30f;
        [SerializeField, Min(0f)] private float _meetingResultSeconds = 5f;
        [SerializeField, Min(0f)] private float _postMeetingBiteProtectionSeconds = 2f;

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
        public float FirstMeetingLockSeconds => _firstMeetingLockSeconds;
        public float MeetingCooldownSeconds => _meetingCooldownSeconds;
        public int MaximumMeetingCount => _maximumMeetingCount;
        public float MeetingDiscussionSeconds => _meetingDiscussionSeconds;
        public float MeetingVoteSeconds => _meetingVoteSeconds;
        public float MeetingResultSeconds => _meetingResultSeconds;
        public float PostMeetingBiteProtectionSeconds =>
            _postMeetingBiteProtectionSeconds;
    }
}
