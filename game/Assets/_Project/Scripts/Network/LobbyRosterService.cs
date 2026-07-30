using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MonkeyLab.Network
{
    public sealed class LobbyRosterService
    {
        private readonly List<LobbyPlayerState> _players = new();
        private readonly ReadOnlyCollection<LobbyPlayerState> _readOnlyPlayers;

        public LobbyRosterService()
        {
            _readOnlyPlayers = _players.AsReadOnly();
        }

        public IReadOnlyList<LobbyPlayerState> Players => _readOnlyPlayers;

        public LobbyRosterResult AddPlayer(ulong clientId, bool isHost)
        {
            if (FindPlayerIndex(clientId) >= 0)
            {
                return LobbyRosterResult.Success();
            }

            if (_players.Count >= GameSessionService.RequiredPlayerCount)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.LobbyFull);
            }

            var slotIndex = FindAvailableSlot();
            var color = FindAvailableColor();
            _players.Add(new LobbyPlayerState(
                clientId,
                slotIndex,
                $"Player {slotIndex + 1}",
                color,
                isReady: false,
                isHost));
            _players.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            return LobbyRosterResult.Success();
        }

        public LobbyRosterResult RemovePlayer(ulong clientId)
        {
            var playerIndex = FindPlayerIndex(clientId);
            if (playerIndex < 0)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.PlayerNotFound);
            }

            _players.RemoveAt(playerIndex);
            return LobbyRosterResult.Success();
        }

        public LobbyRosterResult SetReady(ulong clientId, bool isReady)
        {
            var playerIndex = FindPlayerIndex(clientId);
            if (playerIndex < 0)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.PlayerNotFound);
            }

            _players[playerIndex] = _players[playerIndex].WithReady(isReady);
            return LobbyRosterResult.Success();
        }

        public LobbyRosterResult SetColor(ulong clientId, LobbyPlayerColor color)
        {
            if (!IsValidColor(color))
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.InvalidColor);
            }

            var playerIndex = FindPlayerIndex(clientId);
            if (playerIndex < 0)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.PlayerNotFound);
            }

            if (_players.Any(player =>
                    player.ClientId != clientId &&
                    player.Color == color))
            {
                return LobbyRosterResult.Failure(
                    LobbyRosterFailureKind.ColorAlreadyTaken);
            }

            _players[playerIndex] = _players[playerIndex].WithColor(color);
            return LobbyRosterResult.Success();
        }

        public LobbyRosterResult CanStart(
            ulong requesterClientId,
            bool allowDevelopmentStart = false)
        {
            var requesterIndex = FindPlayerIndex(requesterClientId);
            if (requesterIndex < 0)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.PlayerNotFound);
            }

            if (!_players[requesterIndex].IsHost)
            {
                return LobbyRosterResult.Failure(LobbyRosterFailureKind.NotHost);
            }

            if (allowDevelopmentStart)
            {
                return LobbyRosterResult.Success();
            }

            if (_players.Count != GameSessionService.RequiredPlayerCount)
            {
                return LobbyRosterResult.Failure(
                    LobbyRosterFailureKind.NotEnoughPlayers);
            }

            return _players.All(player => player.IsReady)
                ? LobbyRosterResult.Success()
                : LobbyRosterResult.Failure(
                    LobbyRosterFailureKind.PlayersNotReady);
        }

        private int FindPlayerIndex(ulong clientId)
        {
            return _players.FindIndex(player => player.ClientId == clientId);
        }

        private int FindAvailableSlot()
        {
            for (var slotIndex = 0;
                 slotIndex < GameSessionService.RequiredPlayerCount;
                 slotIndex++)
            {
                if (_players.All(player => player.SlotIndex != slotIndex))
                {
                    return slotIndex;
                }
            }

            return _players.Count;
        }

        private LobbyPlayerColor FindAvailableColor()
        {
            for (var colorIndex = 0;
                 colorIndex < GameSessionService.RequiredPlayerCount;
                 colorIndex++)
            {
                var color = (LobbyPlayerColor)colorIndex;
                if (_players.All(player => player.Color != color))
                {
                    return color;
                }
            }

            return LobbyPlayerColor.Blue;
        }

        private static bool IsValidColor(LobbyPlayerColor color)
        {
            return color >= LobbyPlayerColor.Blue &&
                   color <= LobbyPlayerColor.Orange;
        }
    }
}
