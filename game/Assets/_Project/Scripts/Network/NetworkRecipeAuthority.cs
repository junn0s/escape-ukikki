using System.Collections.Generic;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 라운드마다 생존자별 개인 레시피를 후보 지점에 배치하고 발견을 판정한다.
    /// 배치 결과는 절대 복제하지 않는다. 생존자는 맵에서 직접 찾아야 하고(GDD §14.2),
    /// 빌런에게는 레시피가 없다(docs/system-design-document.md §4 정보 공개 표).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRecipeAuthority : NetworkBehaviour
    {
        public static NetworkRecipeAuthority Current { get; private set; }

        [SerializeField] private RecipeNotePrototype[] _candidates;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private readonly Dictionary<ulong, int> _assignments = new();
        private bool _hasAssigned;

        public int CandidateCount => _candidates?.Length ?? 0;
        public bool HasAssigned => _hasAssigned;

        public void Configure(
            RecipeNotePrototype[] candidates,
            InteractionBalanceConfig interactionConfig)
        {
            _candidates = candidates;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_candidates == null || _candidates.Length == 0 ||
                _interactionConfig == null)
            {
                Debug.LogError(
                    "[Antidote] Recipe authority references are missing.",
                    this);
                enabled = false;
                return;
            }

            Current = this;
            for (var index = 0; index < _candidates.Length; index++)
            {
                var candidate = _candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                candidate.SetInteractionAuthority(
                    this,
                    CanLocalPlayerRequestInteraction,
                    CreateRequestHandler(candidate));
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_candidates != null)
            {
                foreach (var candidate in _candidates)
                {
                    candidate?.ClearInteractionAuthority(this);
                }
            }

            _assignments.Clear();
            _hasAssigned = false;
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (!IsServer || _hasAssigned)
            {
                return;
            }

            ServerTryAssignRecipes();
        }

        /// <summary>
        /// 살아 있는 생존자 전원에게 서로 다른 후보를 배정한다.
        /// 역할 배정이 끝나기 전에는 아무 것도 하지 않고 다음 프레임에 다시 시도한다.
        /// </summary>
        public bool ServerTryAssignRecipes()
        {
            if (!IsServer || _hasAssigned || NetworkManager == null)
            {
                return false;
            }

            var survivorClientIds = new List<ulong>();
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar))
                {
                    return false;
                }

                if (avatar.Role == PlayerRole.Unassigned)
                {
                    // 아직 역할 배정 전이다. 다음 프레임에 다시 시도한다.
                    return false;
                }

                if (avatar.Role == PlayerRole.Survivor)
                {
                    survivorClientIds.Add(pair.Key);
                }
            }

            if (survivorClientIds.Count == 0)
            {
                return false;
            }

            var seed = Random.Range(int.MinValue, int.MaxValue);
            if (!RecipeAssignmentService.TryAssign(
                    survivorClientIds,
                    CandidateCount,
                    seed,
                    _assignments))
            {
                Debug.LogError(
                    $"[Antidote] Recipe candidates ({CandidateCount}) are fewer " +
                    $"than survivors ({survivorClientIds.Count}).",
                    this);
                enabled = false;
                return false;
            }

            _hasAssigned = true;
            Debug.Log(
                $"[Antidote] Assigned {survivorClientIds.Count} recipes to " +
                $"{CandidateCount} candidates.",
                this);
            return true;
        }

        /// <summary>재접속한 생존자가 아직 찾지 못한 개인 레시피 배정을 이어받는다.</summary>
        public bool ServerRebindPlayer(
            ulong previousClientId,
            ulong currentClientId)
        {
            if (!IsServer || previousClientId == currentClientId ||
                !_assignments.Remove(previousClientId, out var candidateIndex))
            {
                return false;
            }

            _assignments[currentClientId] = candidateIndex;
            return true;
        }

        private bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner)
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState != null && !roundState.AllowsMissionInteraction)
            {
                return false;
            }

            return !interactor.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private System.Action<GameObject> CreateRequestHandler(
            RecipeNotePrototype candidate)
        {
            return interactor =>
            {
                if (CanLocalPlayerRequestInteraction(interactor))
                {
                    RequestRecipeDiscoveryRpc(candidate.CandidateIndex);
                }
            };
        }

        [Rpc(SendTo.Server)]
        private void RequestRecipeDiscoveryRpc(
            int candidateIndex,
            RpcParams rpcParams = default)
        {
            if (NetworkManager == null || !_hasAssigned)
            {
                return;
            }

            var senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var candidate = FindCandidate(candidateIndex);
            if (candidate == null)
            {
                return;
            }

            var playerObject = client.PlayerObject;
            if (playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection) &&
                infection.LifeState == PlayerLifeState.DeadGhost)
            {
                return;
            }

            var range = _interactionConfig.ItemPickupRangeMeters;
            var squaredDistance = (
                (Vector2)playerObject.transform.position -
                (Vector2)candidate.transform.position).sqrMagnitude;
            if (squaredDistance > range * range)
            {
                return;
            }

            // 자기 후보가 아니면 아무 것도 알려주지 않는다.
            // 남의 레시피 위치가 새어 나가면 개인 정보 규칙이 깨진다.
            if (!_assignments.TryGetValue(senderClientId, out var assigned) ||
                assigned != candidateIndex)
            {
                return;
            }

            var inventory = playerObject
                .GetComponent<NetworkAntidoteInventoryAuthority>();
            if (inventory == null || !inventory.ServerGrantRecipe())
            {
                return;
            }

            ConfirmRecipeDiscoveryRpc(senderClientId, candidateIndex);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmRecipeDiscoveryRpc(
            ulong targetClientId,
            int candidateIndex)
        {
            if (NetworkManager == null ||
                NetworkManager.LocalClientId != targetClientId)
            {
                return;
            }

            FindCandidate(candidateIndex)?.ApplyLocalDiscovery();
        }

        private RecipeNotePrototype FindCandidate(int candidateIndex)
        {
            if (_candidates == null)
            {
                return null;
            }

            foreach (var candidate in _candidates)
            {
                if (candidate != null &&
                    candidate.CandidateIndex == candidateIndex)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
