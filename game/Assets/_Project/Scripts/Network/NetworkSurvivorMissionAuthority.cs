using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 신규 생존자 미션 22종이 공유하는 배정·역할·거리·완료 검증 계층이다.
    /// 개별 권위 컴포넌트는 퍼즐 입력만 판정하고 개인 일지와 프로젝트 점수는
    /// 이 컴포넌트를 통해서만 변경한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkSurvivorMissionAuthority : NetworkBehaviour
    {
        public const ulong NoInteractorClientId = ulong.MaxValue;

        [SerializeField] private SurvivorMissionKind _kind;
        [SerializeField] private InteractionBalanceConfig _interactionConfig;

        private ulong _lastServerInteractorClientId = NoInteractorClientId;

        public SurvivorMissionKind Kind => _kind;
        public ulong MissionId => SurvivorMissionCatalog.GetMissionId(_kind);
        public Vector2 Position => transform.position;
        public ulong LastServerInteractorClientId =>
            _lastServerInteractorClientId;

        public void Configure(
            SurvivorMissionKind kind,
            InteractionBalanceConfig interactionConfig)
        {
            _kind = kind;
            _interactionConfig = interactionConfig;
        }

        public override void OnNetworkSpawn()
        {
            if (_interactionConfig == null)
            {
                Debug.LogError(
                    $"[Mission] {name} has no interaction balance config.",
                    this);
                enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            _lastServerInteractorClientId = NoInteractorClientId;
        }

        public bool CanLocalPlayerRequestInteraction(GameObject interactor)
        {
            if (!IsSpawned || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(
                    out var playerNetworkObject) ||
                !playerNetworkObject.IsOwner ||
                !interactor.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Survivor ||
                !interactor.TryGetComponent<NetworkPlayerMissionJournal>(
                    out var journal))
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            if (roundState == null || !roundState.AllowsMissionInteraction)
            {
                return false;
            }

            var hasPersonalMission =
                journal.IsAssigned(MissionId) &&
                !journal.IsCompleted(MissionId);
            var isGhost =
                interactor.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection) &&
                infection.LifeState == PlayerLifeState.DeadGhost;
            var hasRecoveryMission =
                !isGhost && roundState.HasRecoveryMission(MissionId);
            return hasPersonalMission || hasRecoveryMission;
        }

        public bool ServerCanProcess(ulong clientId)
        {
            if (!IsServer || NetworkManager == null ||
                _interactionConfig == null ||
                !TryGetEligibleServerPlayer(
                    clientId,
                    out var playerObject,
                    out _,
                    out _))
            {
                return false;
            }

            var range = _interactionConfig.GeneralInteractionRangeMeters;
            if (((Vector2)playerObject.transform.position - Position)
                .sqrMagnitude > range * range)
            {
                return false;
            }

            _lastServerInteractorClientId = clientId;
            return true;
        }

        public bool ServerTryComplete(ulong clientId)
        {
            if (!ServerCanProcess(clientId))
            {
                return false;
            }

            return ServerTryCompleteEligiblePlayer(clientId);
        }

        public bool ServerTryCompleteLastInteractor()
        {
            return _lastServerInteractorClientId != NoInteractorClientId &&
                   ServerTryCompleteEligiblePlayer(
                       _lastServerInteractorClientId);
        }

        private bool ServerTryCompleteEligiblePlayer(ulong clientId)
        {
            if (!IsServer ||
                !TryGetEligibleServerPlayer(
                    clientId,
                    out _,
                    out var journal,
                    out var isGhost))
            {
                return false;
            }

            var roundState = NetworkRoundState.Current;
            var completed = journal.IsAssigned(MissionId) &&
                            !journal.IsCompleted(MissionId)
                ? roundState.ServerTryCompleteMission(
                    clientId,
                    MissionId,
                    out _)
                : !isGhost &&
                  roundState.ServerTryCompleteRecoveryMission(
                      clientId,
                      MissionId,
                      out _);
            if (completed)
            {
                _lastServerInteractorClientId = NoInteractorClientId;
            }

            return completed;
        }

        private bool TryGetEligibleServerPlayer(
            ulong clientId,
            out NetworkObject playerObject,
            out NetworkPlayerMissionJournal journal,
            out bool isGhost)
        {
            playerObject = null;
            journal = null;
            isGhost = false;
            var roundState = NetworkRoundState.Current;
            if (roundState == null || !roundState.AllowsMissionInteraction ||
                NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar) ||
                avatar.Role != PlayerRole.Survivor ||
                !client.PlayerObject.TryGetComponent(
                    out journal))
            {
                return false;
            }

            playerObject = client.PlayerObject;
            isGhost =
                playerObject.TryGetComponent<NetworkInfectionAuthority>(
                    out var infection) &&
                infection.LifeState == PlayerLifeState.DeadGhost;
            var hasPersonalMission =
                journal.IsAssigned(MissionId) &&
                !journal.IsCompleted(MissionId);
            var hasRecoveryMission =
                !isGhost && roundState.HasRecoveryMission(MissionId);
            return hasPersonalMission || hasRecoveryMission;
        }
    }
}
