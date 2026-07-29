using System;
using MonkeyLab.Gameplay.Domain;
using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class FuseMissionInstanceTests
    {
        [Test]
        public void CorrectOrder_CompletesMission()
        {
            var mission = new FuseMissionInstance(new[] { 2, 1, 3 });

            mission.Begin();

            Assert.That(mission.SubmitFuse(2), Is.EqualTo(FuseMissionInputResult.Accepted));
            Assert.That(mission.SubmitFuse(1), Is.EqualTo(FuseMissionInputResult.Accepted));
            Assert.That(mission.SubmitFuse(3), Is.EqualTo(FuseMissionInputResult.Completed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            Assert.That(mission.ProgressIndex, Is.EqualTo(3));
        }

        [Test]
        public void WrongFuse_FailsImmediately()
        {
            var mission = new FuseMissionInstance(new[] { 3, 1, 2 });
            mission.Begin();

            var result = mission.SubmitFuse(1);

            Assert.That(result, Is.EqualTo(FuseMissionInputResult.Failed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
            Assert.That(mission.ProgressIndex, Is.Zero);
        }

        [Test]
        public void InputAfterCompletion_IsIgnored()
        {
            var mission = new FuseMissionInstance(new[] { 1, 2, 3 });
            mission.Begin();
            mission.SubmitFuse(1);
            mission.SubmitFuse(2);
            mission.SubmitFuse(3);

            var result = mission.SubmitFuse(1);

            Assert.That(result, Is.EqualTo(FuseMissionInputResult.Ignored));
            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
        }

        [Test]
        public void InvalidOrder_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new FuseMissionInstance(new[] { 1, 1, 3 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuseMissionInstance(new[] { 1, 2 }));
        }

        [Test]
        public void CancelDuringMission_ChangesState()
        {
            var mission = new FuseMissionInstance(new[] { 1, 3, 2 });
            mission.Begin();

            mission.Cancel();

            Assert.That(mission.State, Is.EqualTo(MissionState.Cancelled));
        }
    }
}
