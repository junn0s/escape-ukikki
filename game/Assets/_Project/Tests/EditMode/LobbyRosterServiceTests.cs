using System.Linq;
using MonkeyLab.Network;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class LobbyRosterServiceTests
    {
        [Test]
        public void JoiningPlayersReceiveUniqueSlotsAndColors()
        {
            var service = new LobbyRosterService();

            for (ulong clientId = 0;
                 clientId < GameSessionService.RequiredPlayerCount;
                 clientId++)
            {
                Assert.That(
                    service.AddPlayer(clientId, isHost: clientId == 0).Succeeded,
                    Is.True);
            }

            Assert.That(
                service.Players.Select(player => player.SlotIndex).Distinct().Count(),
                Is.EqualTo(GameSessionService.RequiredPlayerCount));
            Assert.That(
                service.Players.Select(player => player.Color).Distinct().Count(),
                Is.EqualTo(GameSessionService.RequiredPlayerCount));
            Assert.That(service.Players.Single(player => player.ClientId == 0).IsHost);
        }

        [Test]
        public void DuplicateColorRequestIsRejectedWithoutChangingPlayer()
        {
            var service = new LobbyRosterService();
            service.AddPlayer(0, isHost: true);
            service.AddPlayer(1, isHost: false);
            var hostColor = service.Players.Single(player => player.ClientId == 0).Color;
            var originalClientColor =
                service.Players.Single(player => player.ClientId == 1).Color;

            var result = service.SetColor(1, hostColor);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureKind,
                Is.EqualTo(LobbyRosterFailureKind.ColorAlreadyTaken));
            Assert.That(
                service.Players.Single(player => player.ClientId == 1).Color,
                Is.EqualTo(originalClientColor));
        }

        [Test]
        public void ReadyStateCanBeSetAndReleased()
        {
            var service = new LobbyRosterService();
            service.AddPlayer(0, isHost: true);

            Assert.That(service.SetReady(0, true).Succeeded, Is.True);
            Assert.That(service.Players.Single().IsReady, Is.True);

            Assert.That(service.SetReady(0, false).Succeeded, Is.True);
            Assert.That(service.Players.Single().IsReady, Is.False);
        }

        [Test]
        public void NormalStartRequiresSixReadyPlayers()
        {
            var service = new LobbyRosterService();
            for (ulong clientId = 0;
                 clientId < GameSessionService.RequiredPlayerCount;
                 clientId++)
            {
                service.AddPlayer(clientId, isHost: clientId == 0);
            }

            var beforeReady = service.CanStart(0);
            Assert.That(beforeReady.Succeeded, Is.False);
            Assert.That(
                beforeReady.FailureKind,
                Is.EqualTo(LobbyRosterFailureKind.PlayersNotReady));

            foreach (var player in service.Players.ToArray())
            {
                service.SetReady(player.ClientId, true);
            }

            Assert.That(service.CanStart(0).Succeeded, Is.True);
        }

        [Test]
        public void NonHostCannotStartReadyLobby()
        {
            var service = new LobbyRosterService();
            for (ulong clientId = 0;
                 clientId < GameSessionService.RequiredPlayerCount;
                 clientId++)
            {
                service.AddPlayer(clientId, isHost: clientId == 0);
                service.SetReady(clientId, true);
            }

            var result = service.CanStart(1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureKind,
                Is.EqualTo(LobbyRosterFailureKind.NotHost));
        }

        [Test]
        public void DevelopmentStartAllowsHostWithFewerPlayers()
        {
            var service = new LobbyRosterService();
            service.AddPlayer(0, isHost: true);

            Assert.That(service.CanStart(0).Succeeded, Is.False);
            Assert.That(
                service.CanStart(0, allowDevelopmentStart: true).Succeeded,
                Is.True);
        }

        [Test]
        public void RemovingPlayerReleasesSlotAndColorForNextJoin()
        {
            var service = new LobbyRosterService();
            service.AddPlayer(0, isHost: true);
            service.AddPlayer(1, isHost: false);
            var removed = service.Players.Single(player => player.ClientId == 1);

            service.RemovePlayer(1);
            service.AddPlayer(2, isHost: false);
            var replacement = service.Players.Single(player => player.ClientId == 2);

            Assert.That(replacement.SlotIndex, Is.EqualTo(removed.SlotIndex));
            Assert.That(replacement.Color, Is.EqualTo(removed.Color));
        }
    }
}
