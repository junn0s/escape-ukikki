using MonkeyLab.Core;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// SO_GameBalance의 기본값이 docs/balance-and-telemetry.md 표와 일치하는지 검증한다.
    /// 문서를 고치면 이 테스트가 먼저 깨져야 한다 (문서 → 데이터 → 테스트 → 코드).
    /// </summary>
    public sealed class GameBalanceDefaultsTests
    {
        private SO_GameBalance _balance;

        [SetUp]
        public void SetUp()
        {
            _balance = ScriptableObject.CreateInstance<SO_GameBalance>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_balance);
        }

        [Test]
        public void RoundTimers_MatchBalanceDocument()
        {
            Assert.AreEqual(900f, _balance.ExplorationSeconds, "탐색 시간 900초 (balance §2)");
            Assert.AreEqual(30f, _balance.StartProtectionSeconds, "시작 보호 30초");
            Assert.AreEqual(120f, _balance.FirstMeetingLockSeconds, "첫 회의 잠금 120초");
            Assert.AreEqual(3, _balance.MaxMeetingCount, "최대 회의 3회");
            Assert.AreEqual(90f, _balance.DiscussionSeconds, "토론 90초");
            Assert.AreEqual(30f, _balance.VoteSeconds, "투표 30초");
        }

        [Test]
        public void MonsterInvestigateSpeed_IsOnePointFiveTimesPlayerSpeed()
        {
            // GDD §11.2: 반응한 괴물은 플레이어 기본 속도의 1.5배로 이동한다.
            Assert.AreEqual(
                _balance.PlayerMoveSpeed * 1.5f,
                _balance.MonsterInvestigateSpeed,
                0.001f,
                "소리 조사 속도는 플레이어 속도의 1.5배여야 한다");
        }

        [Test]
        public void UpgradeAxes_MatchThreeStageTable()
        {
            // GDD §12.3 강화 3단계 표
            Assert.AreEqual(0.5f, _balance.GetSmellRadius(0), "후각 기본 0.5m");
            Assert.AreEqual(1f, _balance.GetSmellRadius(1), "후각 1회 1m");
            Assert.AreEqual(2f, _balance.GetSmellRadius(2), "후각 2회 2m");

            Assert.AreEqual(4, _balance.GetMonsterCount(0), "괴물 기본 4마리");
            Assert.AreEqual(6, _balance.GetMonsterCount(1), "괴물 1회 6마리");
            Assert.AreEqual(8, _balance.GetMonsterCount(2), "괴물 2회 8마리");

            Assert.AreEqual(90f, _balance.GetInfectionSeconds(0), "감염 기본 90초");
            Assert.AreEqual(60f, _balance.GetInfectionSeconds(1), "감염 1회 60초");
            Assert.AreEqual(30f, _balance.GetInfectionSeconds(2), "감염 2회 30초");
        }

        [Test]
        public void GetUpgradeValue_ClampsOutOfRangeLevel()
        {
            // 범위를 벗어난 단계 요청은 예외 대신 양 끝값으로 고정한다.
            Assert.AreEqual(_balance.GetMonsterCount(0), _balance.GetMonsterCount(-1), "음수는 기본값");
            Assert.AreEqual(_balance.GetMonsterCount(2), _balance.GetMonsterCount(99), "초과는 최대값");
        }

        [Test]
        public void ProjectPoints_SplitEvenlyAcrossFiveSurvivors()
        {
            // GDD §9.1: 각 생존자가 전체의 20%를 담당한다.
            const int survivorCount = 5;
            Assert.AreEqual(
                _balance.ProjectTotalPoints,
                _balance.PointsPerSurvivor * survivorCount,
                "생존자 5명의 총점 합이 프로젝트 총점과 같아야 한다");
        }

        [Test]
        public void SpeakerCooldown_Is45Seconds()
        {
            // GDD §13.1 / balance §6
            Assert.AreEqual(45f, _balance.SpeakerCooldownSeconds);
        }

        [Test]
        public void AntidoteCraft_IsLongerThanShortestInfectionTimer()
        {
            // GDD §14.4: 최종 30초 단계에서는 물린 뒤 새로 제작할 수 없어야 한다.
            Assert.Greater(
                _balance.AntidoteCraftSeconds,
                _balance.GetInfectionSeconds(2),
                "제작 시간이 최종 감염 제한시간보다 길어야 사전 제작 압박이 성립한다");
        }
    }
}
