using System;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Monsters;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using MonkeyLab.Presentation.Camera;
using MonkeyLab.Presentation.UI;
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
        private const float FallbackRoleRevealDurationSeconds = 7f;
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
        private GUIStyle _roleEyebrowStyle;
        private GUIStyle _roleTitleStyle;
        private GUIStyle _roleBodyStyle;
        private GUIStyle _roleHintStyle;
        private GUIStyle _roleButtonStyle;
        private PlayerRole _lastRevealedRole;
        private float _roleRevealStartedAt;
        private float _roleRevealUntil;
        private float _roleRevealDismissAt;
        private NetworkRoundState _roundState;
        private RoundPhase? _lastObservedRoundPhase;
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
            RefreshRoleRevealCycle();
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

            // 벽 차폐 후레시는 런타임 메시를 만들기 때문에 직렬화된 렌더러
            // 배열에 포함되지 않는다. 소유자 화면에만 보이도록 별도로 전달한다.
            if (_flashlightController != null)
            {
                _flashlightController.SetOwnerVisionVisible(
                    isLocalGameplayPlayer);
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
            _lastObservedRoundPhase = null;
            _lastRevealedRole = PlayerRole.Unassigned;
            _roleRevealStartedAt = 0f;
            _roleRevealUntil = 0f;
            _roleRevealDismissAt = 0f;
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

        private void RefreshRoleRevealCycle()
        {
            if (_roundState == null)
            {
                _lastObservedRoundPhase = null;
                return;
            }

            var currentPhase = _roundState.Phase;
            if (currentPhase == RoundPhase.RoleReveal &&
                _lastObservedRoundPhase != RoundPhase.RoleReveal)
            {
                // 같은 플레이어가 다음 판에도 같은 역할을 받더라도
                // 새 라운드의 역할 공개는 반드시 다시 시작한다.
                _lastRevealedRole = PlayerRole.Unassigned;
                _roleRevealStartedAt = 0f;
                _roleRevealUntil = 0f;
                _roleRevealDismissAt = 0f;
            }

            _lastObservedRoundPhase = currentPhase;
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

            EnsureRoleStyles();
            GUI.depth = -10000;

            var isVillain = _lastRevealedRole == PlayerRole.Villain;
            var elapsed = Mathf.Max(
                0f,
                Time.unscaledTime - _roleRevealStartedAt);
            var remaining = Mathf.Max(
                0f,
                _roleRevealUntil - Time.unscaledTime);
            var introProgress = Mathf.Clamp01(elapsed / 0.42f);
            var fade = Mathf.Min(
                Mathf.Clamp01(elapsed / 0.18f),
                Mathf.Clamp01(remaining / 0.32f));
            var panelScale = Mathf.Lerp(
                0.82f,
                1f,
                EaseOutBack(introProgress));
            var accent = isVillain
                ? new Color(0.92f, 0.18f, 0.2f, 1f)
                : new Color(0.2f, 0.72f, 0.95f, 1f);
            var safeArea = Screen.safeArea;
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.015f, 0.02f, 0.03f, 0.97f * fade));

            var width = Mathf.Min(900f, safeArea.width - 32f);
            var height = Mathf.Min(500f, safeArea.height - 32f);
            var basePanel = new Rect(
                safeArea.x + (safeArea.width - width) * 0.5f,
                safeArea.y + (safeArea.height - height) * 0.5f,
                width,
                height);
            var panel = ScaleRect(basePanel, panelScale);
            DrawSolidRect(
                panel,
                new Color(accent.r, accent.g, accent.b, fade));
            var innerPanel = new Rect(
                panel.x + 4f,
                panel.y + 4f,
                panel.width - 8f,
                panel.height - 8f);
            DrawSolidRect(
                innerPanel,
                new Color(0.045f, 0.055f, 0.07f, fade));
            DrawSolidRect(
                new Rect(
                    innerPanel.x,
                    innerPanel.y,
                    innerPanel.width,
                    12f),
                new Color(accent.r, accent.g, accent.b, fade));

            var previousGuiColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            var portraitArea = new Rect(
                panel.x + 42f,
                panel.y + 58f,
                panel.width * 0.38f,
                panel.height - 126f);
            DrawRolePortrait(portraitArea, accent, fade);
            var textArea = new Rect(
                panel.x + panel.width * 0.42f,
                panel.y,
                panel.width * 0.54f,
                panel.height);

            GUI.Label(
                new Rect(textArea.x, panel.y + 58f, textArea.width, 28f),
                isVillain ? "PROJECT SABOTEUR" : "LABORATORY CREW",
                _roleEyebrowStyle);
            GUI.Label(
                new Rect(textArea.x, panel.y + 92f, textArea.width, 82f),
                isVillain ? "빌런" : "시민",
                _roleTitleStyle);
            GUI.Label(
                new Rect(textArea.x + 14f, panel.y + 184f, textArea.width - 28f, 112f),
                isVillain
                    ? "정체를 숨기고 프로젝트를 방해하세요.\n강화 미션과 스피커로 원숭이를 유도할 수 있습니다."
                    : "동료와 협력해 프로젝트를 완성하고 연구소를 탈출하세요.\n회의에서 단서를 공유하고 빌런을 찾아내세요.",
                _roleBodyStyle);
            GUI.Label(
                new Rect(textArea.x, panel.y + 304f, textArea.width, 30f),
                "쉿! 이 역할은 다른 플레이어에게 비밀입니다.",
                _roleHintStyle);

            var secondsRemaining = Mathf.Max(
                0,
                Mathf.CeilToInt(_roleRevealUntil - Time.unscaledTime));
            var canDismiss = Time.unscaledTime >= _roleRevealDismissAt;
            GUI.enabled = canDismiss;
            var buttonLabel = canDismiss
                ? $"확인  ·  {secondsRemaining}초 후 자동 시작"
                : "역할을 확인하세요";
            if (GUI.Button(
                    new Rect(
                        panel.x + panel.width * 0.5f - 150f,
                        panel.yMax - 82f,
                        300f,
                        46f),
                    buttonLabel,
                    _roleButtonStyle))
            {
                _roleRevealUntil = 0f;
            }

            GUI.enabled = true;
            GUI.color = previousGuiColor;
        }

        private void DrawRolePortrait(
            Rect area,
            Color accent,
            float fade)
        {
            var pulse = 0.88f +
                        (Mathf.Sin(Time.unscaledTime * 3.6f) * 0.5f + 0.5f) *
                        0.12f;
            var glow = ScaleRect(area, 0.82f + pulse * 0.08f);
            DrawSolidRect(
                glow,
                new Color(accent.r, accent.g, accent.b, 0.12f * fade));

            if (!TryGetRoleSprite(out var sprite) || sprite.texture == null)
            {
                GUI.Label(area, "?", _roleTitleStyle);
                return;
            }

            var spriteRect = sprite.rect;
            var texture = sprite.texture;
            var uv = new Rect(
                spriteRect.x / texture.width,
                spriteRect.y / texture.height,
                spriteRect.width / texture.width,
                spriteRect.height / texture.height);
            var aspect = spriteRect.width / Mathf.Max(1f, spriteRect.height);
            var drawWidth = Mathf.Min(area.width, area.height * aspect);
            var drawHeight = drawWidth / Mathf.Max(0.01f, aspect);
            if (drawHeight > area.height)
            {
                drawHeight = area.height;
                drawWidth = drawHeight * aspect;
            }

            var drawRect = new Rect(
                area.center.x - drawWidth * 0.5f,
                area.center.y - drawHeight * 0.5f,
                drawWidth,
                drawHeight);
            var previousColor = GUI.color;
            var playerColor = CreateColor(_avatar.Color);
            GUI.color = new Color(
                playerColor.r,
                playerColor.g,
                playerColor.b,
                fade);
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
            GUI.color = previousColor;
        }

        private bool TryGetRoleSprite(out Sprite sprite)
        {
            for (var index = 0; index < _renderers.Length; index++)
            {
                if (_renderers[index] is SpriteRenderer spriteRenderer &&
                    spriteRenderer.sprite != null)
                {
                    sprite = spriteRenderer.sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }

        private static Rect ScaleRect(Rect rect, float scale)
        {
            var width = rect.width * scale;
            var height = rect.height * scale;
            return new Rect(
                rect.center.x - width * 0.5f,
                rect.center.y - height * 0.5f,
                width,
                height);
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.35f;
            var shifted = Mathf.Clamp01(value) - 1f;
            return 1f +
                   (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        private void EnsureRoleStyles()
        {
            if (_roleTitleStyle != null && _roleBodyStyle != null &&
                _roleEyebrowStyle != null && _roleHintStyle != null &&
                _roleButtonStyle != null)
            {
                return;
            }

            _roleEyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            _roleEyebrowStyle.normal.textColor =
                new Color(0.72f, 0.78f, 0.84f);
            _roleTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 60,
                fontStyle = FontStyle.Bold
            };
            _roleTitleStyle.normal.textColor = Color.white;
            _roleBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                wordWrap = true
            };
            _roleBodyStyle.normal.textColor =
                new Color(0.82f, 0.9f, 0.96f);
            _roleHintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Italic
            };
            _roleHintStyle.normal.textColor =
                new Color(1f, 0.78f, 0.35f);
            _roleButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
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
                topDownCamera.Configure(
                    transform,
                    TopDownCamera.DefaultOrthographicSize);
            }
        }

        private void BindGameplayServices(bool isLocalGameplayPlayer)
        {
            // 미션 뷰는 씬을 만들 때 받은 프로토타입 플레이어를 들고 있고, 그 오브젝트는
            // 네트워크 모드에서 비활성화된다. 소유 플레이어를 알려야 E로 미션 화면이 열린다.
            if (isLocalGameplayPlayer)
            {
                LocalGameplayPlayer.Set(gameObject);
            }
            else if (LocalGameplayPlayer.Current == gameObject)
            {
                LocalGameplayPlayer.Set(null);
            }

            // 배정된 미션 테두리 강조는 자기 일지만 본다. 활성화는 소유자 전용
            // 컴포넌트 목록이 관리한다(SDD §7.2).
            if (TryGetComponent<AssignedMissionHighlightDriver>(
                    out var assignedHighlight))
            {
                assignedHighlight.BindJournal(
                    isLocalGameplayPlayer ? _missionJournal : null);
            }

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
            _roleRevealStartedAt = Time.unscaledTime;
            var revealDurationSeconds = _roundState?.Config != null
                ? _roundState.Config.RoleRevealSeconds
                : FallbackRoleRevealDurationSeconds;
            _roleRevealUntil =
                Time.unscaledTime + revealDurationSeconds;
            _roleRevealDismissAt = _roleRevealUntil;
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
