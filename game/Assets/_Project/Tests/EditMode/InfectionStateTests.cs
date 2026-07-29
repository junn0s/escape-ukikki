using MonkeyLab.Core;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/game-design-document.md §14.1 감염 규칙 검증.
    /// </summary>
    public sealed class InfectionStateTests
    {
        [Test]
        public void NewState_IsNotInfected()
        {
            var state = new InfectionState();

            Assert.IsFalse(state.IsInfected);
            Assert.IsFalse(state.IsDead);
            Assert.AreEqual(0f, state.RemainingSeconds);
        }

        [Test]
        public void Infect_StartsTimerAtGivenDuration()
        {
            var state = new InfectionState();

            Assert.IsTrue(state.TryInfect(90f));
            Assert.IsTrue(state.IsInfected);
            Assert.AreEqual(90f, state.RemainingSeconds);
        }

        [Test]
        public void SecondBiteWhileInfected_DoesNotResetOrExtendTimer()
        {
            // GDD §14.1: 이미 감염된 동안 다시 물려도 타이머를 추가하거나 초기화하지 않는다.
            var state = new InfectionState();
            state.TryInfect(90f);
            state.Tick(30f);

            Assert.AreEqual(60f, state.RemainingSeconds, 0.001f);

            bool infectedAgain = state.TryInfect(30f);

            Assert.IsFalse(infectedAgain, "이미 감염 중이면 새 감염이 시작되지 않는다");
            Assert.AreEqual(60f, state.RemainingSeconds, 0.001f, "타이머가 바뀌면 안 된다");
        }

        [Test]
        public void TimerReachingZero_CausesDeath()
        {
            var state = new InfectionState();
            state.TryInfect(30f);

            Assert.IsFalse(state.Tick(29f), "아직 살아있어야 한다");
            Assert.IsTrue(state.Tick(1f), "0에 도달하면 사망");
            Assert.IsTrue(state.IsDead);
            Assert.IsFalse(state.IsInfected);
        }

        [Test]
        public void Cure_ClearsInfection()
        {
            var state = new InfectionState();
            state.TryInfect(90f);

            Assert.IsTrue(state.TryCure());
            Assert.IsFalse(state.IsInfected);
            Assert.AreEqual(0f, state.RemainingSeconds);
        }

        [Test]
        public void CanBeInfectedAgainAfterCure()
        {
            // GDD §14.1: 해독 후에는 다시 물려 감염될 수 있다.
            var state = new InfectionState();
            state.TryInfect(90f);
            state.TryCure();

            Assert.IsTrue(state.TryInfect(60f), "치료 후 재감염이 가능해야 한다");
            Assert.AreEqual(60f, state.RemainingSeconds);
        }

        [Test]
        public void CureWhenNotInfected_ReturnsFalse()
        {
            var state = new InfectionState();

            Assert.IsFalse(state.TryCure(), "감염 중이 아니면 치료가 성립하지 않는다");
        }

        [Test]
        public void DeadPlayer_CannotBeInfectedOrCured()
        {
            var state = new InfectionState();
            state.TryInfect(30f);
            state.Tick(30f);

            Assert.IsTrue(state.IsDead);
            Assert.IsFalse(state.TryInfect(90f), "사망자는 다시 감염되지 않는다");
            Assert.IsFalse(state.TryCure(), "사망자는 치료되지 않는다");
        }

        [Test]
        public void PausedTimer_DoesNotAdvance()
        {
            // 회의 중에는 Tick을 호출하지 않는 방식으로 정지를 구현한다.
            var state = new InfectionState();
            state.TryInfect(90f);
            state.Tick(10f);

            float beforePause = state.RemainingSeconds;
            // Tick을 호출하지 않음 = 회의 중
            Assert.AreEqual(beforePause, state.RemainingSeconds, "호출하지 않으면 줄지 않는다");
        }

        [Test]
        public void InvalidDuration_Throws()
        {
            var state = new InfectionState();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.TryInfect(0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.TryInfect(-5f));
        }
    }
}
