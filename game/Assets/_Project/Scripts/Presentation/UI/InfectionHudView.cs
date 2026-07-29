using MonkeyLab.Gameplay.Infection;
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

            if (!_infectionService.IsInfected)
            {
                DrawFeedback();
                return;
            }

            var remainingSeconds = _infectionService.RemainingSeconds;
            var timerColor = GetTimerColor(remainingSeconds);
            var timerStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
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
