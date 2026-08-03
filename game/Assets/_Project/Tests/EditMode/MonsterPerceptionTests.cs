using System.Collections.Generic;
using System.Reflection;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class MonsterPerceptionTests
    {
        [Test]
        public void PatrolReservation_PreventsTwoMonstersChoosingSameRoom()
        {
            var firstMonster = new GameObject("FirstMonster");
            var secondMonster = new GameObject("SecondMonster");
            var patrolRoom = new Vector2(12f, -7f);
            try
            {
                Assert.That(
                    MonsterPatrolReservation.TryReserve(
                        patrolRoom,
                        firstMonster),
                    Is.True);
                Assert.That(
                    MonsterPatrolReservation.TryReserve(
                        patrolRoom,
                        secondMonster),
                    Is.False);

                MonsterPatrolReservation.Release(
                    patrolRoom,
                    firstMonster);
                Assert.That(
                    MonsterPatrolReservation.TryReserve(
                        patrolRoom,
                        secondMonster),
                    Is.True);
            }
            finally
            {
                MonsterPatrolReservation.ReleaseAll(firstMonster);
                MonsterPatrolReservation.ReleaseAll(secondMonster);
                Object.DestroyImmediate(firstMonster);
                Object.DestroyImmediate(secondMonster);
            }
        }

        [Test]
        public void CircularProximityDetectsTargetBehindMonster()
        {
            var monster = new GameObject("Monster");
            var targetObject = new GameObject("Target");
            var config =
                ScriptableObject.CreateInstance<MonsterBalanceConfig>();
            var tierConfig =
                ScriptableObject.CreateInstance<MonsterTierConfig>();
            try
            {
                var target = targetObject.AddComponent<MonsterTarget>();
                target.Configure(true, true);
                targetObject.transform.position = Vector2.down * 2.2f;
                var tierRuntime =
                    monster.AddComponent<MonsterTierRuntime>();
                tierRuntime.Configure(tierConfig);
                var senses = monster.AddComponent<MonsterSenses>();
                senses.Configure(
                    config,
                    tierRuntime,
                    target,
                    Physics2D.DefaultRaycastLayers);
                senses.SetFacingDirection(Vector2.up);

                Assert.That(
                    senses.TryDetectTarget(out var detectionType),
                    Is.True);
                Assert.That(
                    detectionType,
                    Is.EqualTo(MonsterDetectionType.Proximity));
            }
            finally
            {
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(tierConfig);
            }
        }

        [Test]
        public void NoiseAmbushUsesEightMeterEventRadius()
        {
            var monster = new GameObject("Monster");
            var targetObject = new GameObject("Target");
            var config =
                ScriptableObject.CreateInstance<MonsterBalanceConfig>();
            var tierConfig =
                ScriptableObject.CreateInstance<MonsterTierConfig>();
            try
            {
                var target = targetObject.AddComponent<MonsterTarget>();
                target.Configure(true, true);
                targetObject.transform.position = Vector2.right * 7.9f;
                var tierRuntime =
                    monster.AddComponent<MonsterTierRuntime>();
                tierRuntime.Configure(tierConfig);
                var senses = monster.AddComponent<MonsterSenses>();
                senses.Configure(
                    config,
                    tierRuntime,
                    target,
                    Physics2D.DefaultRaycastLayers);

                Assert.That(senses.TryDetectTarget(out _), Is.False);
                Assert.That(
                    senses.TryDetectTargetNearPosition(
                        Vector3.zero,
                        config.NoiseAmbushRadius,
                        out var detectionType),
                    Is.True);
                Assert.That(
                    detectionType,
                    Is.EqualTo(MonsterDetectionType.NoiseAmbush));

                targetObject.transform.position = Vector2.right * 8.1f;
                Assert.That(
                    senses.TryDetectTargetNearPosition(
                        Vector3.zero,
                        config.NoiseAmbushRadius,
                        out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(tierConfig);
            }
        }

        [Test]
        public void DetectionRadiusUsesXYDistance()
        {
            var result = MonsterPerceptionRules.IsWithinRadius(
                Vector3.zero,
                new Vector3(0.3f, 0.4f, 4f),
                0.5f);

            Assert.That(result, Is.True);
        }

        [Test]
        public void BiteReachUsesColliderGapInsteadOfCenterOverlap()
        {
            var monster = new GameObject("Monster");
            var player = new GameObject("Player");
            var config =
                ScriptableObject.CreateInstance<MonsterBalanceConfig>();
            var tierConfig =
                ScriptableObject.CreateInstance<MonsterTierConfig>();
            try
            {
                monster.AddComponent<BoxCollider2D>().size = Vector2.one;
                var playerCollider = player.AddComponent<BoxCollider2D>();
                playerCollider.size = Vector2.one;
                player.transform.position = new Vector2(1.8f, 0f);
                var target = player.AddComponent<MonsterTarget>();
                target.Configure(true, true);
                var tierRuntime =
                    monster.AddComponent<MonsterTierRuntime>();
                tierRuntime.Configure(tierConfig);
                var senses = monster.AddComponent<MonsterSenses>();
                senses.Configure(
                    config,
                    tierRuntime,
                    target,
                    Physics2D.DefaultRaycastLayers);
                var targetColliderCache = typeof(MonsterSenses).GetField(
                    "_targetCollider",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(targetColliderCache, Is.Not.Null);
                targetColliderCache.SetValue(senses, null);
                Physics2D.SyncTransforms();

                Assert.That(
                    Vector2.Distance(
                        monster.transform.position,
                        player.transform.position),
                    Is.GreaterThan(config.BiteDistance));
                Assert.That(
                    senses.IsTargetInBiteRange(),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(tierConfig);
            }
        }

        [Test]
        public void OtherMonstersDoNotBlockBitePathButWallsDo()
        {
            var monster = new GameObject("Monster");
            var otherMonster = new GameObject("OtherMonster");
            var player = new GameObject("Player");
            var wall = new GameObject("Wall");
            var config =
                ScriptableObject.CreateInstance<MonsterBalanceConfig>();
            var tierConfig =
                ScriptableObject.CreateInstance<MonsterTierConfig>();
            try
            {
                monster.AddComponent<BoxCollider2D>().size =
                    new Vector2(0.4f, 0.4f);
                otherMonster.AddComponent<BoxCollider2D>().size =
                    new Vector2(0.2f, 0.2f);
                otherMonster.transform.position = new Vector2(0.5f, 0f);
                player.AddComponent<BoxCollider2D>().size =
                    new Vector2(0.4f, 0.4f);
                player.transform.position = new Vector2(1f, 0f);
                var target = player.AddComponent<MonsterTarget>();
                target.Configure(true, true);
                var tierRuntime =
                    monster.AddComponent<MonsterTierRuntime>();
                tierRuntime.Configure(tierConfig);
                var senses = monster.AddComponent<MonsterSenses>();
                senses.Configure(
                    config,
                    tierRuntime,
                    target,
                    Physics2D.DefaultRaycastLayers);
                var otherSenses =
                    otherMonster.AddComponent<MonsterSenses>();
                otherSenses.Configure(
                    config,
                    tierRuntime,
                    null,
                    Physics2D.DefaultRaycastLayers);
                Physics2D.SyncTransforms();

                Assert.That(senses.HasClearPathToTarget(), Is.True);

                otherMonster.transform.position = new Vector2(0.5f, 2f);
                wall.AddComponent<BoxCollider2D>().size =
                    new Vector2(0.2f, 0.2f);
                wall.transform.position = new Vector2(0.5f, 0f);
                Physics2D.SyncTransforms();

                Assert.That(senses.HasClearPathToTarget(), Is.False);
                Assert.That(senses.LastPathBlocker.gameObject, Is.SameAs(wall));
            }
            finally
            {
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(otherMonster);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(tierConfig);
            }
        }

        [Test]
        public void SuccessfulBiteReleasesTargetButMissDoesNot()
        {
            Assert.That(
                MonsterAggroRules.ShouldReleaseTargetAfterBite(MonsterBiteResult.Hit),
                Is.True);
            Assert.That(
                MonsterAggroRules.ShouldReleaseTargetAfterBite(MonsterBiteResult.Miss),
                Is.False);
            Assert.That(
                MonsterAggroRules.ShouldReleaseTargetAfterBite(MonsterBiteResult.Protected),
                Is.False);
        }

        [Test]
        public void BiteProtectionRejectsRepeatUntilDurationEnds()
        {
            var gameObject = new GameObject("Target");
            try
            {
                var target = gameObject.AddComponent<MonsterTarget>();

                Assert.That(target.TryReceiveBite(null, 10f, 1.5f), Is.True);
                Assert.That(target.TryReceiveBite(null, 11.49f, 1.5f), Is.False);
                Assert.That(target.TryReceiveBite(null, 11.5f, 1.5f), Is.True);
                Assert.That(target.BiteCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GracePeriodBlocksAggressionUntilDevelopmentSkip()
        {
            var config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
            var gameObject = new GameObject("RoundPhase");
            gameObject.SetActive(false);
            try
            {
                var roundPhase = gameObject.AddComponent<LocalRoundPhasePrototype>();
                roundPhase.Configure(config);
                roundPhase.ResetForRound();

                Assert.That(roundPhase.IsMonsterAggressionEnabled, Is.False);
                Assert.That(roundPhase.RemainingGracePeriodSeconds, Is.GreaterThan(29f));

                roundPhase.SkipGracePeriodForDevelopment();

                Assert.That(roundPhase.IsMonsterAggressionEnabled, Is.True);
                Assert.That(roundPhase.RemainingGracePeriodSeconds, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void MonsterTierRuntimeUpdatesProximityRadiusImmediately()
        {
            var config = ScriptableObject.CreateInstance<MonsterTierConfig>();
            var gameObject = new GameObject("MonsterTierRuntime");
            gameObject.SetActive(false);
            try
            {
                var runtime = gameObject.AddComponent<MonsterTierRuntime>();
                runtime.Configure(config);

                Assert.That(
                    runtime.CurrentProximityDetectionRadius,
                    Is.EqualTo(5f));

                runtime.SetProximityDetectionTier(1);
                Assert.That(
                    runtime.CurrentProximityDetectionRadius,
                    Is.EqualTo(7f));

                runtime.SetProximityDetectionTier(2);
                Assert.That(
                    runtime.CurrentProximityDetectionRadius,
                    Is.EqualTo(9f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TopDownNavigationGraphBuildsConnectedPath()
        {
            var root = new GameObject("NavigationGraph");
            try
            {
                var nodes = new Transform[3];
                for (var index = 0; index < nodes.Length; index++)
                {
                    var node = new GameObject("Node_" + index);
                    node.transform.SetParent(root.transform);
                    node.transform.position = new Vector2(index * 2f, 0f);
                    nodes[index] = node.transform;
                }

                var graph = root.AddComponent<TopDownNavigationGraph>();
                graph.Configure(
                    nodes,
                    new[]
                    {
                        new TopDownNavigationGraph.Link(0, 1),
                        new TopDownNavigationGraph.Link(1, 2)
                    });
                var path = new List<Vector2>();

                Assert.That(
                    graph.TryBuildPath(
                        new Vector2(-0.5f, 0f),
                        new Vector2(4.5f, 0f),
                        path,
                        out var distance),
                    Is.True);
                Assert.That(path.Count, Is.GreaterThanOrEqualTo(3));
                Assert.That(distance, Is.EqualTo(5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
