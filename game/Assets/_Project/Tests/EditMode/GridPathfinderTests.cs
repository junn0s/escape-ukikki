using System.Collections.Generic;
using MonkeyLab.Core;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 그리드 A* 검증. NavMesh를 대체하는 핵심 로직이라 규칙을 테스트로 고정한다.
    /// docs/technical-design-document.md §10.1
    /// </summary>
    public sealed class GridPathfinderTests
    {
        /// <summary>모두 통행 가능한 격자를 만든다. 셀 크기 1m로 두면 거리 계산이 읽기 쉽다.</summary>
        private static PathGrid OpenGrid(int width, int height, float cellSize = 1f)
        {
            var grid = new PathGrid(Vector2.zero, cellSize, width, height);
            grid.Fill(true);
            return grid;
        }

        [Test]
        public void StraightLine_DistanceEqualsCellCount()
        {
            var grid = OpenGrid(10, 10);
            var finder = new GridPathfinder(grid);

            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(5, 0));

            Assert.AreEqual(5f, distance, 0.001f, "직선 5칸은 5m");
        }

        [Test]
        public void Diagonal_UsesDiagonalCost()
        {
            var grid = OpenGrid(10, 10);
            var finder = new GridPathfinder(grid);

            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(3, 3));

            // 대각선 3칸 = 3 × 1.414
            Assert.AreEqual(3f * 1.41421356f, distance, 0.01f);
        }

        [Test]
        public void CellSize_ScalesDistance()
        {
            var grid = OpenGrid(10, 10, cellSize: 0.5f);
            var finder = new GridPathfinder(grid);

            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(4, 0));

            Assert.AreEqual(2f, distance, 0.001f, "0.5m 셀 4칸은 2m");
        }

        [Test]
        public void SameCell_ReturnsZero()
        {
            var grid = OpenGrid(5, 5);
            var finder = new GridPathfinder(grid);

            Assert.AreEqual(0f, finder.GetPathDistance(new Vector2Int(2, 2), new Vector2Int(2, 2)));
        }

        [Test]
        public void BlockedGoal_ReturnsNegative()
        {
            var grid = OpenGrid(5, 5);
            grid.SetWalkable(3, 3, false);
            var finder = new GridPathfinder(grid);

            Assert.AreEqual(-1f, finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(3, 3)));
        }

        [Test]
        public void FullyWalledOff_ReturnsNegative()
        {
            // 세로 벽으로 격자를 둘로 나눈다.
            var grid = OpenGrid(7, 5);
            for (int y = 0; y < 5; y++)
            {
                grid.SetWalkable(3, y, false);
            }

            var finder = new GridPathfinder(grid);
            float distance = finder.GetPathDistance(new Vector2Int(0, 2), new Vector2Int(6, 2));

            Assert.AreEqual(-1f, distance, "완전히 막힌 경로는 -1");
        }

        [Test]
        public void WallDetour_PathDistanceExceedsStraightLine()
        {
            // 이것이 NavMesh를 대체하는 핵심 이유다.
            // GDD §11.2: 벽 너머 소리는 직선으로 가깝더라도 돌아가는 거리로 평가해야 한다.
            var grid = OpenGrid(9, 9);

            // 가운데 세로 벽. 위쪽 한 칸만 열어둔다.
            for (int y = 0; y < 8; y++)
            {
                grid.SetWalkable(4, y, false);
            }

            var finder = new GridPathfinder(grid);
            var a = new Vector2Int(3, 0);
            var b = new Vector2Int(5, 0);

            float straight = Vector2Int.Distance(a, b);          // 2
            float path = finder.GetPathDistance(a, b);

            Assert.Greater(path, 0f, "경로가 존재해야 한다");
            Assert.Greater(path, straight * 3f,
                "벽을 돌아가야 하므로 직선거리보다 훨씬 길어야 한다");
        }

        [Test]
        public void DiagonalThroughWallCorner_IsNotAllowed()
        {
            // 대각선 이동이 벽 모서리를 뚫지 못해야 한다 (map-level-design.md §11).
            // (1,0)과 (0,1)을 막으면 (0,0)에서 (1,1)로 대각선 통과가 불가능하다.
            var grid = OpenGrid(3, 3);
            grid.SetWalkable(1, 0, false);
            grid.SetWalkable(0, 1, false);

            var finder = new GridPathfinder(grid);
            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(1, 1));

            Assert.AreEqual(-1f, distance, "모서리를 대각선으로 통과할 수 없어야 한다");
        }

        [Test]
        public void DiagonalWithOneSideBlocked_TakesOrthogonalDetour()
        {
            // 대각선 이동은 양쪽 직교 셀이 모두 열려야 허용된다.
            // (1,0)만 막아도 (0,0)→(1,1) 대각선은 금지되므로 (0,1)을 거쳐 돌아간다.
            // 직교 2칸 = 2.0이며, 대각선 1.414보다 길다.
            var grid = OpenGrid(3, 3);
            grid.SetWalkable(1, 0, false);

            var finder = new GridPathfinder(grid);
            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(1, 1));

            Assert.AreEqual(2f, distance, 0.01f, "대각선이 막혀 직교로 우회한다");
        }

        [Test]
        public void DiagonalWithBothSidesOpen_IsAllowed()
        {
            // 양쪽이 모두 열려 있으면 대각선으로 지나간다.
            var grid = OpenGrid(3, 3);

            var finder = new GridPathfinder(grid);
            float distance = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(1, 1));

            Assert.AreEqual(1.41421356f, distance, 0.01f);
        }

        [Test]
        public void Path_ContainsGoalAndExcludesStart()
        {
            var grid = OpenGrid(6, 6);
            var finder = new GridPathfinder(grid);
            var path = new List<Vector2Int>();

            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(3, 0);

            finder.FindPath(start, goal, path);

            Assert.AreEqual(3, path.Count, "시작 셀은 포함하지 않는다");
            Assert.AreEqual(goal, path[^1], "마지막은 목표 셀");
            Assert.IsFalse(path.Contains(start));
        }

        [Test]
        public void Pathfinder_IsReusableAcrossCalls()
        {
            // 인스턴스를 재사용해도 이전 탐색 상태가 남지 않아야 한다.
            var grid = OpenGrid(8, 8);
            var finder = new GridPathfinder(grid);

            float first = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(5, 0));
            float second = finder.GetPathDistance(new Vector2Int(0, 0), new Vector2Int(5, 0));
            float third = finder.GetPathDistance(new Vector2Int(1, 1), new Vector2Int(6, 1));

            Assert.AreEqual(first, second, 0.001f, "같은 질의는 같은 결과");
            Assert.AreEqual(5f, third, 0.001f, "다른 질의도 정상");
        }

        [Test]
        public void FindNearestWalkable_RecoversFromInsideWall()
        {
            // 괴물이 벽 안으로 밀려났을 때의 복구 (SDD §10.4).
            var grid = OpenGrid(5, 5);
            grid.SetWalkable(2, 2, false);

            bool found = grid.TryFindNearestWalkable(
                grid.CellToWorld(2, 2), maxRingSearch: 2, out Vector2Int cell);

            Assert.IsTrue(found);
            Assert.IsTrue(grid.IsWalkable(cell.x, cell.y));
        }

        [Test]
        public void WorldToCell_RoundTrips()
        {
            var grid = new PathGrid(new Vector2(-10f, -5f), 0.5f, 40, 20);

            Vector2 world = grid.CellToWorld(12, 7);
            Vector2Int cell = grid.WorldToCell(world);

            Assert.AreEqual(new Vector2Int(12, 7), cell);
        }
    }
}
