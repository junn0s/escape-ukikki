using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 모든 IMGUI 화면의 기본 서체를 프로젝트 폰트로 바꾼다.
    ///
    /// <c>GUI.skin</c>은 프레임마다 새로 만들어지지 않고 같은 객체가 유지되므로,
    /// 한 번 바꿔두면 이후 모든 화면에 적용된다. 다만 씬 전환이나 스킨 교체로
    /// 되돌아갈 수 있어 매 <c>OnGUI</c>마다 확인한다. 값이 같으면 아무 일도 하지 않는다.
    ///
    /// 실행 순서를 아주 앞으로 당겨 다른 화면이 그리기 전에 서체가 정해지게 한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ImguiFontInstaller : MonoBehaviour
    {
        private static ImguiFontInstaller _instance;

        private Font _font;
        private bool _hasSearched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            _instance = null;
        }

        /// <summary>
        /// 어떤 씬에서 시작하든 적용되어야 하므로 씬 배치가 아니라 자동 생성한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject("[UI] ImguiFont")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _instance = host.AddComponent<ImguiFontInstaller>();
            DontDestroyOnLoad(host);
        }

        private void OnGUI()
        {
            var font = ResolveFont();
            if (font == null || GUI.skin == null || GUI.skin.font == font)
            {
                return;
            }

            GUI.skin.font = font;
        }

        private Font ResolveFont()
        {
            if (_font != null || _hasSearched)
            {
                return _font;
            }

            _hasSearched = true;
            var fontSet = Resources.Load<ImguiFontSet>(
                ImguiFontSet.ResourcePath);
            if (fontSet == null)
            {
                Debug.LogWarning(
                    "[MonkeyLab] " + ImguiFontSet.ResourcePath +
                    " is missing from a Resources folder. IMGUI screens fall " +
                    "back to the built-in font. Run Tools > Monkey Lab > " +
                    "Build > Create Or Update UI Theme.");
                return null;
            }

            _font = fontSet.PreferredFont;
            if (_font == null)
            {
                Debug.LogWarning(
                    "[MonkeyLab] The IMGUI font set has no font assigned.");
            }

            return _font;
        }
    }
}
