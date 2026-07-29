using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 라운드 밸런스 수치의 원본 설정.
    /// 필드 이름은 docs/balance-and-telemetry.md의 키와 1:1로 맞춘다.
    /// 런타임 상태를 저장하지 않는다 (docs/project-structure.md §8).
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_GameBalance_Default",
        menuName = "MonkeyLab/Balance/Game Balance",
        order = 0)]
    public sealed class SO_GameBalance : ScriptableObject
    {
        [Header("라운드와 회의 (balance §2)")]
        [SerializeField] private float _explorationSeconds = 900f;
        [SerializeField] private float _startProtectionSeconds = 30f;
        [SerializeField] private float _roleRevealSeconds = 5f;
        [SerializeField] private float _firstMeetingLockSeconds = 120f;
        [SerializeField] private float _meetingSharedCooldownSeconds = 120f;
        [SerializeField] private int _maxMeetingCount = 3;
        [SerializeField] private float _discussionSeconds = 90f;
        [SerializeField] private float _voteSeconds = 30f;
        [SerializeField] private float _voteResultDisplaySeconds = 5f;
        [SerializeField] private float _postMeetingBiteProtectionSeconds = 2f;

        [Header("플레이어 이동 (balance §3)")]
        [SerializeField] private float _playerMoveSpeed = 4.0f;
        [SerializeField] private float _batteryCarrySpeed = 3.0f;
        [SerializeField] private float _ghostMoveSpeed = 4.8f;
        [SerializeField] private float _playerTurnSpeedDegrees = 720f;
        [SerializeField] private float _interactionRange = 1.5f;
        [SerializeField] private float _itemPickupRange = 1.2f;

        [Header("괴물 (balance §4)")]
        [SerializeField] private float _monsterPatrolSpeed = 2.6f;
        [SerializeField] private float _monsterChaseSpeed = 4.6f;
        [SerializeField] private float _monsterInvestigateSpeed = 6.0f;
        [SerializeField] private float _noiseSprintMaxSeconds = 6f;
        [SerializeField] private float _roomDwellSeconds = 6f;
        [SerializeField] private float _roomDwellVarianceSeconds = 1f;
        [SerializeField] private float _searchSeconds = 3f;
        [SerializeField] private float _sightRange = 7f;
        [SerializeField] private float _sightAngleDegrees = 100f;
        [SerializeField] private float _biteRange = 0.9f;
        [SerializeField] private float _biteWindupSeconds = 0.35f;
        [SerializeField] private float _biteRecoverySeconds = 1.2f;
        [SerializeField] private float _victimBiteProtectionSeconds = 1.5f;
        [SerializeField] private float _aiTickRateHz = 8f;
        [SerializeField] private float _extraMonsterWarningSeconds = 3f;

        [Header("강화 3축 (balance §4.1 / GDD §12.3)")]
        [SerializeField] private float[] _smellRadiusByLevel = { 0.5f, 1f, 2f };
        [SerializeField] private int[] _monsterCountByLevel = { 4, 6, 8 };
        [SerializeField] private float[] _infectionSecondsByLevel = { 90f, 60f, 30f };

        [Header("소음 반경 (balance §5)")]
        [SerializeField] private float _noiseRadiusSmall = 8f;
        [SerializeField] private float _noiseRadiusMedium = 14f;
        [SerializeField] private float _noiseRadiusLarge = 24f;

        [Header("빌런 (balance §6)")]
        [SerializeField] private float _speakerCooldownSeconds = 45f;
        [SerializeField] private float _speakerNoiseRadius = 24f;
        [SerializeField] private float _speakerPlaybackSeconds = 3f;
        [SerializeField] private float _upgradeMissionMinSeconds = 12f;
        [SerializeField] private float _upgradeMissionMaxSeconds = 18f;
        [SerializeField] private int _maxUpgradeLevelPerAxis = 2;

        [Header("미션과 진행률 (balance §7.1)")]
        [SerializeField] private int _projectTotalPoints = 10000;
        [SerializeField] private int _pointsPerSurvivor = 2000;

        [Header("해독제 (balance §8)")]
        [SerializeField] private int _antidoteMachineCount = 2;
        [SerializeField] private float _antidoteCraftSeconds = 180f;
        [SerializeField] private int _antidoteCarryLimit = 1;
        [SerializeField] private float _antidoteUseSeconds = 1.5f;

        public float ExplorationSeconds => _explorationSeconds;
        public float StartProtectionSeconds => _startProtectionSeconds;
        public float RoleRevealSeconds => _roleRevealSeconds;
        public float FirstMeetingLockSeconds => _firstMeetingLockSeconds;
        public float MeetingSharedCooldownSeconds => _meetingSharedCooldownSeconds;
        public int MaxMeetingCount => _maxMeetingCount;
        public float DiscussionSeconds => _discussionSeconds;
        public float VoteSeconds => _voteSeconds;
        public float VoteResultDisplaySeconds => _voteResultDisplaySeconds;
        public float PostMeetingBiteProtectionSeconds => _postMeetingBiteProtectionSeconds;

        public float PlayerMoveSpeed => _playerMoveSpeed;
        public float BatteryCarrySpeed => _batteryCarrySpeed;
        public float GhostMoveSpeed => _ghostMoveSpeed;
        public float PlayerTurnSpeedDegrees => _playerTurnSpeedDegrees;
        public float InteractionRange => _interactionRange;
        public float ItemPickupRange => _itemPickupRange;

        public float MonsterPatrolSpeed => _monsterPatrolSpeed;
        public float MonsterChaseSpeed => _monsterChaseSpeed;
        public float MonsterInvestigateSpeed => _monsterInvestigateSpeed;
        public float NoiseSprintMaxSeconds => _noiseSprintMaxSeconds;
        public float RoomDwellSeconds => _roomDwellSeconds;
        public float RoomDwellVarianceSeconds => _roomDwellVarianceSeconds;
        public float SearchSeconds => _searchSeconds;
        public float SightRange => _sightRange;
        public float SightAngleDegrees => _sightAngleDegrees;
        public float BiteRange => _biteRange;
        public float BiteWindupSeconds => _biteWindupSeconds;
        public float BiteRecoverySeconds => _biteRecoverySeconds;
        public float VictimBiteProtectionSeconds => _victimBiteProtectionSeconds;
        public float AiTickRateHz => _aiTickRateHz;
        public float ExtraMonsterWarningSeconds => _extraMonsterWarningSeconds;

        public float NoiseRadiusSmall => _noiseRadiusSmall;
        public float NoiseRadiusMedium => _noiseRadiusMedium;
        public float NoiseRadiusLarge => _noiseRadiusLarge;

        public float SpeakerCooldownSeconds => _speakerCooldownSeconds;
        public float SpeakerNoiseRadius => _speakerNoiseRadius;
        public float SpeakerPlaybackSeconds => _speakerPlaybackSeconds;
        public float UpgradeMissionMinSeconds => _upgradeMissionMinSeconds;
        public float UpgradeMissionMaxSeconds => _upgradeMissionMaxSeconds;
        public int MaxUpgradeLevelPerAxis => _maxUpgradeLevelPerAxis;

        public int ProjectTotalPoints => _projectTotalPoints;
        public int PointsPerSurvivor => _pointsPerSurvivor;

        public int AntidoteMachineCount => _antidoteMachineCount;
        public float AntidoteCraftSeconds => _antidoteCraftSeconds;
        public int AntidoteCarryLimit => _antidoteCarryLimit;
        public float AntidoteUseSeconds => _antidoteUseSeconds;

        /// <summary>강화 단계(0=기본, 1=1회, 2=2회)에 해당하는 후각 반경을 반환한다.</summary>
        public float GetSmellRadius(int level) => ReadLevel(_smellRadiusByLevel, level);

        /// <summary>강화 단계에 해당하는 괴물 수를 반환한다.</summary>
        public int GetMonsterCount(int level) => ReadLevel(_monsterCountByLevel, level);

        /// <summary>강화 단계에 해당하는 신규 감염 제한시간(초)을 반환한다.</summary>
        public float GetInfectionSeconds(int level) => ReadLevel(_infectionSecondsByLevel, level);

        private static T ReadLevel<T>(T[] table, int level)
        {
            if (table == null || table.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"{nameof(SO_GameBalance)}: 강화 단계 표가 비어 있다.");
            }

            return table[Mathf.Clamp(level, 0, table.Length - 1)];
        }

        private void OnValidate()
        {
            ValidateLevelTable(_smellRadiusByLevel, nameof(_smellRadiusByLevel));
            ValidateLevelTable(_monsterCountByLevel, nameof(_monsterCountByLevel));
            ValidateLevelTable(_infectionSecondsByLevel, nameof(_infectionSecondsByLevel));
        }

        private void ValidateLevelTable<T>(T[] table, string fieldName)
        {
            int expected = _maxUpgradeLevelPerAxis + 1;
            if (table != null && table.Length != expected)
            {
                Debug.LogWarning(
                    $"[Balance] {fieldName}의 길이는 {expected}여야 한다 (기본 + 강화 {_maxUpgradeLevelPerAxis}회). " +
                    $"현재 {table.Length}. docs/balance-and-telemetry.md §4.1 참조.",
                    this);
            }
        }
    }
}
