using MonkeyLab.Gameplay.Villain;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class ClueRegistryTests
    {
        [Test]
        public void NewRegistry_HasNoActiveClues()
        {
            var registry = new ClueRegistry();

            Assert.That(registry.ActiveClueCount, Is.Zero);
            Assert.That(registry.IsActive(1), Is.False);
            Assert.That(
                registry.GetState(1),
                Is.EqualTo(ClueState.Inactive));
        }

        [Test]
        public void TryActivate_MakesClueVisibleAndUninspected()
        {
            var registry = new ClueRegistry();

            Assert.That(
                registry.TryActivate(1, ClueKind.VentRedSmoke),
                Is.True);
            Assert.That(registry.IsActive(1), Is.True);
            Assert.That(
                registry.GetState(1),
                Is.EqualTo(ClueState.ActiveUninspected));
        }

        [Test]
        public void TryActivate_IgnoresDuplicateActivation()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryMarkInspected(1, ClueKind.VentRedSmoke);

            // 이미 조사된 단서를 다시 활성화해도 상태가 되돌아가지 않아야 한다.
            Assert.That(
                registry.TryActivate(1, ClueKind.VentRedSmoke),
                Is.False);
            Assert.That(
                registry.GetState(1),
                Is.EqualTo(ClueState.ActiveInspected));
        }

        [Test]
        public void MarkInspected_KeepsClueActive()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.EmptySyringe);

            Assert.That(
                registry.TryMarkInspected(1, ClueKind.EmptySyringe),
                Is.True);
            // 핵심 규칙: 조사해도 단서는 사라지지 않는다.
            Assert.That(registry.IsActive(1), Is.True);
            Assert.That(registry.ActiveClueCount, Is.EqualTo(1));
        }

        [Test]
        public void MarkInspected_IgnoresInactiveClue()
        {
            var registry = new ClueRegistry();

            Assert.That(
                registry.TryMarkInspected(9, ClueKind.EmptySyringe),
                Is.False);
        }

        [Test]
        public void MarkInspected_IsIdempotent()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.SpeakerRedLed);
            registry.TryMarkInspected(1, ClueKind.SpeakerRedLed);

            Assert.That(
                registry.TryMarkInspected(1, ClueKind.SpeakerRedLed),
                Is.False);
            Assert.That(registry.IsActive(1), Is.True);
        }

        [Test]
        public void SameAxisTwice_LeavesTwoSeparateClues()
        {
            var registry = new ClueRegistry();

            // 같은 축을 2회 강화하면 두 번째 위치에 별도 단서가 생긴다(SDD §14.2).
            Assert.That(
                registry.TryActivate(1, ClueKind.VentRedSmoke),
                Is.True);
            Assert.That(
                registry.TryActivate(2, ClueKind.VentRedSmoke),
                Is.True);
            Assert.That(registry.ActiveClueCount, Is.EqualTo(2));
        }

        [Test]
        public void ActiveCluesNeverRevertToInactive()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryActivate(2, ClueKind.BrokenQuarantineLock);
            registry.TryMarkInspected(2, ClueKind.BrokenQuarantineLock);

            // 라운드 중에는 어떤 경로로도 Inactive로 돌아가지 않는다.
            Assert.That(registry.IsActive(1), Is.True);
            Assert.That(registry.IsActive(2), Is.True);
            Assert.That(
                registry.CountByState(ClueState.Inactive),
                Is.Zero);
        }

        [Test]
        public void CountByState_SeparatesInspectedFromUninspected()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryActivate(2, ClueKind.EmptySyringe);
            registry.TryActivate(3, ClueKind.SpeakerRedLed);
            registry.TryMarkInspected(2, ClueKind.EmptySyringe);

            Assert.That(
                registry.CountByState(ClueState.ActiveUninspected),
                Is.EqualTo(2));
            Assert.That(
                registry.CountByState(ClueState.ActiveInspected),
                Is.EqualTo(1));
        }

        [Test]
        public void ResetForNewRound_ClearsEveryClue()
        {
            var registry = new ClueRegistry();
            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryActivate(2, ClueKind.EmptySyringe);

            registry.ResetForNewRound();

            Assert.That(registry.ActiveClueCount, Is.Zero);
            Assert.That(registry.IsActive(1), Is.False);
        }

        [Test]
        public void ClueStateChanged_RaisedOnActivationAndInspection()
        {
            var registry = new ClueRegistry();
            var events = 0;
            registry.ClueStateChanged += (id, kind, state) => events++;

            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryActivate(1, ClueKind.VentRedSmoke);
            registry.TryMarkInspected(1, ClueKind.VentRedSmoke);
            registry.TryMarkInspected(1, ClueKind.VentRedSmoke);

            Assert.That(events, Is.EqualTo(2));
        }
    }
}
