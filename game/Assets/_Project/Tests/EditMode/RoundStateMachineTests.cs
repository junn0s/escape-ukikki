using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class RoundStateMachineTests
    {
        [Test]
        public void RoundAdvancesFromRoleRevealToGraceAndExploration()
        {
            var config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            try
            {
                var machine = new RoundStateMachine(config);
                var snapshot = CreateSafeSnapshot(config, 5);

                Assert.That(machine.Phase, Is.EqualTo(RoundPhase.RoleReveal));
                Assert.That(
                    machine.RemainingPhaseSeconds,
                    Is.EqualTo(7f));

                machine.Tick(7f, snapshot);
                Assert.That(machine.Phase, Is.EqualTo(RoundPhase.GracePeriod));
                Assert.That(
                    machine.RemainingPhaseSeconds,
                    Is.EqualTo(30f));

                // 라운드 시계는 보호 시간부터 흐른다(GDD §6.3). 보호 중에도
                // 미션을 수행하므로 시간이 멈추지 않는다.
                Assert.That(
                    machine.RemainingRoundSeconds,
                    Is.EqualTo(900f));

                machine.Tick(10f, snapshot);
                Assert.That(machine.Phase, Is.EqualTo(RoundPhase.GracePeriod));
                Assert.That(
                    machine.RemainingRoundSeconds,
                    Is.EqualTo(890f),
                    "보호 시간에도 라운드 시계가 줄어야 한다.");

                machine.Tick(20f, snapshot);
                Assert.That(machine.Phase, Is.EqualTo(RoundPhase.Exploration));
                Assert.That(
                    machine.RemainingRoundSeconds,
                    Is.EqualTo(870f),
                    "탐색 진입 시 보호 중 흘린 30초를 되채우면 안 된다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ExplorationTimeoutEndsRoundWithVillainVictory()
        {
            var config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            try
            {
                var machine = new RoundStateMachine(config);
                machine.SkipToExplorationForDevelopment();
                machine.SetRemainingRoundSecondsForDevelopment(1f);

                machine.Tick(1f, CreateSafeSnapshot(config, 5));

                Assert.That(machine.HasEnded, Is.True);
                Assert.That(machine.Outcome, Is.EqualTo(RoundOutcome.VillainWins));
                Assert.That(
                    machine.EndReason,
                    Is.EqualTo(RoundEndReason.TimeExpired));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void WinResolutionUsesDocumentedSameTickPrecedence()
        {
            var snapshot = new RoundWinSnapshot(
                isVillainExiled: true,
                projectPoints: 10000,
                projectMaximumPoints: 10000,
                realSurvivorCount: 0,
                remainingRoundSeconds: 0f);

            Assert.That(
                RoundWinConditionService.TryResolve(
                    snapshot,
                    out var outcome,
                    out var reason),
                Is.True);
            Assert.That(outcome, Is.EqualTo(RoundOutcome.SurvivorsWin));
            Assert.That(reason, Is.EqualTo(RoundEndReason.VillainExiled));

            snapshot = new RoundWinSnapshot(
                isVillainExiled: false,
                projectPoints: 10000,
                projectMaximumPoints: 10000,
                realSurvivorCount: 0,
                remainingRoundSeconds: 0f);
            RoundWinConditionService.TryResolve(
                snapshot,
                out outcome,
                out reason);

            Assert.That(outcome, Is.EqualTo(RoundOutcome.SurvivorsWin));
            Assert.That(reason, Is.EqualTo(RoundEndReason.ProjectCompleted));
        }

        [Test]
        public void SurvivorFiveMissionsContributeExactlyTwentyPercent()
        {
            var progress = new ProjectProgressService(10000, 2000);
            var totalAward = 0;

            for (ulong missionId = 1; missionId <= 5; missionId++)
            {
                Assert.That(
                    progress.TryCompleteMission(
                        playerId: 7,
                        missionId,
                        assignedMissionCount: 5,
                        out var awardedPoints),
                    Is.True);
                totalAward += awardedPoints;
            }

            Assert.That(totalAward, Is.EqualTo(2000));
            Assert.That(progress.Points, Is.EqualTo(2000));
            Assert.That(
                progress.TryCompleteMission(7, 1, 5, out var duplicateAward),
                Is.False);
            Assert.That(duplicateAward, Is.Zero);
            Assert.That(progress.Points, Is.EqualTo(2000));
        }

        [Test]
        public void FiveSurvivorsCanCompleteTheWholeProject()
        {
            var progress = new ProjectProgressService(10000, 2000);

            for (ulong playerId = 1; playerId <= 5; playerId++)
            {
                for (ulong stationId = 1; stationId <= 5; stationId++)
                {
                    Assert.That(
                        progress.TryCompleteMission(
                            playerId,
                            stationId,
                            assignedMissionCount: 5,
                            out _),
                        Is.True);
                }
            }

            Assert.That(progress.Points, Is.EqualTo(10000));
            Assert.That(
                progress.Milestone,
                Is.EqualTo(ProjectMilestone.Completed));
        }

        [TestCase(2499, ProjectMilestone.None)]
        [TestCase(2500, ProjectMilestone.FacilityGuidance)]
        [TestCase(5000, ProjectMilestone.SecurityAccess)]
        [TestCase(7500, ProjectMilestone.ExitGuidance)]
        [TestCase(10000, ProjectMilestone.Completed)]
        public void ProjectMilestonesUseQuarterThresholds(
            int points,
            ProjectMilestone expected)
        {
            Assert.That(
                ProjectProgressService.ResolveMilestone(points, 10000),
                Is.EqualTo(expected));
        }

        [Test]
        public void MissionAssignmentStartsWithNearestStation()
        {
            var candidates = new[]
            {
                new MissionAssignmentCandidate(
                    missionId: 30,
                    position: new Vector2(8f, 0f)),
                new MissionAssignmentCandidate(
                    missionId: 20,
                    position: new Vector2(2f, 0f)),
                new MissionAssignmentCandidate(
                    missionId: 10,
                    position: new Vector2(5f, 0f))
            };

            var ordered = MissionAssignmentOrderService.OrderByDistance(
                Vector2.zero,
                candidates);

            Assert.That(ordered, Is.EqualTo(new ulong[] { 20, 10, 30 }));
        }

        [Test]
        public void MissionAssignmentUsesMissionIdAsStableTieBreaker()
        {
            var candidates = new[]
            {
                new MissionAssignmentCandidate(
                    missionId: 9,
                    position: new Vector2(-2f, 0f)),
                new MissionAssignmentCandidate(
                    missionId: 4,
                    position: new Vector2(2f, 0f))
            };

            var ordered = MissionAssignmentOrderService.OrderByDistance(
                Vector2.zero,
                candidates);

            Assert.That(ordered, Is.EqualTo(new ulong[] { 4, 9 }));
        }

        [Test]
        public void MissionAssignmentSelectsFiveUniqueStationsAcrossMap()
        {
            var candidates = new MissionAssignmentCandidate[10];
            for (var index = 0; index < candidates.Length; index++)
            {
                candidates[index] = new MissionAssignmentCandidate(
                    (ulong)(index + 1),
                    new Vector2(index * 2f, 0f));
            }

            var selected =
                MissionAssignmentOrderService.SelectSpreadAssignments(
                    Vector2.zero,
                    candidates,
                    assignedCount: 5);

            Assert.That(selected, Has.Length.EqualTo(5));
            Assert.That(selected, Is.Unique);
            Assert.That(selected[0], Is.EqualTo(1UL));
            Assert.That(selected[^1], Is.EqualTo(10UL));
        }

        private static RoundWinSnapshot CreateSafeSnapshot(
            RoundBalanceConfig config,
            int survivorCount)
        {
            return new RoundWinSnapshot(
                isVillainExiled: false,
                projectPoints: 0,
                projectMaximumPoints: config.ProjectMaximumPoints,
                realSurvivorCount: survivorCount,
                remainingRoundSeconds: config.ExplorationDurationSeconds);
        }
    }
}
