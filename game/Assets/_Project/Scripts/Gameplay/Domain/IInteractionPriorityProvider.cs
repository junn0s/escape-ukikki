using UnityEngine;

namespace MonkeyLab.Gameplay.Domain
{
    /// <summary>
    /// 같은 월드 위치에 여러 역할별 상호작용이 겹칠 때 선택 우선순위를 제공한다.
    /// 거리가 같은 후보끼리만 비교하므로 가까운 다른 오브젝트를 가로채지 않는다.
    /// </summary>
    public interface IInteractionPriorityProvider
    {
        int GetInteractionPriority(GameObject interactor);
    }
}
