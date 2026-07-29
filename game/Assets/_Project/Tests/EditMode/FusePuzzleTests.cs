using MonkeyLab.Core;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 퓨즈 미션 로직 검증. GDD §10.1, §10.2
    /// </summary>
    public sealed class FusePuzzleTests
    {
        private static FusePuzzle Create() => new(new[] { 2, 0, 3, 1 });

        [Test]
        public void CorrectSequence_Completes()
        {
            var puzzle = Create();

            Assert.IsTrue(puzzle.TryInsert(2));
            Assert.IsTrue(puzzle.TryInsert(0));
            Assert.IsTrue(puzzle.TryInsert(3));
            Assert.IsFalse(puzzle.IsComplete, "마지막 전까지는 미완료");
            Assert.IsTrue(puzzle.TryInsert(1));

            Assert.IsTrue(puzzle.IsComplete);
            Assert.AreEqual(4, puzzle.Progress);
        }

        [Test]
        public void WrongInput_ResetsProgress()
        {
            // GDD §10.1: 미션을 중단하거나 실패하면 진행 상황은 초기화한다.
            var puzzle = Create();
            puzzle.TryInsert(2);
            puzzle.TryInsert(0);

            Assert.AreEqual(2, puzzle.Progress);
            Assert.IsFalse(puzzle.TryInsert(9), "잘못된 퓨즈");
            Assert.AreEqual(0, puzzle.Progress, "진행이 초기화되어야 한다");
            Assert.IsTrue(puzzle.HasFailedLastInput);
        }

        [Test]
        public void ExpectedFuseId_TracksProgress()
        {
            var puzzle = Create();

            Assert.AreEqual(2, puzzle.ExpectedFuseId);
            puzzle.TryInsert(2);
            Assert.AreEqual(0, puzzle.ExpectedFuseId);
            puzzle.TryInsert(0);
            Assert.AreEqual(3, puzzle.ExpectedFuseId);
        }

        [Test]
        public void CompletedPuzzle_IgnoresFurtherInput()
        {
            var puzzle = Create();
            foreach (int id in new[] { 2, 0, 3, 1 })
            {
                puzzle.TryInsert(id);
            }

            Assert.IsTrue(puzzle.IsComplete);
            Assert.IsFalse(puzzle.TryInsert(2), "완료 후 입력은 무시된다");
            Assert.AreEqual(-1, puzzle.ExpectedFuseId);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var puzzle = Create();
            puzzle.TryInsert(2);
            puzzle.Reset();

            Assert.AreEqual(0, puzzle.Progress);
            Assert.IsFalse(puzzle.IsComplete);
            Assert.AreEqual(2, puzzle.ExpectedFuseId);
        }

        [Test]
        public void SuccessAfterFailure_StartsFromBeginning()
        {
            var puzzle = Create();
            puzzle.TryInsert(2);
            puzzle.TryInsert(9); // 실패

            // 실패 후에는 처음부터 다시 넣어야 한다.
            Assert.IsFalse(puzzle.TryInsert(0), "초기화됐으므로 0은 이제 오답");
            Assert.IsTrue(puzzle.TryInsert(2), "다시 첫 퓨즈부터");
        }

        [Test]
        public void EmptyOrder_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new FusePuzzle(new int[0]));
            Assert.Throws<System.ArgumentException>(() => new FusePuzzle(null));
        }
    }
}
