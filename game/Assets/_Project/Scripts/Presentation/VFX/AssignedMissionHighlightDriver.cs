using System.Collections.Generic;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 자기에게 배정된 미션 설치물만 테두리로 강조한다(SDD §7.2).
    ///
    /// 배정은 사람마다 다르므로 반드시 소유자 인스턴스에서만 돈다. 원격 플레이어가
    /// 이걸 돌리면 남의 배정이 내 화면에 표시된다.
    /// </summary>
    public sealed class AssignedMissionHighlightDriver : MonoBehaviour
    {
        private readonly Dictionary<InteractableHighlight, ulong> _missionIds =
            new();

        private NetworkPlayerMissionJournal _journal;
        private int _knownHighlightCount = -1;
        private bool _isDirty;

        /// <summary>자기 플레이어의 미션 일지를 연결한다. 잃으면 null을 넣는다.</summary>
        public void BindJournal(NetworkPlayerMissionJournal journal)
        {
            if (_journal == journal)
            {
                return;
            }

            if (_journal != null)
            {
                _journal.Changed -= HandleJournalChanged;
            }

            _journal = journal;
            if (_journal != null)
            {
                _journal.Changed += HandleJournalChanged;
            }

            _isDirty = true;
        }

        private void OnDisable()
        {
            if (_journal != null)
            {
                _journal.Changed -= HandleJournalChanged;
                _journal = null;
            }

            // 소유권을 잃거나 화면을 나갈 때 미션 강조 판정을 남기지 않는다.
            foreach (var pair in _missionIds)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetMissionAssignment(false, false);
                }
            }

            _missionIds.Clear();
            _knownHighlightCount = -1;
        }

        private void HandleJournalChanged()
        {
            _isDirty = true;
        }

        private void LateUpdate()
        {
            // 미션 스테이션은 라운드 중 늘거나 줄지 않지만, 네트워크 스폰 시점이
            // 소유권 획득보다 늦을 수 있어 개수가 달라지면 다시 모은다.
            var highlights = InteractableHighlight.All;
            if (_knownHighlightCount != highlights.Count)
            {
                CollectMissionStations(highlights);
            }

            if (!_isDirty)
            {
                return;
            }

            _isDirty = false;
            foreach (var pair in _missionIds)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.SetMissionAssignment(
                    true,
                    _journal != null && _journal.IsAssigned(pair.Value));
            }
        }

        private void CollectMissionStations(
            IReadOnlyList<InteractableHighlight> highlights)
        {
            _knownHighlightCount = highlights.Count;
            _missionIds.Clear();
            for (var index = 0; index < highlights.Count; index++)
            {
                var highlight = highlights[index];
                if (highlight == null ||
                    !highlight.TryGetComponent<NetworkSurvivorMissionAuthority>(
                        out var mission) ||
                    !highlight.TryGetComponent<NetworkObject>(
                        out var networkObject) ||
                    !networkObject.IsSpawned)
                {
                    continue;
                }

                _missionIds[highlight] = mission.MissionId;
            }

            _isDirty = true;
        }
    }
}
