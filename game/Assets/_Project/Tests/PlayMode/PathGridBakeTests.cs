using System.Collections;
using MonkeyLab.Core;
using MonkeyLab.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonkeyLab.Tests.PlayMode
{
    /// <summary>
    /// 실제 씬에서 길찾기 격자가 제대로 구워지는지 검증한다.
    /// 순수 로직 테스트(GridPathfinderTests)와 달리, 씬의 콜라이더 배치가
    /// 의도대로 통행 가능/불가로 변환되는지를 본다.
    /// </summary>
    public sealed class PathGridBakeTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/91_GameplaySandbox.unity";

        [UnityTest]
        public IEnumerator SandboxScene_BakesUsableGrid()
        {
            yield return LoadSandbox();

            PathGridBaker baker = Object.FindFirstObjectByType<PathGridBaker>();
            Assert.IsNotNull(baker, "씬에 PathGridBaker가 있어야 한다");

            PathGrid grid = baker.Grid;
            Assert.IsNotNull(grid, "Awake에서 격자가 구워져야 한다");

            int walkable = CountWalkable(grid);
            int total = grid.Width * grid.Height;

            Assert.Greater(walkable, 0, "통행 가능 셀이 하나도 없다");
            Assert.Less(walkable, total, "모든 셀이 통행 가능하면 벽이 인식되지 않은 것이다");

            // 방과 복도가 격자 안에 들어와야 한다. 너무 적으면 범위나 원점이 잘못됐다.
            float ratio = (float)walkable / total;
            Assert.Greater(ratio, 0.3f, $"통행 가능 비율이 너무 낮다 ({ratio:P0})");
        }

        [UnityTest]
        public IEnumerator Monster_CanReachPlayerStartAcrossMap()
        {
            // 맵 양 끝(전력 복구실 ↔ 백신실 A)이 실제로 연결돼 있어야 한다.
            // 벽이 잘못 배치되면 괴물이 영원히 도달하지 못한다.
            yield return LoadSandbox();

            PathGridBaker baker = Object.FindFirstObjectByType<PathGridBaker>();
            var finder = new GridPathfinder(baker.Grid);

            Vector2Int start = baker.Grid.WorldToCell(new Vector2(14f, 0f));   // 괴물 시작
            Vector2Int goal = baker.Grid.WorldToCell(new Vector2(-14f, 0f));   // 플레이어 시작

            Assert.IsTrue(baker.Grid.IsWalkable(start.x, start.y), "괴물 시작 지점이 벽 안이다");
            Assert.IsTrue(baker.Grid.IsWalkable(goal.x, goal.y), "플레이어 시작 지점이 벽 안이다");

            float distance = finder.GetPathDistance(start, goal);

            Assert.Greater(distance, 0f, "맵 양 끝이 연결되지 않았다");

            // 두 지점은 y=0 직선상에 28m 떨어져 있고 복도도 일직선이라
            // 최단 경로가 정확히 직선거리와 같다. 그보다 짧으면 벽을 통과한 것이다.
            Assert.GreaterOrEqual(distance, 28f, "직선거리보다 짧으면 벽을 통과한 것이다");
            Assert.Less(distance, 28f * 1.5f, "우회가 과도하면 복도 배치를 확인하라");
        }

        [UnityTest]
        public IEnumerator MonsterAgent_ReceivesGrid()
        {
            yield return LoadSandbox();

            GridPathAgent agent = Object.FindFirstObjectByType<GridPathAgent>();
            Assert.IsNotNull(agent, "씬에 GridPathAgent가 있어야 한다");

            // Initialize가 됐다면 경로 조회가 -1이 아닌 값을 낸다.
            float distance = agent.QueryPathDistance(new Vector2(0f, 0f));
            Assert.GreaterOrEqual(distance, 0f, "에이전트에 격자가 주입되지 않았다");
        }

        private static IEnumerator LoadSandbox()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            // Awake/Start가 돌 시간을 준다.
            yield return null;
            yield return null;
        }

        private static int CountWalkable(PathGrid grid)
        {
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsWalkable(x, y))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
