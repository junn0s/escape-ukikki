using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class InfectionServiceTests
    {
        [Test]
        public void BiteStartsTimerFromToxicityTierAtBite()
        {
            using var context = TestContext.Create();
            context.TierRuntime.SetToxicityTier(1);

            Assert.That(context.Target.TryReceiveBite(null, 10f, 0f), Is.True);

            Assert.That(context.Infection.State, Is.EqualTo(PlayerLifeState.AliveInfected));
            Assert.That(context.Infection.ToxicityTierAtBite, Is.EqualTo(1));
            Assert.That(context.Infection.DurationAtBiteSeconds, Is.EqualTo(60f));
            Assert.That(context.Infection.RemainingSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void VillainRejectsBiteWithoutPresentationOrInfection()
        {
            using var context = TestContext.Create();
            context.Target.SetCanBeInfected(false);
            context.Target.SetCanBeBitten(false);
            var wasPresented = false;
            context.Target.BitePresented += (_, _) => wasPresented = true;

            Assert.That(
                context.Target.TryReceiveBite(null, 10f, 1.5f),
                Is.False);
            Assert.That(wasPresented, Is.False);
            Assert.That(context.Target.BiteCount, Is.Zero);
            Assert.That(
                context.Infection.State,
                Is.EqualTo(PlayerLifeState.AliveHealthy));
            Assert.That(context.Target.IsDetectable, Is.True);
            Assert.That(context.Target.CanBeBitten, Is.False);
        }

        [Test]
        public void ReplicatedBitePresentationDoesNotRepeatServerGameplay()
        {
            using var context = TestContext.Create();
            var presentationCount = 0;
            context.Target.BitePresented += (_, _) => presentationCount++;

            context.Target.PresentReplicatedBite();

            Assert.That(presentationCount, Is.EqualTo(1));
            Assert.That(context.Target.BiteCount, Is.Zero);
            Assert.That(
                context.Infection.State,
                Is.EqualTo(PlayerLifeState.AliveHealthy));
        }

        [Test]
        public void InfectedPlayerRejectsBitesUntilCured()
        {
            using var context = TestContext.Create();
            context.Target.TryReceiveBite(null, 10f, 0f);
            context.Infection.Tick(10f);

            context.TierRuntime.SetToxicityTier(2);
            var repeatedBite = context.Target.TryReceiveBite(null, 12f, 0f);

            Assert.That(repeatedBite, Is.False);
            Assert.That(context.Target.BiteCount, Is.EqualTo(1));
            Assert.That(context.Target.IsDetectable, Is.False);
            Assert.That(context.Infection.ToxicityTierAtBite, Is.Zero);
            Assert.That(context.Infection.DurationAtBiteSeconds, Is.EqualTo(90f));
            Assert.That(context.Infection.RemainingSeconds, Is.EqualTo(80f));

            Assert.That(context.Infection.TryCure(), Is.True);
            Assert.That(context.Target.IsDetectable, Is.True);
            Assert.That(
                context.Target.TryReceiveBite(null, 13f, 0f),
                Is.True);
            Assert.That(context.Target.BiteCount, Is.EqualTo(2));
            Assert.That(context.Infection.DurationAtBiteSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void PausedInfectionDoesNotAdvanceTimer()
        {
            using var context = TestContext.Create();
            context.Target.TryReceiveBite(null, 10f, 0f);

            context.Infection.SetPaused(true);
            context.Infection.Tick(20f);
            Assert.That(context.Infection.RemainingSeconds, Is.EqualTo(90f));

            context.Infection.SetPaused(false);
            context.Infection.Tick(1f);
            Assert.That(context.Infection.RemainingSeconds, Is.EqualTo(89f));
        }

        [Test]
        public void InfectionExpiryTransitionsToDeadGhostAndStopsMonsterDetection()
        {
            using var context = TestContext.Create();
            context.TierRuntime.SetToxicityTier(2);
            context.Target.TryReceiveBite(null, 10f, 0f);

            context.Infection.Tick(30f);

            Assert.That(context.Infection.State, Is.EqualTo(PlayerLifeState.DeadGhost));
            Assert.That(context.Target.IsDetectable, Is.False);
        }

        [Test]
        public void CompletedAntidoteUseConsumesItemAndCuresInfection()
        {
            using var context = TestContext.Create();
            context.Target.TryReceiveBite(null, 10f, 0f);
            Assert.That(context.Antidote.TryAddAntidote(), Is.True);
            Assert.That(context.Antidote.TryBeginUse(100f), Is.True);

            context.Antidote.TickUse(101.49f, Vector2.zero);
            Assert.That(context.Infection.IsInfected, Is.True);
            Assert.That(context.Antidote.CarriedCount, Is.EqualTo(1));

            context.Antidote.TickUse(101.5f, Vector2.zero);
            Assert.That(context.Infection.State, Is.EqualTo(PlayerLifeState.AliveHealthy));
            Assert.That(context.Target.IsDetectable, Is.True);
            Assert.That(context.Antidote.CarriedCount, Is.Zero);
            Assert.That(context.Antidote.IsUsing, Is.False);
        }

        [Test]
        public void MovementCancelsAntidoteUseWithoutConsumingItem()
        {
            using var context = TestContext.Create();
            context.Target.TryReceiveBite(null, 10f, 0f);
            context.Antidote.TryAddAntidote();
            context.Antidote.TryBeginUse(100f);

            context.Antidote.TickUse(100.5f, Vector2.right);

            Assert.That(context.Infection.IsInfected, Is.True);
            Assert.That(context.Antidote.CarriedCount, Is.EqualTo(1));
            Assert.That(context.Antidote.IsUsing, Is.False);
        }

        [Test]
        public void AntidoteInventoryRespectsSingleItemCarryLimit()
        {
            using var context = TestContext.Create();

            Assert.That(context.Antidote.TryAddAntidote(), Is.True);
            Assert.That(context.Antidote.TryAddAntidote(), Is.False);
            Assert.That(context.Antidote.CarriedCount, Is.EqualTo(1));
        }

        [Test]
        public void LocalInventoryCanRemoveAntidoteForStorage()
        {
            using var context = TestContext.Create();
            Assert.That(context.Antidote.TryAddAntidote(), Is.True);

            Assert.That(context.Antidote.TryRemoveAntidote(), Is.True);
            Assert.That(context.Antidote.CarriedCount, Is.Zero);
            Assert.That(context.Antidote.TryRemoveAntidote(), Is.False);
        }

        [Test]
        public void HealthyPlayerCannotConsumeAntidote()
        {
            using var context = TestContext.Create();
            context.Antidote.TryAddAntidote();

            Assert.That(context.Antidote.TryBeginUse(100f), Is.False);
            Assert.That(context.Antidote.CarriedCount, Is.EqualTo(1));
        }

        private sealed class TestContext : IDisposable
        {
            private TestContext(
                GameObject root,
                MonsterTierConfig tierConfig,
                AntidoteBalanceConfig antidoteConfig,
                MonsterTarget target,
                MonsterTierRuntime tierRuntime,
                InfectionService infection,
                AntidoteService antidote)
            {
                Root = root;
                TierConfig = tierConfig;
                AntidoteConfig = antidoteConfig;
                Target = target;
                TierRuntime = tierRuntime;
                Infection = infection;
                Antidote = antidote;
            }

            public GameObject Root { get; }
            public MonsterTierConfig TierConfig { get; }
            public AntidoteBalanceConfig AntidoteConfig { get; }
            public MonsterTarget Target { get; }
            public MonsterTierRuntime TierRuntime { get; }
            public InfectionService Infection { get; }
            public AntidoteService Antidote { get; }

            public static TestContext Create()
            {
                var root = new GameObject("InfectionTestRoot");
                root.SetActive(false);
                var target = root.AddComponent<MonsterTarget>();
                target.Configure(true, true);

                var tierConfig = ScriptableObject.CreateInstance<MonsterTierConfig>();
                var tierRuntime = root.AddComponent<MonsterTierRuntime>();
                tierRuntime.Configure(tierConfig);

                var infection = root.AddComponent<InfectionService>();
                infection.Configure(target, tierRuntime);

                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                var motor = root.AddComponent<PlayerMotor>();
                var antidoteConfig = ScriptableObject.CreateInstance<AntidoteBalanceConfig>();
                var antidote = root.AddComponent<AntidoteService>();
                antidote.Configure(antidoteConfig, infection, null, motor);

                return new TestContext(
                    root,
                    tierConfig,
                    antidoteConfig,
                    target,
                    tierRuntime,
                    infection,
                    antidote);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(TierConfig);
                UnityEngine.Object.DestroyImmediate(AntidoteConfig);
            }
        }
    }
}
