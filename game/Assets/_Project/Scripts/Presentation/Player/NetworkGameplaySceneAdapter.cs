using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Presentation.UI;
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

        public static event Action CurrentChanged;
        public static NetworkGameplaySceneAdapter Current { get; private set; }
        public GameObject LocalPrototypeRoot => _localPrototypeRoot;
        public GameObject LocalPlayer => _localPlayer;
        public MonsterTierRuntime MonsterTierRuntime => _monsterTierRuntime;
        public InfectionHudView InfectionHud => _infectionHud;
        public MonsterBiteAlertView MonsterBiteAlert => _monsterBiteAlert;
        public InteractionPromptView InteractionPrompt =>
            _interactionPrompt;
        public bool IsNetworkMode { get; private set; }

        public void Configure(
            GameObject localPrototypeRoot,
            GameObject localPlayer,
            MonsterTierRuntime monsterTierRuntime = null,
            InfectionHudView infectionHud = null,
            MonsterBiteAlertView monsterBiteAlert = null,
            InteractionPromptView interactionPrompt = null)
        {
            _localPrototypeRoot = localPrototypeRoot;
            _localPlayer = localPlayer;
            _monsterTierRuntime = monsterTierRuntime;
            _infectionHud = infectionHud;
            _monsterBiteAlert = monsterBiteAlert;
            _interactionPrompt = interactionPrompt;
        }

        private void Awake()
        {
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
        }

        public bool BindNetworkPlayer(
            MonsterTarget target,
            InfectionService infectionService,
            AntidoteService antidoteService,
            PlayerInteractor interactor,
            bool bindLocalFeedback)
        {
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
            }

            return true;
        }
    }
}
