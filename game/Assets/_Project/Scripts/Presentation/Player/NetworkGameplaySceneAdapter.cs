using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Presentation.UI;
using MonkeyLab.Presentation.VFX;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.Player
{
    public sealed class NetworkGameplaySceneAdapter : MonoBehaviour
    {
        [SerializeField] private GameObject _localPrototypeRoot;
        [SerializeField] private GameObject _localPlayer;
        [SerializeField] private MonsterTierRuntime _monsterTierRuntime;
        [SerializeField] private InfectionHudView _infectionHud;
        [SerializeField] private MonsterBiteAlertView _monsterBiteAlert;
        [SerializeField] private InteractionPromptView _interactionPrompt;
        [SerializeField] private MissionJournalView _missionJournal;
        [SerializeField] private GameplayFeelView _gameplayFeel;

        // 해독제 화면은 자기 AntidoteService의 상태 변화로 열린다. 씬을 만들 때 받은
        // 프로토타입 서비스에 묶여 있으면 네트워크 모드에서 열리지 않으므로,
        // 소유권을 얻을 때 자기 서비스로 다시 연결한다(GDD §14).
        [SerializeField]
        private AntidoteTerminalView[] _antidoteTerminals =
            Array.Empty<AntidoteTerminalView>();
        [SerializeField]
        private AntidoteKeypadView[] _antidoteKeypads =
            Array.Empty<AntidoteKeypadView>();

        public static event Action CurrentChanged;
        public static NetworkGameplaySceneAdapter Current { get; private set; }
        public GameObject LocalPrototypeRoot => _localPrototypeRoot;
        public GameObject LocalPlayer => _localPlayer;
        public MonsterTierRuntime MonsterTierRuntime => _monsterTierRuntime;
        public InfectionHudView InfectionHud => _infectionHud;
        public MonsterBiteAlertView MonsterBiteAlert => _monsterBiteAlert;
        public InteractionPromptView InteractionPrompt =>
            _interactionPrompt;
        public MissionJournalView MissionJournal => _missionJournal;
        public GameplayFeelView GameplayFeel => _gameplayFeel;
        public int AntidoteTerminalCount => _antidoteTerminals?.Length ?? 0;
        public int AntidoteKeypadCount => _antidoteKeypads?.Length ?? 0;
        public bool IsNetworkMode { get; private set; }

        public void Configure(
            GameObject localPrototypeRoot,
            GameObject localPlayer,
            MonsterTierRuntime monsterTierRuntime = null,
            InfectionHudView infectionHud = null,
            MonsterBiteAlertView monsterBiteAlert = null,
            InteractionPromptView interactionPrompt = null,
            MissionJournalView missionJournal = null,
            GameplayFeelView gameplayFeel = null,
            AntidoteTerminalView[] antidoteTerminals = null,
            AntidoteKeypadView[] antidoteKeypads = null)
        {
            _localPrototypeRoot = localPrototypeRoot;
            _localPlayer = localPlayer;
            _monsterTierRuntime = monsterTierRuntime;
            _infectionHud = infectionHud;
            _monsterBiteAlert = monsterBiteAlert;
            _interactionPrompt = interactionPrompt;
            _missionJournal = missionJournal;
            _gameplayFeel = gameplayFeel;
            _antidoteTerminals =
                antidoteTerminals ?? Array.Empty<AntidoteTerminalView>();
            _antidoteKeypads =
                antidoteKeypads ?? Array.Empty<AntidoteKeypadView>();
        }

        private void Awake()
        {
            MixedPerspectiveSceneStyler.ApplyTo(gameObject.scene);

            if (_localPrototypeRoot == null || _localPlayer == null)
            {
                Debug.LogError(
                    "[NetworkPlayer] Gameplay root or local player is missing.",
                    this);
            }
        }

        private void OnEnable()
        {
            Current = this;
            CurrentChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }
        }

        private void Start()
        {
            ApplyMode(
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening);
        }

        public void ApplyMode(bool isNetworkMode)
        {
            IsNetworkMode = isNetworkMode;
            if (_localPrototypeRoot != null)
            {
                _localPrototypeRoot.SetActive(true);
            }

            if (_localPlayer != null)
            {
                _localPlayer.SetActive(!IsNetworkMode);
            }

            if (!IsNetworkMode && _localPlayer != null)
            {
                // Default 씬 직접 실행은 로컬 연습 모드지만 Tab 지도만큼은
                // 네트워크 모드와 동일하게 사용할 수 있어야 한다.
                _missionJournal?.BindInput(
                    _localPlayer.GetComponent<PlayerInputReader>());
            }
        }

        public bool BindNetworkPlayer(
            MonsterTarget target,
            InfectionService infectionService,
            AntidoteService antidoteService,
            PlayerInteractor interactor,
            PlayerInputReader input,
            bool bindLocalFeedback)
        {
            // 지도 입력은 감염·괴물 런타임 바인딩보다 독립적이다. 씬 참조가
            // 아직 준비되지 않았더라도 로컬 소유자의 Tab은 먼저 연결한다.
            if (bindLocalFeedback)
            {
                _missionJournal?.BindInput(input);
            }

            if (target == null || infectionService == null ||
                _monsterTierRuntime == null)
            {
                return false;
            }

            infectionService.Configure(target, _monsterTierRuntime);
            if (bindLocalFeedback)
            {
                _infectionHud?.Configure(infectionService, antidoteService);
                _monsterBiteAlert?.Configure(target);
                _interactionPrompt?.Configure(interactor);
                _gameplayFeel?.BindLocalPlayer(
                    target.transform,
                    target,
                    interactor,
                    input);
                BindAntidoteViews(antidoteService);
            }

            return true;
        }

        private void BindAntidoteViews(AntidoteService antidoteService)
        {
            if (antidoteService == null)
            {
                return;
            }

            foreach (var terminal in _antidoteTerminals)
            {
                terminal?.BindAntidoteService(antidoteService);
            }

            foreach (var keypad in _antidoteKeypads)
            {
                keypad?.BindAntidoteService(antidoteService);
            }
        }
    }
}
