using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 라운드가 끝난 뒤 결과 화면용 정보를 전원에게 공개한다(GDD §20).
    /// 라운드 중에는 목록이 비어 있다. 역할·개인 미션 수·강화 단계는 모두
    /// 라운드 중 비공개 정보이므로, 종료 시점에 서버가 한 번만 채운다.
    ///
    /// 사건 타임라인(감염·치료·스피커 시각)은 컷라인(로드맵 §14의 3번,
    /// mvp-scope §9의 7번)에 따라 MVP에서 제외했다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRoundSummaryAuthority : NetworkBehaviour
    {
        public static NetworkRoundSummaryAuthority Current { get; private set; }
        public static event Action CurrentChanged;

        private readonly NetworkList<RoundSummaryEntry> _entries = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _scentLevel = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _populationLevel = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _toxicityLevel = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _inspectedClueCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _activeClueCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action SummaryChanged;

        public int EntryCount => _entries.Count;
        public bool HasSummary => _entries.Count > 0;
        public int ScentLevel => _scentLevel.Value;
        public int PopulationLevel => _populationLevel.Value;
        public int ToxicityLevel => _toxicityLevel.Value;

        /// <summary>조사된 단서 수다. 놓친 단서는 전체에서 뺀 값이다(GDD §20).</summary>
        public int InspectedClueCount => _inspectedClueCount.Value;
        public int ActiveClueCount => _activeClueCount.Value;
        public int MissedClueCount =>
            Mathf.Max(0, _activeClueCount.Value - _inspectedClueCount.Value);

        public RoundSummaryEntry GetEntry(int index)
        {
            return _entries[index];
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            CurrentChanged?.Invoke();
            _entries.OnListChanged += HandleEntriesChanged;
            _scentLevel.OnValueChanged += HandleLevelChanged;
            _populationLevel.OnValueChanged += HandleLevelChanged;
            _toxicityLevel.OnValueChanged += HandleLevelChanged;
        }

        public override void OnNetworkDespawn()
        {
            _entries.OnListChanged -= HandleEntriesChanged;
            _scentLevel.OnValueChanged -= HandleLevelChanged;
            _populationLevel.OnValueChanged -= HandleLevelChanged;
            _toxicityLevel.OnValueChanged -= HandleLevelChanged;
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        /// <summary>
        /// 라운드 종료 시 서버가 한 번만 호출한다.
        /// 이미 채워져 있으면 다시 쓰지 않아 결과가 흔들리지 않는다.
        /// </summary>
        public void ServerPublishSummary()
        {
            if (!IsServer || NetworkManager == null || _entries.Count > 0)
            {
                return;
            }

            var upgradeAuthority = NetworkVillainUpgradeAuthority.Current;
            if (upgradeAuthority != null)
            {
                _scentLevel.Value =
                    upgradeAuthority.ServerGetLevel(UpgradeAxis.Scent);
                _populationLevel.Value =
                    upgradeAuthority.ServerGetLevel(UpgradeAxis.Population);
                _toxicityLevel.Value =
                    upgradeAuthority.ServerGetLevel(UpgradeAxis.Toxicity);
            }

            var clueAuthority = NetworkClueAuthority.Current;
            if (clueAuthority != null)
            {
                _activeClueCount.Value = clueAuthority.ActiveClueCount;
                _inspectedClueCount.Value =
                    clueAuthority.ServerCountInspectedClues();
            }

            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value?.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar))
                {
                    continue;
                }

                var lifeState =
                    playerObject.TryGetComponent<NetworkInfectionAuthority>(
                        out var infection)
                        ? infection.LifeState
                        : PlayerLifeState.AliveHealthy;
                var journal = playerObject
                    .GetComponent<NetworkPlayerMissionJournal>();

                _entries.Add(new RoundSummaryEntry
                {
                    ClientId = pair.Key,
                    SlotIndex = avatar.SlotIndex,
                    Color = avatar.Color,
                    Role = avatar.Role,
                    LifeState = lifeState,
                    CompletedMissionCount = journal != null
                        ? (byte)Mathf.Clamp(journal.CompletedCount, 0, 255)
                        : (byte)0,
                    AssignedMissionCount = journal != null
                        ? (byte)Mathf.Clamp(journal.AssignedCount, 0, 255)
                        : (byte)0
                });
            }

            Debug.Log(
                $"[Round] Published the round summary for {_entries.Count} players.",
                this);
        }

        private void HandleEntriesChanged(
            NetworkListEvent<RoundSummaryEntry> changeEvent)
        {
            SummaryChanged?.Invoke();
        }

        private void HandleLevelChanged(int previousValue, int currentValue)
        {
            SummaryChanged?.Invoke();
        }
    }
}
