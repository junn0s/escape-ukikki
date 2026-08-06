using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// IMGUI 화면이 쓸 폰트다.
    ///
    /// 화면 17개를 uGUI+TMP로 옮기는 작업은 크고 오래 걸리는데, 그때까지 기본
    /// 폰트로 두면 저장소에 넣은 한글 서체가 화면에 전혀 나오지 않는다. IMGUI도
    /// <c>GUI.skin.font</c>로 서체를 바꿀 수 있으므로 먼저 여기에 적용한다.
    ///
    /// TMP 에셋(<see cref="UiThemeConfig"/>)과 같은 서체 파일을 가리키므로,
    /// uGUI로 옮긴 뒤에도 글자 모양이 달라지지 않는다.
    ///
    /// <see cref="ImguiFontInstaller"/>가 <c>Resources</c>에서 찾아야 하므로
    /// 이 에셋만 Resources 폴더에 둔다. 서체 파일 자체는 옮기지 않는다.
    /// </summary>
    public sealed class ImguiFontSet : ScriptableObject
    {
        /// <summary><see cref="Resources"/> 기준 경로. 확장자를 빼고 쓴다.</summary>
        public const string ResourcePath = "SO_ImguiFontSet";

        [Tooltip("본문·버튼·제목에 쓰는 굵은 한글 산세리프")]
        [SerializeField] private Font _boldFont;

        [Tooltip("보조 설명용 한글 산세리프")]
        [SerializeField] private Font _regularFont;

        public Font BoldFont => _boldFont;
        public Font RegularFont => _regularFont;

        /// <summary>둘 중 있는 것을 쓴다. 굵은 쪽이 어두운 화면에서 잘 읽힌다.</summary>
        public Font PreferredFont =>
            _boldFont != null ? _boldFont : _regularFont;
    }
}
