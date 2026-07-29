using MonkeyLab.Core;
using MonkeyLab.Gameplay.Interaction;
using MonkeyLab.Gameplay.Noise;
using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>
    /// 퓨즈 미션 스테이션. M1 로컬 검증용이라 서버 권한 검사는 아직 없다.
    /// 점유·중단·실패 소음 규칙은 지금부터 지켜 M2에서 그대로 옮길 수 있게 한다.
    ///
    /// GDD §10.1, §10.2 / SDD §6.3, §8
    /// </summary>
    public sealed class FuseMissionStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private NoiseService _noiseService;
        [SerializeField] private Transform _interactionPoint;

        [Tooltip("정답 퓨즈 순서. 비우면 1..N 순서로 채운다")]
        [SerializeField] private int[] _correctOrder = { 2, 0, 3, 1 };

        [Tooltip("미션을 시작한 플레이어가 이 거리를 벗어나면 중단된다")]
        [SerializeField] private float _abandonDistance = 2.5f;

        private FusePuzzle _puzzle;
        private GameObject _occupant;

        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public string InteractionPrompt => IsComplete ? "복구 완료" : "퓨즈 교체";
        public bool IsOccupied => _occupant != null;
        public bool IsComplete { get; private set; }

        /// <summary>퓨즈를 하나 넣을 때마다 발생. (성공 여부, 진행도, 전체 개수)</summary>
        public event System.Action<bool, int, int> InsertResolved;

        /// <summary>미션이 완료됐을 때 발생.</summary>
        public event System.Action Completed;

        private void Awake()
        {
            if (_noiseService == null)
            {
                Debug.LogError($"[{nameof(FuseMissionStation)}] {nameof(_noiseService)} 미할당", this);
                enabled = false;
                return;
            }

            if (_correctOrder == null || _correctOrder.Length == 0)
            {
                _correctOrder = new[] { 0, 1, 2, 3 };
            }

            _puzzle = new FusePuzzle(_correctOrder);
        }

        private void Update()
        {
            if (_occupant == null)
            {
                return;
            }

            // 시작한 플레이어가 멀어지면 중단하고 진행을 초기화한다 (GDD §10.1).
            float sqr = (_occupant.transform.position - InteractionPoint.position).sqrMagnitude;
            if (sqr > _abandonDistance * _abandonDistance)
            {
                Abandon();
            }
        }

        public bool CanInteract(GameObject actor) => !IsComplete && (_occupant == null || _occupant == actor);

        public bool TryBeginInteract(GameObject actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }

            if (_occupant == null)
            {
                _occupant = actor;
                Debug.Log($"[Mission] 퓨즈 미션 시작. 다음 퓨즈: {_puzzle.ExpectedFuseId}");
                return true;
            }

            // 이미 점유 중이면 같은 플레이어의 재입력을 "다음 퓨즈 삽입"으로 처리한다.
            // 정식 UI가 붙기 전까지 쓰는 임시 조작이다.
            InsertNext();
            return true;
        }

        /// <summary>
        /// UI가 지정한 퓨즈를 넣는다. M1 임시 조작에서는 InsertNext가 대신 호출한다.
        /// </summary>
        public void Insert(int fuseId)
        {
            if (IsComplete || _occupant == null)
            {
                return;
            }

            bool ok = _puzzle.TryInsert(fuseId);
            InsertResolved?.Invoke(ok, _puzzle.Progress, _puzzle.SlotCount);

            if (!ok)
            {
                OnFailed();
                return;
            }

            if (_puzzle.IsComplete)
            {
                OnCompleted();
            }
        }

        /// <summary>
        /// M1 임시 조작: 80% 확률로 정답, 20%로 오답을 넣어 실패 소음을 확인한다.
        /// 정식 미션 UI가 생기면 제거한다.
        /// </summary>
        private void InsertNext()
        {
            int expected = _puzzle.ExpectedFuseId;
            bool deliberateMistake = Random.value < 0.2f;

            int fuseId = deliberateMistake ? expected + 1 : expected;
            Insert(fuseId);
        }

        private void OnFailed()
        {
            // 실패는 Medium 소음을 만든다 (balance §7.2).
            _noiseService.Emit(NoiseSourceType.MissionFailure, transform.position, NoiseIntensity.Medium);
            Debug.Log("[Mission] 퓨즈 실패 — 진행 초기화, Medium 소음 발생");
        }

        private void OnCompleted()
        {
            IsComplete = true;
            _occupant = null;
            Completed?.Invoke();
            Debug.Log("[Mission] 퓨즈 미션 완료");
        }

        private void Abandon()
        {
            Debug.Log("[Mission] 퓨즈 미션 중단 — 진행 초기화");
            _puzzle.Reset();
            _occupant = null;
        }
    }
}
