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
            Rect.MinMaxRect(-42.5f, -25f, 38f, 32f);
        private static readonly Rect LaboratoryFloorBounds =
            Rect.MinMaxRect(-40.5f, -23f, 36f, 30f);

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
        public void MapBounds_KeepTwoMeterBarrierOutsideLaboratoryFloor()
        {
            Assert.That(
                MapBounds.xMin,
                Is.EqualTo(LaboratoryFloorBounds.xMin - 2f));
            Assert.That(
                MapBounds.xMax,
                Is.EqualTo(LaboratoryFloorBounds.xMax + 2f));
            Assert.That(
                MapBounds.yMin,
                Is.EqualTo(LaboratoryFloorBounds.yMin - 2f));
            Assert.That(
                MapBounds.yMax,
                Is.EqualTo(LaboratoryFloorBounds.yMax + 2f));
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
