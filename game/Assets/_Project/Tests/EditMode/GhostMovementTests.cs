using MonkeyLab.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 유령 이동 규칙을 고정한다. GDD §17: 벽은 통과하되 맵 밖으로 나갈 수 없다.
    /// </summary>
    public sealed class GhostMovementTests
    {
        private static readonly Rect MapBounds =
            new(-52f, -40f, 119f, 76f);

        private GameObject _playerObject;
        private GhostMovementController _controller;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("GhostTestPlayer");
            _playerObject.AddComponent<Rigidbody2D>();
            _controller =
                _playerObject.AddComponent<GhostMovementController>();
            _controller.Configure(
                null,
                _playerObject.GetComponent<Rigidbody2D>(),
                null,
                null,
                MapBounds);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
        }

        [Test]
        public void ClampToMap_KeepsInsidePositionUnchanged()
        {
            var inside = new Vector2(0f, 0f);

            Assert.That(_controller.ClampToMap(inside), Is.EqualTo(inside));
        }

        [Test]
        public void ClampToMap_BlocksMovementBeyondLeftEdge()
        {
            var clamped = _controller.ClampToMap(new Vector2(-999f, 0f));

            Assert.That(clamped.x, Is.EqualTo(MapBounds.xMin));
        }

        [Test]
        public void ClampToMap_BlocksMovementBeyondRightEdge()
        {
            var clamped = _controller.ClampToMap(new Vector2(999f, 0f));

            Assert.That(clamped.x, Is.EqualTo(MapBounds.xMax));
        }

        [Test]
        public void ClampToMap_BlocksMovementBeyondVerticalEdges()
        {
            Assert.That(
                _controller.ClampToMap(new Vector2(0f, -999f)).y,
                Is.EqualTo(MapBounds.yMin));
            Assert.That(
                _controller.ClampToMap(new Vector2(0f, 999f)).y,
                Is.EqualTo(MapBounds.yMax));
        }

        [Test]
        public void MapBounds_CoverEveryLaboratoryRoom()
        {
            // FirstPlayableBuilder의 방 중심과 크기를 모두 감싸야 한다.
            var roomExtremes = new[]
            {
                new Vector2(-42f, 4f),   // VaccineA
                new Vector2(-10f, 24f),  // LabA
                new Vector2(13f, 24f),   // QuarantineA
                new Vector2(-25f, -7f),  // Storage
                new Vector2(-7f, -7f),   // Security
                new Vector2(13f, -7f),   // Power
                new Vector2(-7f, -29f),  // Ward
                new Vector2(13f, -29f),  // LabB
                new Vector2(35f, -29f),  // QuarantineB
                new Vector2(55f, -29f)   // VaccineB
            };

            foreach (var room in roomExtremes)
            {
                Assert.That(
                    MapBounds.Contains(room),
                    Is.True,
                    $"맵 경계가 방 중심 {room}을 포함하지 않는다.");
            }
        }

        [Test]
        public void GhostSpeed_MatchesBalanceTable()
        {
            var config =
                ScriptableObject.CreateInstance<PlayerMovementConfig>();
            try
            {
                // balance-and-telemetry.md §3: 유령 이동 속도 4.8m/s
                Assert.That(
                    config.GhostMoveSpeed,
                    Is.EqualTo(4.8f).Within(0.001f));
                Assert.That(
                    config.GhostMoveSpeed,
                    Is.GreaterThan(config.MoveSpeed),
                    "유령은 살아 있는 플레이어보다 빨라야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void IsGhost_FalseWithoutInfectionService()
        {
            Assert.That(_controller.IsGhost, Is.False);
        }
    }
}
