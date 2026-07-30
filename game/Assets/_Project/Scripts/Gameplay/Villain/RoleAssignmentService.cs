using System;
using System.Collections.Generic;

namespace MonkeyLab.Gameplay.Villain
{
    public sealed class RoleAssignmentService
    {
        public IReadOnlyList<PlayerRoleAssignment> AssignRoles(
            IReadOnlyList<ulong> participantClientIds,
            int villainIndex)
        {
            if (participantClientIds == null ||
                participantClientIds.Count == 0)
            {
                throw new ArgumentException(
                    "At least one participant is required.",
                    nameof(participantClientIds));
            }

            if (villainIndex < 0 ||
                villainIndex >= participantClientIds.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(villainIndex));
            }

            var seenClientIds = new HashSet<ulong>();
            var assignments =
                new PlayerRoleAssignment[participantClientIds.Count];
            for (var index = 0;
                 index < participantClientIds.Count;
                 index++)
            {
                var clientId = participantClientIds[index];
                if (!seenClientIds.Add(clientId))
                {
                    throw new ArgumentException(
                        "Participant client IDs must be unique.",
                        nameof(participantClientIds));
                }

                assignments[index] = new PlayerRoleAssignment(
                    clientId,
                    index == villainIndex
                        ? PlayerRole.Villain
                        : PlayerRole.Survivor);
            }

            return assignments;
        }
    }
}
