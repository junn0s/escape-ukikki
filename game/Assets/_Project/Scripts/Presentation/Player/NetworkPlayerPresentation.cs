using System;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.VFX;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.Player
{
    public sealed class NetworkPlayerPresentation : MonoBehaviour
    {
        private const float MissionActivityPulseSpeed = 8f;
        private const float MissionActivityPulseAmount = 0.06f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        [SerializeField] private NetworkPlayerAvatar _avatar;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField] private Behaviour[] _ownerOnlyBehaviours =
            Array.Empty<Behaviour>();
        [SerializeField] private Light2D[] _ownerOnlyVisionLights =
            Array.Empty<Light2D>();
        [SerializeField] private Renderer[] _ownerOnlyVisionRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private FlashlightController _flashlightController;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private NetworkPlayerMissionJournal _missionJournal;
        [SerializeField] private PlayerAimController _aim;
        [SerializeField] private MonsterTarget _monsterTarget;
        [SerializeField] private InfectionService _infectionService;
        [SerializeField] private AntidoteService _antidoteService;

        private MaterialPropertyBlock _propertyBlock;
        private GUIStyle _roleTitleStyle;
        private GUIStyle _roleBodyStyle;
        private PlayerRole _lastRevealedRole;
        private float _roleRevealUntil;
        private NetworkRoundState _roundState;
        private bool _isFlashlightSubscribed;

        public NetworkPlayerAvatar Avatar => _avatar;
        public GameObject VisualRoot => _visualRoot;
        public PlayerAimController Aim => _aim;
        public Rigidbody2D Body => _body;
        public PlayerInteractor Interactor => _interactor;
        public NetworkPlayerMissionJournal MissionJournal =>
            _missionJournal;
        public MonsterTarget MonsterTarget => _monsterTarget;
        public InfectionService InfectionService => _infectionService;
        public AntidoteService AntidoteService => _antidoteService;
        public bool IsOwnerOnlyBehaviour(Behaviour behaviour)
        {
            return behaviour != null &&
                   Array.IndexOf(_ownerOnlyBehaviours, behaviour) >= 0;
        }

        public void Configure(
            NetworkPlayerAvatar avatar,
            GameObject visualRoot,
            Renderer[] renderers,
            Behaviour[] ownerOnlyBehaviours,
            Rigidbody2D body = null,
            PlayerInteractor interactor = null,
            NetworkPlayerMissionJournal missionJournal = null,
            PlayerAimController aim = null,
            MonsterTarget monsterTarget = null,
            InfectionService infectionService = null,
            AntidoteService antidoteService = null,
            Light2D[] ownerOnlyVisionLights = null,
            Renderer[] ownerOnlyVisionRenderers = null,
            FlashlightController flashlightController = null)
        {
            UnbindFlashlightController();
            _avatar = avatar;
            _visualRoot = visualRoot;
            _renderers = renderers ?? Array.Empty<Renderer>();
            _ownerOnlyBehaviours =
                ownerOnlyBehaviours ?? Array.Empty<Behaviour>();
            _body = body;
            _interactor = interactor;
            _missionJournal = missionJournal;
            _aim = aim;
            _monsterTarget = monsterTarget;
            _infectionService = infectionService;
            _antidoteService = antidoteService;
            _ownerOnlyVisionLights =
                ownerOnlyVisionLights ?? Array.Empty<Light2D>();
            _ownerOnlyVisionRenderers =
                ownerOnlyVisionRenderers ?? Array.Empty<Renderer>();
            _flashlightController = flashlightController;
            if (isActiveAndEnabled)
            {
                BindFlashlightController();
            }
        }

        private void Awake()
        {
            if (_avatar == null || _visualRoot == null)
            {
                Debug.LogError(
                    "[NetworkPlayer] Presentation references are missing.",
                    this);
            }

        }

        private void OnEnable()
        {
            if (_avatar != null)
            {
                _avatar.StateChanged += Refresh;
            }

            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            NetworkGameplaySceneAdapter.CurrentChanged += Refresh;
            NetworkRoundState.CurrentChanged += HandleCurrentRoundChanged;
            BindFlashlightController();
            BindCurrentRound();
            Refresh();
        }

        private void OnDisable()
        {
            if (_avatar != null)
            {
                _avatar.StateChanged -= Refresh;
            }

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            NetworkGameplaySceneAdapter.CurrentChanged -= Refresh;
            NetworkRoundState.CurrentChanged -= HandleCurrentRoundChanged;
            UnbindFlashlightController();
            UnbindCurrentRound();
            ResetMissionActivityVisual();
            ApplyOwnerVision(false);
        }

        private void Update()
        {
            ApplyMissionActivityVisual();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            Refresh();
        }

        private void Refresh()
        {
            var isLaboratory =
                SceneManager.GetActiveScene().name ==
                NetworkPlayerAvatar.LaboratorySceneName;
            var isLocalGameplayPlayer =
                isLaboratory &&
                _avatar != null &&
                _avatar.IsSpawned &&
                _avatar.IsOwner;
            var canControlLocalPlayer =
                isLocalGameplayPlayer &&
                (_roundState == null || _roundState.AllowsPlayerControl);

            if (_visualRoot != null)
            {
                _visualRoot.SetActive(isLaboratory);
            }

            foreach (var behaviour in _ownerOnlyBehaviours)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = canControlLocalPlayer;
                }
            }

            ApplyOwnerVision(isLocalGameplayPlayer);
            ConfigurePhysicsAuthority(isLocalGameplayPlayer);
            ApplyColor();
            BindGameplayServices(isLocalGameplayPlayer);
            if (isLocalGameplayPlayer)
            {
                if (_flashlightController != null &&
                    _avatar.IsFlashlightEnabled !=
                    _flashlightController.IsFlashlightEnabled)
                {
                    _avatar.RequestSetFlashlight(
                        _flashlightController.IsFlashlightEnabled);
                }

                BindCamera();
                TryBeginRoleReveal();
            }
        }

        private void BindFlashlightController()
        {
            if (_isFlashlightSubscribed || _flashlightController == null)
            {
                return;
            }

            _flashlightController.FlashlightStateChanged +=
                HandleFlashlightStateChanged;
            _isFlashlightSubscribed = true;
        }

        private void UnbindFlashlightController()
        {
            if (!_isFlashlightSubscribed || _flashlightController == null)
            {
                return;
            }

            _flashlightController.FlashlightStateChanged -=
                HandleFlashlightStateChanged;
            _isFlashlightSubscribed = false;
        }

        private void HandleFlashlightStateChanged(bool isEnabled)
        {
            if (_avatar != null && _avatar.IsSpawned && _avatar.IsOwner)
            {
                _avatar.RequestSetFlashlight(isEnabled);
            }
        }

        private void ApplyOwnerVision(bool isLocalGameplayPlayer)
        {
            for (var index = 0;
                 index < _ownerOnlyVisionLights.Length;
                 index++)
            {
                var visionLight = _ownerOnlyVisionLights[index];
                if (visionLight != null)
                {
                    visionLight.enabled = isLocalGameplayPlayer;
                }
            }

            for (var index = 0;
                 index < _ownerOnlyVisionRenderers.Length;
                 index++)
            {
                var visionRenderer = _ownerOnlyVisionRenderers[index];
                if (visionRenderer != null)
                {
                    visionRenderer.enabled = isLocalGameplayPlayer;
                }
            }
        }

        private void HandleCurrentRoundChanged()
        {
            BindCurrentRound();
            Refresh();
        }

        private void BindCurrentRound()
        {
            UnbindCurrentRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += Refresh;
            }
        }

        private void UnbindCurrentRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= Refresh;
            }

            _roundState = null;
        }

        private void OnGUI()
        {
            if (_avatar == null || !_avatar.IsSpawned ||
                !_avatar.IsOwner ||
                SceneManager.GetActiveScene().name !=
                NetworkPlayerAvatar.LaboratorySceneName ||
                Time.unscaledTime >= _roleRevealUntil ||
                _lastRevealedRole == PlayerRole.Unassigned)
            {
                return;
            }

            const float width = 520f;
            const float height = 130f;
            EnsureRoleStyles();
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                42f,
                width,
                height);
            GUI.Box(rect, GUIContent.none);
            var title = _lastRevealedRole == PlayerRole.Villain
                ? "당신은 빌런입니다"
                : "당신은 생존자입니다";
            var body = _lastRevealedRole == PlayerRole.Villain
                ? "정체를 숨기고 탈출을 방해하세요"
                : "협력해서 연구소를 탈출하세요";
            GUI.Label(
                new Rect(rect.x, rect.y + 16f, rect.width, 44f),
                title,
                _roleTitleStyle);
            GUI.Label(
                new Rect(rect.x, rect.y + 68f, rect.width, 34f),
                body,
                _roleBodyStyle);
        }

        private void EnsureRoleStyles()
        {
            if (_roleTitleStyle != null && _roleBodyStyle != null)
            {
                return;
            }

            _roleTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
            _roleTitleStyle.normal.textColor = Color.white;
            _roleBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            _roleBodyStyle.normal.textColor =
                new Color(0.82f, 0.9f, 0.96f);
        }

        private void BindCamera()
        {
            var mainCamera = UnityEngine.Camera.main;
            _aim?.SetWorldCamera(mainCamera);
            var topDownCamera = mainCamera != null
                ? mainCamera.GetComponent<TopDownCamera>()
                : null;
            if (topDownCamera != null)
            {
                topDownCamera.Configure(transform, 9f, 0.12f);
            }
        }

        private void BindGameplayServices(bool isLocalGameplayPlayer)
        {
            NetworkGameplaySceneAdapter.Current?.BindNetworkPlayer(
                _monsterTarget,
                _infectionService,
                _antidoteService,
                _interactor,
                _interactor != null
                    ? _interactor.GetComponent<PlayerInputReader>()
                    : null,
                isLocalGameplayPlayer);
        }

        private void ConfigurePhysicsAuthority(
            bool isLocalGameplayPlayer)
        {
            if (_body == null)
            {
                return;
            }

            var desiredBodyType = isLocalGameplayPlayer
                ? RigidbodyType2D.Dynamic
                : RigidbodyType2D.Kinematic;
            if (_body.bodyType == desiredBodyType)
            {
                return;
            }

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.bodyType = desiredBodyType;
        }

        private void ApplyMissionActivityVisual()
        {
            if (_visualRoot == null)
            {
                return;
            }

            if (_missionJournal == null ||
                !_missionJournal.IsPerformingMission)
            {
                ResetMissionActivityVisual();
                return;
            }

            var pulse =
                1f +
                (Mathf.Sin(
                     Time.unscaledTime * MissionActivityPulseSpeed) *
                 0.5f + 0.5f) *
                MissionActivityPulseAmount;
            _visualRoot.transform.localScale =
                new Vector3(pulse, pulse, 1f);
        }

        private void ResetMissionActivityVisual()
        {
            if (_visualRoot != null)
            {
                _visualRoot.transform.localScale = Vector3.one;
            }
        }

        private void TryBeginRoleReveal()
        {
            if (!_avatar.HasAssignedRole ||
                _avatar.Role == _lastRevealedRole)
            {
                return;
            }

            _lastRevealedRole = _avatar.Role;
            _roleRevealUntil = Time.unscaledTime + 5f;
        }

        private void ApplyColor()
        {
            var color = CreateColor(
                _avatar != null
                    ? _avatar.Color
                    : LobbyPlayerColor.Blue);
            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                if (targetRenderer is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.color = color;
                    continue;
                }

                _propertyBlock ??= new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static Color CreateColor(LobbyPlayerColor color)
        {
            return color switch
            {
                LobbyPlayerColor.Blue => new Color(0.15f, 0.55f, 1f),
                LobbyPlayerColor.Yellow => new Color(1f, 0.82f, 0.12f),
                LobbyPlayerColor.Green => new Color(0.15f, 0.78f, 0.35f),
                LobbyPlayerColor.Red => new Color(0.92f, 0.18f, 0.18f),
                LobbyPlayerColor.Purple => new Color(0.62f, 0.3f, 0.9f),
                LobbyPlayerColor.Orange => new Color(1f, 0.48f, 0.12f),
                _ => Color.white
            };
        }
    }
}
