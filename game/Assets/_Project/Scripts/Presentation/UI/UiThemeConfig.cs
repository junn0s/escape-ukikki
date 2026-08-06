using TMPro;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// uGUI·TextMeshPro 화면이 공유하는 색·크기·형태의 단일 원본이다.
    /// 값은 docs/ui-ux-design.md §15.2~15.4와
    /// docs/art-audio-asset-guide.md §1.3 팔레트에서 온다.
    /// 의미 색상(성공·경고·위험)은 색각 보정 프리셋을 거쳐야 하므로 여기 두지 않고
    /// MonkeyLab.Presentation.Settings.LocalGameSettings.GetSemanticColor를 쓴다.
    /// 텍스트 크기 배율도 같은 클래스의 GetScaledFontSize가 담당하므로,
    /// 아래 값은 배율을 곱하지 않은 1920×1080 기준 크기다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Monkey Lab/Presentation/UI Theme Config",
        fileName = "SO_UiTheme_Default")]
    public sealed class UiThemeConfig : ScriptableObject
    {
        [SerializeField] private string _id = "ui_theme_default";

        [Header("폰트 (art-audio-asset-guide.md §9.1)")]
        [Tooltip("본문·버튼·제목용 굵은 한글 산세리프 SDF 에셋")]
        [SerializeField] private TMP_FontAsset _boldFont;
        [Tooltip("보조 설명용 한글 산세리프 SDF 에셋")]
        [SerializeField] private TMP_FontAsset _regularFont;
        [Tooltip("로고 전용 장식 서체. 본문·버튼에는 쓰지 않는다 (§15.2)")]
        [SerializeField] private TMP_FontAsset _displayFont;

        [Header("글자 크기 (ui-ux-design.md §15.2)")]
        [SerializeField] private int _screenTitleFontSize = 56;
        [SerializeField] private int _eventBannerFontSize = 40;
        [SerializeField] private int _sectionTitleFontSize = 32;
        [SerializeField] private int _emphasisValueFontSize = 32;
        [SerializeField] private int _bodyFontSize = 24;
        [SerializeField] private int _captionFontSize = 20;

        [Header("색 (art-audio-asset-guide.md §1.3)")]
        [SerializeField] private Color _bodyTextColor =
            new Color(0.949f, 0.965f, 0.980f);
        [SerializeField] private Color _textOutlineColor =
            new Color(0.020f, 0.031f, 0.051f);
        [SerializeField] private Color _panelBackgroundColor =
            new Color(0.078f, 0.102f, 0.149f);
        [SerializeField] private Color _panelBorderColor =
            new Color(0.227f, 0.290f, 0.388f);
        [SerializeField] private Color _primaryButtonColor =
            new Color(0.204f, 0.776f, 0.784f);
        [SerializeField] private Color _ghostTextColor =
            new Color(0.561f, 0.651f, 0.722f);

        [Header("형태 (ui-ux-design.md §15.3)")]
        [SerializeField] private float _panelCornerRadius = 20f;
        [SerializeField] private float _buttonCornerRadius = 16f;
        [SerializeField] private float _minimumButtonHeight = 56f;
        [SerializeField] private float _panelBorderThickness = 2f;
        [Tooltip("버튼 아래쪽 두께감을 만드는 어두운 립의 높이")]
        [SerializeField] private float _buttonRimHeight = 6f;
        [Tooltip("TMP 외곽선 두께. 0~1 재질 값이며 굵은 글자에서 0.2가 기준이다.")]
        [SerializeField, Range(0f, 1f)]
        private float _textOutlineWidth = 0.2f;

        public string Id => _id;

        public TMP_FontAsset BoldFont => _boldFont;
        public TMP_FontAsset RegularFont => _regularFont;

        /// <summary>로고 전용. 없으면 <see cref="BoldFont"/>로 대체한다.</summary>
        public TMP_FontAsset DisplayFont =>
            _displayFont != null ? _displayFont : _boldFont;

        public int ScreenTitleFontSize => _screenTitleFontSize;
        public int EventBannerFontSize => _eventBannerFontSize;
        public int SectionTitleFontSize => _sectionTitleFontSize;
        public int EmphasisValueFontSize => _emphasisValueFontSize;
        public int BodyFontSize => _bodyFontSize;
        public int CaptionFontSize => _captionFontSize;

        public Color BodyTextColor => _bodyTextColor;
        public Color TextOutlineColor => _textOutlineColor;
        public Color PanelBackgroundColor => _panelBackgroundColor;
        public Color PanelBorderColor => _panelBorderColor;
        public Color PrimaryButtonColor => _primaryButtonColor;
        public Color GhostTextColor => _ghostTextColor;

        public float PanelCornerRadius => _panelCornerRadius;
        public float ButtonCornerRadius => _buttonCornerRadius;
        public float MinimumButtonHeight => _minimumButtonHeight;
        public float PanelBorderThickness => _panelBorderThickness;
        public float ButtonRimHeight => _buttonRimHeight;
        public float TextOutlineWidth => _textOutlineWidth;

        /// <summary>
        /// 폰트 에셋이 붙기 전에는 화면을 만들어도 한글이 렌더되지 않으므로,
        /// 빌더와 화면이 생성 전에 이 값으로 상태를 확인한다.
        /// </summary>
        public bool HasFonts => _boldFont != null && _regularFont != null;
    }
}
