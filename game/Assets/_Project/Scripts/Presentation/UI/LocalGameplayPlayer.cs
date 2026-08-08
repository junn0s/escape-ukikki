using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 지금 이 화면을 조작하는 플레이어다.
    ///
    /// 미션 뷰는 씬을 만들 때 로컬 프로토타입 플레이어(<c>P_Player_Local</c>)를 받아
    /// 두지만, 네트워크 모드에서는 그 오브젝트가 비활성화되고 실제 조작 대상은 새로
    /// 스폰된 소유 플레이어가 된다(<see cref="Player.NetworkGameplaySceneAdapter"/>).
    /// 그래서 뷰는 들고 있는 참조 대신 이 값을 먼저 본다. 네트워크가 없는 단독
    /// 재생에서는 비어 있으므로 프로토타입 참조로 되돌아간다.
    /// </summary>
    public static class LocalGameplayPlayer
    {
        public static GameObject Current { get; private set; }

        /// <summary>소유권을 얻은 플레이어를 지정한다. 잃으면 null을 넣는다.</summary>
        public static void Set(GameObject player)
        {
            Current = player;
        }

        /// <summary>
        /// 뷰가 사용할 플레이어를 고른다. 네트워크 소유 플레이어가 있으면 그것을,
        /// 없으면 씬을 만들 때 받은 프로토타입 플레이어를 쓴다.
        /// </summary>
        public static GameObject Resolve(GameObject prototypePlayer)
        {
            return Current != null ? Current : prototypePlayer;
        }
    }
}
