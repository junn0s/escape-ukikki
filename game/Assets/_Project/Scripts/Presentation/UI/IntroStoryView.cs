using MonkeyLab.Presentation.Settings;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 메인 메뉴에 들어오면 배경 이야기를 자동 재생한다(ui-ux-design.md §2.1).
    /// 내용은 새로 만들지 않고 game-design-document.md §4 세계관을 그대로 옮긴 것이다.
    ///
    /// 빌런이 "괴물을 만든 해고된 과학자"라는 설정을 모르면 역할 공개 화면의
    /// `당신은 빌런입니다`가 아무 의미도 전달하지 못한다.
    /// </summary>
    public sealed class IntroStoryView : MonoBehaviour
    {
        private const float PanelWidth = 720f;
        private const float PanelHeight = 300f;
        private const float StoryPageDurationSeconds = 4f;

        /// <summary>GDD §4.1~§4.2. 표의 5개 장과 순서를 그대로 따른다.</summary>
        private static readonly StoryPage[] Pages =
        {
            new(
                "해고",
                "한도윤 박사는 신경 재생 치료제를 개발한 수석 연구원이었다.\n" +
                "임상 데이터 조작 의혹의 책임을 뒤집어쓰고 부당 해고됐다."),
            new(
                "RX-9",
                "그는 퇴사 전 마지막 서버 접속으로 약물을 기화형 RX-9 가스로 개조했다.\n" +
                "RX-9은 생물의 공격성과 후각을 극단적으로 증폭한다.\n" +
                "실험 원숭이들은 흉포한 괴물이 됐다."),
            new(
                "정전",
                "지금 연구소는 정전 상태다.\n" +
                "비상 전원과 유도등만 남았고, 괴물 네 마리가 풀려 있다.\n" +
                "비상문은 봉쇄됐다."),
            new(
                "여섯 명",
                "야근 중이던 여섯 명이 안에 갇혔다.\n" +
                "그중 한 명이 한도윤 박사다.\n" +
                "누가 직원이고 누가 박사인지는 아무도 모른다."),
            new(
                "15분",
                "15분 뒤 출입구에 RX-9 가스가 살포된다.\n" +
                "그 전에 시설을 복구하고 빠져나가야 한다.")
        };

        private int _pageIndex;
        private bool _isPlaying;
        private float _pageEndsAt;

        /// <summary>이야기가 화면을 덮고 있는 동안 메인 메뉴는 그리지 않는다.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>이야기를 첫 장부터 자동 재생한다.</summary>
        public void Replay()
        {
            _pageIndex = 0;
            _isPlaying = true;
            _pageEndsAt = Time.unscaledTime + StoryPageDurationSeconds;
        }

        private void OnEnable()
        {
            Replay();
        }

        private void OnGUI()
        {
            if (!_isPlaying)
            {
                return;
            }

            if (Time.unscaledTime >= _pageEndsAt)
            {
                Advance();
                if (!_isPlaying)
                {
                    return;
                }
            }

            // Esc는 언제든 종료한다. GUI 이벤트로 받아야 다른 화면과 순서가 꼬이지 않는다.
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape)
            {
                Finish();
                currentEvent.Use();
                return;
            }

            DrawBackdrop();

            var page = Pages[_pageIndex];
            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);

            var titleStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(32),
                fontStyle = FontStyle.Bold
            };
            var bodyStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = LocalGameSettings.GetScaledFontSize(20),
                wordWrap = true,
                padding = new RectOffset(20, 20, 18, 18)
            };

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Box(page.Title, titleStyle, GUILayout.Height(56f));
            GUILayout.Box(page.Body, bodyStyle, GUILayout.Height(140f));

            GUILayout.BeginHorizontal();
            GUILayout.Box(
                $"{_pageIndex + 1} / {Pages.Length}",
                GUILayout.Width(90f),
                GUILayout.Height(40f));
            GUILayout.FlexibleSpace();
            var remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(_pageEndsAt - Time.unscaledTime));
            GUILayout.Box(
                $"자동 진행 · {remainingSeconds}초",
                GUILayout.Width(170f),
                GUILayout.Height(40f));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Advance()
        {
            if (_pageIndex >= Pages.Length - 1)
            {
                Finish();
                return;
            }

            _pageIndex++;
            _pageEndsAt = Time.unscaledTime + StoryPageDurationSeconds;
        }

        private void Finish()
        {
            _isPlaying = false;
        }

        /// <summary>메뉴가 비쳐 보이면 글이 읽히지 않으므로 화면을 덮는다.</summary>
        private static void DrawBackdrop()
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private readonly struct StoryPage
        {
            public StoryPage(string title, string body)
            {
                Title = title;
                Body = body;
            }

            public string Title { get; }
            public string Body { get; }
        }
    }
}
