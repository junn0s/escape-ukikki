using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 액체 보관실 생존자 미션 2종(GDD §10.2)과 빌런 위장 조작(§13.2)의
    /// 순수 판정 로직을 검증한다. 밸브 잠그기(생존자)와 밸브 압력 풀기(빌런)는
    /// 같은 RotateValveMissionRules 클래스를 반대 방향으로 사용한다.
    /// </summary>
    public sealed class StorageRoomMissionTests
    {
        // --- 밸브 잠그기 / 밸브 압력 풀기 (방향성 있는 회전) ---

        [Test]
        public void RotateValve_ClockwiseCompletesAfterThreeTurns()
        {
            var rules = new RotateValveMissionRules(
                requiredTurns: 3f,
                isClockwise: true);

            Assert.That(rules.Rotate(360f), Is.False);
            Assert.That(rules.Rotate(360f), Is.False);
            Assert.That(rules.Rotate(360f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void RotateValve_CounterClockwiseCompletesAfterThreeTurns()
        {
            var rules = new RotateValveMissionRules(
                requiredTurns: 3f,
                isClockwise: false);

            rules.Rotate(-360f);
            rules.Rotate(-360f);
            Assert.That(rules.Rotate(-360f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void RotateValve_WrongDirectionDoesNotAccumulate()
        {
            var rules = new RotateValveMissionRules(
                requiredTurns: 3f,
                isClockwise: true);

            Assert.That(
                rules.Rotate(-90f),
                Is.False,
                "목표 방향과 반대로 돌리면 진행되지 않아야 한다.");
            Assert.That(rules.AccumulatedDegrees, Is.Zero);
        }

        [Test]
        public void RotateValve_LockAndLoosenAreIndependentOnSameValve()
        {
            // GDD §13.2: 밸브 잠그기와 밸브 압력 풀기는 같은 오브젝트를
            // 반대 방향으로 조작하되 서로 다른 진행치를 갖는다.
            var lockRules = new RotateValveMissionRules(
                requiredTurns: 3f,
                isClockwise: true);
            var loosenRules = new RotateValveMissionRules(
                requiredTurns: 3f,
                isClockwise: false);

            lockRules.Rotate(360f);

            Assert.That(lockRules.AccumulatedDegrees, Is.EqualTo(360f));
            Assert.That(
                loosenRules.AccumulatedDegrees,
                Is.Zero,
                "한쪽 진행이 다른 쪽에 영향을 주면 안 된다.");
        }

        [Test]
        public void RotateValve_CannotRotateAfterCompletion()
        {
            var rules = new RotateValveMissionRules(
                requiredTurns: 1f,
                isClockwise: true);
            rules.Rotate(360f);

            Assert.That(rules.Rotate(360f), Is.False);
        }

        [Test]
        public void RotateValve_ResetClearsProgress()
        {
            var rules = new RotateValveMissionRules(
                requiredTurns: 1f,
                isClockwise: true);
            rules.Rotate(360f);

            rules.Reset();

            Assert.That(rules.AccumulatedDegrees, Is.Zero);
            Assert.That(rules.IsCompleted, Is.False);
        }

        // --- 폐기물 통 압축 (레버 5초 누르고 있기, 다운로드 미션과 조작 공유) ---

        [Test]
        public void WasteCompactor_ReusesHoldButtonRulesForFiveSeconds()
        {
            var rules = new HoldButtonMissionRules();
            rules.BeginHold();

            Assert.That(rules.Tick(4f, requiredSeconds: 5f), Is.False);
            Assert.That(rules.Tick(1f, requiredSeconds: 5f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void StorageBalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(config.ValveLockTurns, Is.EqualTo(3f).Within(0.001f));
                Assert.That(
                    config.WasteCompactorHoldSeconds,
                    Is.EqualTo(5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
