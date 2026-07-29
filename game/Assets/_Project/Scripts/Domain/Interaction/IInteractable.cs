using UnityEngine;

namespace MonkeyLab.Gameplay.Interaction
{
    /// <summary>
    /// 플레이어가 E로 상호작용할 수 있는 대상.
    /// M1은 로컬 단독이지만, M2에서 서버 검증을 붙일 수 있도록
    /// "요청 → 검증 → 실행"의 형태를 미리 유지한다 (SDD §6.1).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>상호작용 지점. 오브젝트 중심이 아니라 앞쪽 접근점을 쓸 수 있다.</summary>
        Transform InteractionPoint { get; }

        /// <summary>HUD에 표시할 짧은 문구. 예: "퓨즈 교체".</summary>
        string InteractionPrompt { get; }

        /// <summary>현재 다른 플레이어가 점유 중인지 (SDD §6.3).</summary>
        bool IsOccupied { get; }

        /// <summary>이 대상이 지금 상호작용 가능한 상태인지.</summary>
        bool CanInteract(GameObject actor);

        /// <summary>상호작용을 시작한다. 성공하면 true.</summary>
        bool TryBeginInteract(GameObject actor);
    }
}
