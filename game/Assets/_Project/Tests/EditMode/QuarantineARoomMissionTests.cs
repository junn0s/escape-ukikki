using MonkeyLab.Gameplay.Missions;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 격리실 A 생존자 미션 3종(GDD §10.2)의 순수 판정 로직을 검증한다.
    /// </summary>
    public sealed class QuarantineARoomMissionTests
    {
        // --- 배선 복구 (같은 색 전선 연결) ---

        [Test]
        public void WireConnect_CompletesWhenAllWiresMatched()
        {
            var rules = new WireConnectMissionRules(
                new[] { 0, 1, 2, 3 });

            Assert.That(rules.TryConnect(0, 0), Is.True);
            Assert.That(rules.TryConnect(1, 1), Is.True);
            Assert.That(rules.TryConnect(2, 2), Is.True);
            Assert.That(rules.IsCompleted, Is.False);

            Assert.That(rules.TryConnect(3, 3), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
        }

        [Test]
        public void WireConnect_RejectsMismatchedColors()
        {
            var rules = new WireConnectMissionRules(
                new[] { 0, 1, 2, 3 });

            Assert.That(
                rules.TryConnect(0, 1),
                Is.False,
                "색이 다른 전선끼리는 연결할 수 없다.");
            Assert.That(rules.IsConnected(0), Is.False);
        }

        [Test]
        public void WireConnect_RejectsAlreadyConnectedWire()
        {
            var rules = new WireConnectMissionRules(
                new[] { 0, 1, 2, 3 });
            rules.TryConnect(0, 0);

            Assert.That(rules.TryConnect(0, 0), Is.False);
        }

        [Test]
        public void WireConnect_ResetClearsAllConnections()
        {
            var rules = new WireConnectMissionRules(
                new[] { 0, 1, 2, 3 });
            rules.TryConnect(0, 0);
            rules.TryConnect(1, 1);

            rules.Reset();

            Assert.That(rules.ConnectedCount, Is.Zero);
            Assert.That(rules.IsConnected(0), Is.False);
        }

        // --- 에어록 압력 조절 (다이얼을 0에 맞추기) ---

        [Test]
        public void DialToZero_CompletesWithinTolerance()
        {
            var rules = new DialToZeroMissionRules(toleranceDegrees: 8f);
            rules.SetAngle(90f);

            rules.Rotate(-85f);

            Assert.That(rules.IsCompleted, Is.True);
            Assert.That(
                rules.CurrentAngleDegrees,
                Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void DialToZero_DoesNotCompleteOutsideTolerance()
        {
            var rules = new DialToZeroMissionRules(toleranceDegrees: 8f);
            rules.SetAngle(90f);

            rules.Rotate(-70f);

            Assert.That(rules.IsCompleted, Is.False);
        }

        [Test]
        public void DialToZero_WrapsAngleToShortestPath()
        {
            var rules = new DialToZeroMissionRules(toleranceDegrees: 8f);
            rules.SetAngle(170f);

            rules.Rotate(20f);

            Assert.That(
                rules.CurrentAngleDegrees,
                Is.EqualTo(-170f).Within(0.01f),
                "170도에서 20도를 더하면 -170도로 감싸져야 한다.");
        }

        [Test]
        public void DialToZero_ResetReturnsToZeroAndIncomplete()
        {
            var rules = new DialToZeroMissionRules(toleranceDegrees: 8f);
            rules.SetAngle(3f);
            Assert.That(rules.IsCompleted, Is.True);

            rules.Reset();

            Assert.That(rules.CurrentAngleDegrees, Is.Zero);
            Assert.That(rules.IsCompleted, Is.False);
        }

        // --- 방호복 소독 (6초 시야 차단) ---

        [Test]
        public void TimedBlind_CompletesAfterDuration()
        {
            var rules = new TimedBlindMissionRules();
            rules.TryStart();

            Assert.That(rules.Tick(5f, durationSeconds: 6f), Is.False);
            Assert.That(rules.IsRunning, Is.True);

            Assert.That(rules.Tick(1f, durationSeconds: 6f), Is.True);
            Assert.That(rules.IsCompleted, Is.True);
            Assert.That(rules.IsRunning, Is.False);
        }

        [Test]
        public void TimedBlind_CannotStartTwice()
        {
            var rules = new TimedBlindMissionRules();
            rules.TryStart();

            Assert.That(
                rules.TryStart(),
                Is.False,
                "이미 진행 중이면 다시 시작할 수 없다.");
        }

        [Test]
        public void TimedBlind_CannotRestartAfterCompletion()
        {
            var rules = new TimedBlindMissionRules();
            rules.TryStart();
            rules.Tick(6f, durationSeconds: 6f);

            Assert.That(rules.TryStart(), Is.False);
        }

        [Test]
        public void TimedBlind_TickDoesNothingWhenNotRunning()
        {
            var rules = new TimedBlindMissionRules();

            var completed = rules.Tick(6f, durationSeconds: 6f);

            Assert.That(completed, Is.False);
            Assert.That(rules.ElapsedSeconds, Is.Zero);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §7.2) ---

        [Test]
        public void QuarantineABalance_MatchesBalanceTable()
        {
            var config = UnityEngine.ScriptableObject
                .CreateInstance<SurvivorMissionBalanceConfig>();
            try
            {
                Assert.That(config.WireConnectCount, Is.EqualTo(4));
                Assert.That(
                    config.AirlockDialToleranceDegrees,
                    Is.EqualTo(8f).Within(0.001f));
                Assert.That(
                    config.HazmatDecontaminationSeconds,
                    Is.EqualTo(6f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
