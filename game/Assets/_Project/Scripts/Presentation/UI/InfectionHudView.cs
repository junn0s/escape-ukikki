using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Presentation.Settings;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    public sealed class InfectionHudView : MonoBehaviour
    {
        private const float FeedbackDurationSeconds = 2f;

        [SerializeField] private InfectionService _infectionService;
        [SerializeField] private AntidoteService _antidoteService;

        private bool _isSubscribed;
        private string _feedback = string.Empty;
        private float _feedbackVisibleUntil;

        public void Configure(
            InfectionService infectionService,
            AntidoteService antidoteService)
        {
            Unsubscribe();
            _infectionService = infectionService;
            _antidoteService = antidoteService;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (_infectionService == null || _antidoteService == null)
            {
                return;
            }

            // 감염 사망은 반드시 화면에 남겨야 한다. 이게 없으면 타이머가 0에 닿는 순간
            // IsInfected가 false로 바뀌면서 패널이 그냥 사라져, 죽은 것이 아니라
            // 아무 일도 없었던 것처럼 보인다.
            if (_infectionService.State == PlayerLifeState.DeadGhost)
            {
                DrawGhostState();
                DrawFeedback();
                return;
            }

            if (!_infectionService.IsInfected)
            {
                DrawAntidoteEconomyStatus();
                DrawFeedback();
                return;
            }

            var remainingSeconds = _infectionService.RemainingSeconds;
            var timerColor = GetTimerColor(remainingSeconds);
            var timerStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(28),
                fontStyle = FontStyle.Bold,
                normal = { textColor = timerColor }
            };
            var panelX = Screen.width - 310f;
            var panelY = Screen.height - 176f;
            GUI.Box(
                new Rect(panelX, panelY, 280f, 68f),
                $"감염 {FormatTime(remainingSeconds)}",
                timerStyle);

            var inventoryText = _antidoteService.HasAntidote
                ? "해독제 1/1  [R] 사용"
                : "해독제 없음";
            GUI.Box(new Rect(panelX, panelY + 72f, 280f, 36f), inventoryText);

            if (_antidoteService.IsUsing)
            {
                var progress = Mathf.RoundToInt(_antidoteService.UseProgressNormalized * 100f);
                GUI.Box(
                    new Rect(panelX, panelY + 112f, 280f, 36f),
                    $"해독제 사용 중 {progress}% — 이동하면 취소");
            }

            DrawFeedback();
        }

        /// <summary>
        /// 유령 상태를 알린다. 문구는 짧고 단호하게 쓴다(ui-ux-design.md §15.5의
        /// 사망 문구 예외). 유령은 청회색으로 구분한다(§15.1).
        /// </summary>
        private void DrawGhostState()
        {
            var ghostStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = LocalGameSettings.GetScaledFontSize(28),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.56f, 0.65f, 0.72f) }
            };
            var panelX = Screen.width - 310f;
            var panelY = Screen.height - 140f;
            GUI.Box(new Rect(panelX, panelY, 280f, 68f), "감염 사망", ghostStyle);
            GUI.Box(
                new Rect(panelX, panelY + 72f, 280f, 36f),
                "유령 — 벽 통과 가능");
        }

        /// <summary>
        /// 감염 전에도 해독제 준비 상태를 읽을 수 있어야 한다(docs/ui-ux-design.md §7).
        /// 해독제는 물린 뒤 만드는 자원이 아니라 사전 준비 자원이다(밸런스 §8.1).
        /// </summary>
        private void DrawAntidoteEconomyStatus()
        {
            var panelX = Screen.width - 310f;
            var panelY = Screen.height - 104f;
            var recipeText = _antidoteService.HasRecipe
                ? "레시피 확보 — 백신실에서 제작 가능"
                : "레시피 미발견";
            GUI.Box(new Rect(panelX, panelY, 280f, 32f), recipeText);

            if (!_antidoteService.HasAntidote)
            {
                return;
            }

            var maxCarryCount = _antidoteService.Config != null
                ? _antidoteService.Config.MaxCarryCount
                : _antidoteService.CarriedCount;
            GUI.Box(
                new Rect(panelX, panelY + 36f, 280f, 32f),
                $"해독제 {_antidoteService.CarriedCount}/{maxCarryCount}");
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static Color GetTimerColor(float remainingSeconds)
        {
            if (remainingSeconds <= 30f)
            {
                var pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                return Color.Lerp(new Color(1f, 0.12f, 0.08f), Color.white, pulse * 0.35f);
            }

            return remainingSeconds <= 60f
                ? new Color(1f, 0.82f, 0.12f)
                : Color.white;
        }

        private void HandleUseCancelled(AntidoteService service)
        {
            ShowFeedback("사용이 중단되었습니다");
        }

        private void HandleUseCompleted(AntidoteService service)
        {
            ShowFeedback("감염이 해제되었습니다");
        }

        private void ShowFeedback(string message)
        {
            _feedback = message;
            _feedbackVisibleUntil = Time.unscaledTime + FeedbackDurationSeconds;
        }

        private void DrawFeedback()
        {
            if (Time.unscaledTime > _feedbackVisibleUntil)
            {
                return;
            }

            GUI.Box(
                new Rect((Screen.width - 360f) * 0.5f, 224f, 360f, 40f),
                _feedback);
        }

        private void Subscribe()
        {
            if (_isSubscribed || _antidoteService == null)
            {
                return;
            }

            _antidoteService.UseCancelled += HandleUseCancelled;
            _antidoteService.UseCompleted += HandleUseCompleted;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _antidoteService == null)
            {
                return;
            }

            _antidoteService.UseCancelled -= HandleUseCancelled;
            _antidoteService.UseCompleted -= HandleUseCompleted;
            _isSubscribed = false;
        }
    }
}
