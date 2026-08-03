using UnityEngine;

namespace MonkeyLab.Presentation.VFX
{
    /// <summary>
    /// 최종 아트·오디오·VFX를 코드 수정 없이 교체하는 표현 에셋 목록이다.
    /// 비어 있는 항목은 벌더가 배치한 프로토타입 스프라이트로 폴백한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_PresentationAssetCatalog_Default",
        menuName = "Monkey Lab/Presentation/Asset Catalog")]
    public sealed class PresentationAssetCatalog : ScriptableObject
    {
        [field: Header("World")]
        [field: SerializeField] public Sprite GuideLightSprite { get; private set; }
        [field: SerializeField] public Sprite ExitMarkerSprite { get; private set; }

        [field: Header("Ending Prefabs")]
        [field: SerializeField] public GameObject HelicopterPrefab { get; private set; }
        [field: SerializeField] public GameObject GasVfxPrefab { get; private set; }
        [field: SerializeField] public GameObject GasShutdownVfxPrefab { get; private set; }
        [field: SerializeField] public GameObject MissionSuccessVfxPrefab { get; private set; }
        [field: SerializeField] public GameObject MissionFailureVfxPrefab { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] public AudioClip PowerRestoredClip { get; private set; }
        [field: SerializeField] public AudioClip ExitRevealedClip { get; private set; }
        [field: SerializeField] public AudioClip HelicopterApproachClip { get; private set; }
        [field: SerializeField] public AudioClip GasReleasedClip { get; private set; }
        [field: SerializeField] public AudioClip MissionSuccessClip { get; private set; }
        [field: SerializeField] public AudioClip MissionFailureClip { get; private set; }
    }
}
