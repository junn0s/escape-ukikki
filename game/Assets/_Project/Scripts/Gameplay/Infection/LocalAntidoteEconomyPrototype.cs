using System;
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
        [SerializeField] private AntidoteTerminalPrototype[] _terminals;
        [SerializeField]
        private AntidoteFabricatorPrototype[] _fabricators;

        private readonly AntidoteCodeSession _codeSession = new();

        public bool IsInitialized { get; private set; }
        public int TerminalCount => _terminals?.Length ?? 0;
        public int FabricatorCount => _fabricators?.Length ?? 0;

        public void Configure(
            AntidoteService antidoteService,
            InfectionService infectionService,
            AntidoteTerminalPrototype[] terminals,
            AntidoteFabricatorPrototype[] fabricators)
        {
            Deactivate();
            _antidoteService = antidoteService;
            _infectionService = infectionService;
            _terminals = terminals;
            _fabricators = fabricators;
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

            foreach (var terminal in _terminals)
            {
                terminal.SetInteractionAuthority(
                    this,
                    CanLocalPlayerInteract,
                    CreateTerminalInteraction(terminal, seed));
            }

            foreach (var fabricator in _fabricators)
            {
                fabricator.SetInteractionAuthority(
                    this,
                    CanLocalPlayerInteract,
                    CreateFabricatorInteraction(fabricator),
                    CreateCodeSubmitInteraction(fabricator));
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

        private Action<GameObject> CreateTerminalInteraction(
            AntidoteTerminalPrototype terminal,
            int seed)
        {
            return interactor => HandleTerminalInteraction(interactor, terminal, seed);
        }

        public void HandleTerminalInteraction(
            GameObject interactor,
            AntidoteTerminalPrototype terminal,
            int seed)
        {
            if (!CanLocalPlayerInteract(interactor) || terminal == null)
            {
                return;
            }

            var code = AntidoteCodeGenerator.Generate(
                _antidoteService.Config.CodeLength,
                seed ^ (int)Time.frameCount);
            _codeSession.IssueCode(code);
            _antidoteService.ApplyAuthoritativeCodeState(true, code);
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
            if (state == FabricatorState.Ready)
            {
                var collectRejection = AntidoteCraftRules.ValidateCollect(
                    _infectionService.State,
                    state,
                    _antidoteService.CarriedCount,
                    _antidoteService.Config.MaxCarryCount,
                    allowsMissionInteraction: true,
                    isWithinRange: true);
                if (collectRejection != AntidoteRejectionReason.None)
                {
                    fabricator.ApplyInteractionFeedback(collectRejection);
                    return;
                }

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

            var startRejection = AntidoteCraftRules.ValidateCraftStart(
                _infectionService.State,
                _antidoteService.HasValidCode,
                state,
                allowsMissionInteraction: true,
                isWithinRange: true);
            if (startRejection != AntidoteRejectionReason.None)
            {
                fabricator.ApplyInteractionFeedback(startRejection);
                return;
            }

            fabricator.Fabricator.TryBeginCodeEntry(LocalPlayerId);
        }

        private Action<GameObject, string> CreateCodeSubmitInteraction(
            AntidoteFabricatorPrototype fabricator)
        {
            return (interactor, attempt) =>
                HandleCodeSubmit(interactor, fabricator, attempt);
        }

        public void HandleCodeSubmit(
            GameObject interactor,
            AntidoteFabricatorPrototype fabricator,
            string attempt)
        {
            if (!CanLocalPlayerInteract(interactor) || fabricator == null ||
                fabricator.Fabricator.State != FabricatorState.AwaitingCode)
            {
                return;
            }

            if (_codeSession.TrySubmit(attempt, _antidoteService.Config.MaxCodeAttempts))
            {
                fabricator.Fabricator.TryBeginSynthesis(
                    _antidoteService.Config.SynthesisSeconds);
                return;
            }

            if (!_codeSession.HasValidCode)
            {
                _antidoteService.ApplyAuthoritativeCodeState(false, string.Empty);
                fabricator.Fabricator.Reset();
                fabricator.ApplyInteractionFeedback(
                    AntidoteRejectionReason.CodeInvalidated);
                return;
            }

            fabricator.ApplyInteractionFeedback(AntidoteRejectionReason.WrongCode);
        }

        private bool HasRequiredReferences()
        {
            return _antidoteService != null && _infectionService != null &&
                   _antidoteService.Config != null &&
                   IsComplete(_terminals) && IsComplete(_fabricators);
        }

        private bool HasAnotherAuthority()
        {
            foreach (var terminal in _terminals)
            {
                if (terminal.InteractionAuthorityOwner != null &&
                    !ReferenceEquals(terminal.InteractionAuthorityOwner, this))
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

            return false;
        }

        private void Deactivate()
        {
            if (_terminals != null)
            {
                foreach (var terminal in _terminals)
                {
                    terminal?.ClearInteractionAuthority(this);
                }
            }

            if (_fabricators != null)
            {
                foreach (var fabricator in _fabricators)
                {
                    fabricator?.ClearInteractionAuthority(this);
                }
            }

            _codeSession.Invalidate();
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
