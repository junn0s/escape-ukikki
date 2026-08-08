using MonkeyLab.Gameplay.Villain;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Network
{
    /// <summary>
    /// 실험실 씬을 에디터에서 바로 재생했을 때 로컬 호스트를 열어 1인 연습 모드를
    /// 만든다. 미션 배정·완료·진행률 판정을 실제와 같은 서버 경로로 확인하려면
    /// 세션이 있어야 하기 때문이다.
    ///
    /// 메인 메뉴에서 세션을 만들어 들어온 경우에는 이미 <see cref="NetworkManager"/>가
    /// 살아 있으므로 아무것도 하지 않는다. 배포 빌드에서도 동작하지 않는다.
    /// </summary>
    public sealed class DeveloperSoloSession : MonoBehaviour
    {
        /// <summary>비활성 상태로 씬에 두는 로컬 전용 NetworkManager다.</summary>
        [SerializeField] private GameObject _soloNetworkRoot;

        private bool _hasAssignedSoloRole;

        public bool IsSoloSessionActive { get; private set; }
        public GameObject SoloNetworkRoot => _soloNetworkRoot;

        public void Configure(GameObject soloNetworkRoot)
        {
            _soloNetworkRoot = soloNetworkRoot;
        }

        private static bool IsDevelopmentBuild =>
            Application.isEditor || Debug.isDebugBuild;

        private void Start()
        {
            if (!IsDevelopmentBuild)
            {
                enabled = false;
                return;
            }

            if (NetworkManager.Singleton != null)
            {
                // 정상 세션으로 들어온 경우다. 연습 모드는 끼어들지 않는다.
                enabled = false;
                return;
            }

            if (_soloNetworkRoot == null)
            {
                Debug.LogError(
                    "[SoloSession] The solo network root is missing.",
                    this);
                enabled = false;
                return;
            }

            StartSoloHost();
        }

        private void StartSoloHost()
        {
            _soloNetworkRoot.SetActive(true);
            var manager = _soloNetworkRoot.GetComponent<NetworkManager>();
            if (manager == null || manager.NetworkConfig.PlayerPrefab == null)
            {
                Debug.LogError(
                    "[SoloSession] The solo NetworkManager is not configured.",
                    this);
                enabled = false;
                return;
            }

            // 승인 콜백은 메인 메뉴의 GameSessionController가 등록한다. 연습 모드는
            // 로컬 호스트 하나뿐이라 승인 절차가 필요하지 않다.
            manager.NetworkConfig.ConnectionApproval = false;
            if (!manager.StartHost())
            {
                Debug.LogError(
                    "[SoloSession] Failed to start the solo host.",
                    this);
                enabled = false;
                return;
            }

            IsSoloSessionActive = true;
            Debug.Log(
                "[SoloSession] 1인 연습 모드로 시작했다. " +
                "생존자 역할과 개인 미션이 배정된다.");
        }

        private void Update()
        {
            if (!IsSoloSessionActive || _hasAssignedSoloRole)
            {
                return;
            }

            // 역할은 보통 로비가 배정한다. 연습 모드에는 로비가 없으므로 플레이어가
            // 스폰된 뒤 생존자로 지정한다. 빌런은 위장 대상이 있어야 성립하므로
            // 1인에서는 배정하지 않는다(LobbyRosterNetwork의 1인 경로와 같다).
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer ||
                manager.LocalClient.PlayerObject == null ||
                !manager.LocalClient.PlayerObject
                    .TryGetComponent<NetworkPlayerAvatar>(out var avatar) ||
                !avatar.IsSpawned)
            {
                return;
            }

            if (avatar.HasAssignedRole)
            {
                _hasAssignedSoloRole = true;
                return;
            }

            _hasAssignedSoloRole =
                avatar.ServerAssignRole(PlayerRole.Survivor);
        }
    }
}
