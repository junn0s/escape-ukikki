using MonkeyLab.Gameplay.Application;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 빌런 위장 미션이 프로젝트 진행률에 반영되지 않는다는 규칙을 고정한다.
    /// GDD §9.1: 빌런의 가짜 미션은 진행률을 올리지 않는다.
    /// </summary>
    public sealed class FakeMissionProgressTests
    {
        private const int MaximumPoints = 10000;
        private const int PersonalBudgetPoints = 2000;
        private const int AssignedMissionCount = 5;
        private const int SurvivorCount = 5;

        private static ProjectProgressService CreateService()
        {
            return new ProjectProgressService(
                MaximumPoints,
                PersonalBudgetPoints);
        }

        [Test]
        public void RebindPlayer_PreservesPersonalBudgetAndCompletionHistory()
        {
            var progress = CreateService();
            for (ulong missionId = 10; missionId < 14; missionId++)
            {
                Assert.That(
                    progress.TryCompleteMission(1, missionId, 5, out _),
                    Is.True);
            }

            Assert.That(progress.RebindPlayer(1, 20), Is.True);
            Assert.That(
                progress.TryCompleteMission(20, 10, 5, out _),
                Is.False);
            Assert.That(
                progress.TryCompleteMission(20, 14, 5, out var finalAward),
                Is.True);
            Assert.That(finalAward, Is.EqualTo(400));
            Assert.That(
                progress.TryCompleteMission(20, 15, 5, out var cappedAward),
                Is.False);
            Assert.That(cappedAward, Is.Zero);
            Assert.That(progress.Points, Is.EqualTo(PersonalBudgetPoints));
        }

        [Test]
        public void FiveSurvivorsAlone_ReachExactlyOneHundredPercent()
        {
            // 빌런이 미션을 하나도 하지 않아도 생존자 5명만으로 100%에 도달해야 한다.
            var service = CreateService();
            for (ulong playerId = 0; playerId < SurvivorCount; playerId++)
            {
                for (ulong missionId = 0;
                     missionId < AssignedMissionCount;
                     missionId++)
                {
                    service.TryCompleteMission(
                        playerId,
                        missionId,
                        AssignedMissionCount,
                        out _);
                }
            }

            Assert.That(service.Points, Is.EqualTo(MaximumPoints));
            Assert.That(
                service.NormalizedProgress,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                service.Milestone,
                Is.EqualTo(ProjectMilestone.Completed));
        }

        [Test]
        public void PersonalBudget_CapsAtTwentyPercentPerSurvivor()
        {
            var service = CreateService();
            // 한 생존자가 배정보다 많은 미션을 완료해도 20%를 넘지 못한다.
            for (ulong missionId = 0; missionId < 20; missionId++)
            {
                service.TryCompleteMission(
                    0,
                    missionId,
                    AssignedMissionCount,
                    out _);
            }

            Assert.That(service.Points, Is.EqualTo(PersonalBudgetPoints));
        }

        [Test]
        public void SameMissionTwice_AwardsPointsOnlyOnce()
        {
            var service = CreateService();
            service.TryCompleteMission(
                0,
                1,
                AssignedMissionCount,
                out var firstAward);

            Assert.That(
                service.TryCompleteMission(
                    0,
                    1,
                    AssignedMissionCount,
                    out var secondAward),
                Is.False);
            Assert.That(secondAward, Is.Zero);
            Assert.That(service.Points, Is.EqualTo(firstAward));
        }

        [Test]
        public void SurvivorProgress_IsUnaffectedByExtraPlayers()
        {
            // 빌런이 위장 미션을 해도 ProjectProgressService는 호출되지 않는다.
            // 즉 생존자만 완료한 경우와 진행률이 같아야 한다.
            var survivorsOnly = CreateService();
            for (ulong playerId = 0; playerId < SurvivorCount; playerId++)
            {
                survivorsOnly.TryCompleteMission(
                    playerId,
                    0,
                    AssignedMissionCount,
                    out _);
            }

            var expectedPoints = survivorsOnly.Points;

            var withVillainAttempts = CreateService();
            for (ulong playerId = 0; playerId < SurvivorCount; playerId++)
            {
                withVillainAttempts.TryCompleteMission(
                    playerId,
                    0,
                    AssignedMissionCount,
                    out _);
            }

            Assert.That(
                withVillainAttempts.Points,
                Is.EqualTo(expectedPoints));
            Assert.That(
                withVillainAttempts.Points,
                Is.LessThan(MaximumPoints));
        }
    }
}
