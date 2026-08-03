using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Villain;
using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 10_Laboratory 씬을 직접 실행할 때 해독제 흐름을 서버 대신 판정한다.
    /// 네트워크 플레이에서는 각 NetworkBehaviour가 상호작용 권위를 덮어쓰며,
    /// 이 컴포넌트가 붙은 로컬 테스트 플레이어는 비활성화된다.
    /// </summary>
    public sealed class LocalAntidoteEconomyPrototype : MonoBehaviour
    {
        private const ulong LocalPlayerId = 0UL;

        [SerializeField] private AntidoteService _antidoteService;
        [SerializeField] private InfectionService _infectionService;
        [SerializeField] private RecipeNotePrototype[] _recipeNotes;
        [SerializeField]
        private AntidoteFabricatorPrototype[] _fabricators;
        [SerializeField] private AntidoteStorageLocker[] _lockers;

        private readonly Dictionary<ulong, int> _recipeAssignment = new();

        public bool IsInitialized { get; private set; }
        public int AssignedCandidateIndex { get; private set; } = -1;
        public int RecipeNoteCount => _recipeNotes?.Length ?? 0;
        public int FabricatorCount => _fabricators?.Length ?? 0;
        public int LockerCount => _lockers?.Length ?? 0;

        public void Configure(
            AntidoteService antidoteService,
            InfectionService infectionService,
            RecipeNotePrototype[] recipeNotes,
            AntidoteFabricatorPrototype[] fabricators,
            AntidoteStorageLocker[] lockers)
        {
            Deactivate();
            _antidoteService = antidoteService;
            _infectionService = infectionService;
            _recipeNotes = recipeNotes;
            _fabricators = fabricators;
            _lockers = lockers;
        }

        public bool Initialize(int seed)
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!HasRequiredReferences() || HasAnotherAuthority())
            {
                return false;
            }

            if (!RecipeAssignmentService.TryAssign(
                    new[] { LocalPlayerId },
                    _recipeNotes.Length,
                    seed,
                    _recipeAssignment) ||
                !_recipeAssignment.TryGetValue(
                    LocalPlayerId,
                    out var assignedCandidateIndex))
            {
                return false;
            }

            AssignedCandidateIndex = assignedCandidateIndex;
            foreach (var note in _recipeNotes)
            {
                note.SetInteractionAuthority(
                    this,
                    CanLocalPlayerInteract,
                    CreateRecipeInteraction(note));
            }

            foreach (var fabricator in _fabricators)
            {
                fabricator.SetInteractionAuthority(
                    this,
                    CanLocalPlayerInteract,
                    CreateFabricatorInteraction(fabricator));
            }

            foreach (var locker in _lockers)
            {
                locker.SetInteractionAuthority(
                    this,
                    CanLocalPlayerInteract,
                    CreateLockerInteraction(locker));
            }

            IsInitialized = true;
            return true;
        }

        private void OnEnable()
        {
            if (UnityEngine.Application.isPlaying)
            {
                Initialize(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            }
        }

        private void Start()
        {
            if (!IsInitialized)
            {
                Initialize(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void Update()
        {
            if (!IsInitialized || _fabricators == null)
            {
                return;
            }

            foreach (var fabricator in _fabricators)
            {
                if (fabricator != null &&
                    ReferenceEquals(
                        fabricator.InteractionAuthorityOwner,
                        this))
                {
                    fabricator.Fabricator.Tick(Time.deltaTime);
                }
            }
        }

        private bool CanLocalPlayerInteract(GameObject interactor)
        {
            return IsInitialized && interactor != null &&
                   _antidoteService != null &&
                   interactor == _antidoteService.gameObject &&
                   _infectionService.State != PlayerLifeState.DeadGhost;
        }

        private Action<GameObject> CreateRecipeInteraction(
            RecipeNotePrototype note)
        {
            return interactor => HandleRecipeInteraction(interactor, note);
        }

        public void HandleRecipeInteraction(
            GameObject interactor,
            RecipeNotePrototype note)
        {
            if (!CanLocalPlayerInteract(interactor) || note == null ||
                note.CandidateIndex != AssignedCandidateIndex)
            {
                return;
            }

            _antidoteService.ApplyAuthoritativeRecipeState(true);
            note.ApplyLocalDiscovery();
        }

        private Action<GameObject> CreateFabricatorInteraction(
            AntidoteFabricatorPrototype fabricator)
        {
            return interactor => HandleFabricatorInteraction(
                interactor,
                fabricator);
        }

        public void HandleFabricatorInteraction(
            GameObject interactor,
            AntidoteFabricatorPrototype fabricator)
        {
            if (!CanLocalPlayerInteract(interactor) || fabricator == null)
            {
                return;
            }

            var state = fabricator.Fabricator.State;
            var rejection = state == FabricatorState.Ready
                ? AntidoteCraftRules.ValidateCollect(
                    _infectionService.State,
                    state,
                    _antidoteService.CarriedCount,
                    _antidoteService.Config.MaxCarryCount,
                    allowsMissionInteraction: true,
                    isWithinRange: true)
                : AntidoteCraftRules.ValidateCraftStart(
                    PlayerRole.Survivor,
                    _infectionService.State,
                    _antidoteService.HasRecipe,
                    state,
                    allowsMissionInteraction: true,
                    isWithinRange: true);
            if (rejection != AntidoteRejectionReason.None)
            {
                fabricator.ApplyInteractionFeedback(rejection);
                return;
            }

            if (state == FabricatorState.Ready)
            {
                if (!_antidoteService.TryAddAntidote())
                {
                    fabricator.ApplyInteractionFeedback(
                        AntidoteRejectionReason.CarryLimitReached);
                    return;
                }

                if (!fabricator.Fabricator.TryCollect())
                {
                    _antidoteService.TryRemoveAntidote();
                    fabricator.ApplyInteractionFeedback(
                        AntidoteRejectionReason.NothingToCollect);
                }

                return;
            }

            if (!fabricator.Fabricator.TryBeginCraft(
                    LocalPlayerId,
                    _antidoteService.Config.CraftDurationSeconds))
            {
                fabricator.ApplyInteractionFeedback(
                    AntidoteRejectionReason.FabricatorBusy);
            }
        }

        private Action<GameObject> CreateLockerInteraction(
            AntidoteStorageLocker locker)
        {
            return interactor => HandleLockerInteraction(interactor, locker);
        }

        public void HandleLockerInteraction(
            GameObject interactor,
            AntidoteStorageLocker locker)
        {
            if (!CanLocalPlayerInteract(interactor) || locker == null)
            {
                return;
            }

            var isStoring = _antidoteService.CarriedCount > 0;
            var rejection = isStoring
                ? AntidoteCraftRules.ValidateStore(
                    _infectionService.State,
                    _antidoteService.CarriedCount,
                    locker.StoredCount,
                    locker.SlotCapacity,
                    allowsMissionInteraction: true,
                    isWithinRange: true)
                : AntidoteCraftRules.ValidateWithdraw(
                    _infectionService.State,
                    _antidoteService.CarriedCount,
                    _antidoteService.Config.MaxCarryCount,
                    locker.StoredCount,
                    allowsMissionInteraction: true,
                    isWithinRange: true);
            if (rejection != AntidoteRejectionReason.None)
            {
                locker.ApplyInteractionFeedback(rejection);
                return;
            }

            if (isStoring)
            {
                if (_antidoteService.TryRemoveAntidote())
                {
                    locker.ApplyAuthoritativeStoredCount(
                        locker.StoredCount + 1);
                }

                return;
            }

            if (_antidoteService.TryAddAntidote())
            {
                locker.ApplyAuthoritativeStoredCount(
                    locker.StoredCount - 1);
            }
        }

        private bool HasRequiredReferences()
        {
            return _antidoteService != null && _infectionService != null &&
                   _antidoteService.Config != null &&
                   IsComplete(_recipeNotes) &&
                   IsComplete(_fabricators) && IsComplete(_lockers);
        }

        private bool HasAnotherAuthority()
        {
            foreach (var note in _recipeNotes)
            {
                if (note.InteractionAuthorityOwner != null &&
                    !ReferenceEquals(note.InteractionAuthorityOwner, this))
                {
                    return true;
                }
            }

            foreach (var fabricator in _fabricators)
            {
                if (fabricator.InteractionAuthorityOwner != null &&
                    !ReferenceEquals(
                        fabricator.InteractionAuthorityOwner,
                        this))
                {
                    return true;
                }
            }

            foreach (var locker in _lockers)
            {
                if (locker.InteractionAuthorityOwner != null &&
                    !ReferenceEquals(locker.InteractionAuthorityOwner, this))
                {
                    return true;
                }
            }

            return false;
        }

        private void Deactivate()
        {
            if (_recipeNotes != null)
            {
                foreach (var note in _recipeNotes)
                {
                    note?.ClearInteractionAuthority(this);
                }
            }

            if (_fabricators != null)
            {
                foreach (var fabricator in _fabricators)
                {
                    fabricator?.ClearInteractionAuthority(this);
                }
            }

            if (_lockers != null)
            {
                foreach (var locker in _lockers)
                {
                    locker?.ClearInteractionAuthority(this);
                }
            }

            _recipeAssignment.Clear();
            AssignedCandidateIndex = -1;
            IsInitialized = false;
        }

        private static bool IsComplete<T>(T[] items)
            where T : UnityEngine.Object
        {
            return items != null && items.Length > 0 &&
                   Array.TrueForAll(items, item => item != null);
        }
    }
}
