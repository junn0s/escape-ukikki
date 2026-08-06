using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 미션 화면이 쓰는 부품 그림의 단일 참조 지점이다.
    /// 화면마다 스프라이트를 따로 들고 있으면 교체할 때 빠뜨리는 곳이 생긴다
    /// (project-structure.md §8 "프리팹과 아이콘 참조는 카탈로그에 모은다").
    ///
    /// 그림은 `MissionUiSpriteBuilder`가 절차적으로 만든다. 손으로 그린 그림으로
    /// 바꿀 때는 같은 이름으로 덮으면 이 카탈로그를 다시 연결하지 않아도 된다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Presentation/Mission UI Sprite Catalog",
        fileName = "SO_MissionUiSprites_Default")]
    public sealed class MissionUiSpriteCatalog : ScriptableObject
    {
        [SerializeField] private string _id = "mission_ui_sprites_default";

        [Header("공통 프레임 (ui-ux-design.md §9.1)")]
        [SerializeField] private Sprite _panel;
        [SerializeField] private Sprite _button;
        [SerializeField] private Sprite _slot;

        [Header("미션별 부품")]
        [SerializeField] private Sprite _fuse;
        [SerializeField] private Sprite _lever;
        [SerializeField] private Sprite _dial;
        [SerializeField] private Sprite _gauge;
        [SerializeField] private Sprite _cableEnd;
        [SerializeField] private Sprite _dish;
        [SerializeField] private Sprite _led;

        public string Id => _id;

        public Sprite Panel => _panel;
        public Sprite Button => _button;
        public Sprite Slot => _slot;
        public Sprite Fuse => _fuse;
        public Sprite Lever => _lever;
        public Sprite Dial => _dial;
        public Sprite Gauge => _gauge;
        public Sprite CableEnd => _cableEnd;
        public Sprite Dish => _dish;
        public Sprite Led => _led;

        /// <summary>
        /// IMGUI는 <see cref="Sprite"/>가 아니라 <see cref="Texture"/>를 그린다.
        /// 부품 스프라이트는 텍스처 한 장을 통째로 쓰므로 그대로 꺼내 쓸 수 있다.
        /// </summary>
        public static Texture GetTexture(Sprite sprite)
        {
            return sprite != null ? sprite.texture : null;
        }
    }
}
