using System.Collections;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Core
{
    [DefaultExecutionOrder(-10_000)]
    public sealed class BootstrapEntryPoint : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_Bootstrap";
        private const string MainMenuSceneName = "01_MainMenu";

        private static BootstrapEntryPoint _instance;
        [SerializeField] private MonoBehaviour[] _startupTaskBehaviours;

        private bool _isRunning;

        public event Action<BootstrapEntryPoint, BootstrapState> StateChanged;

        public BootstrapState State { get; private set; } = BootstrapState.NotStarted;
        public string FailureMessage { get; private set; } = string.Empty;
        public int StartupTaskCount => _startupTaskBehaviours?.Length ?? 0;

        public void ConfigureStartupTasks(params MonoBehaviour[] startupTaskBehaviours)
        {
            _startupTaskBehaviours = startupTaskBehaviours;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
        }

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != BootstrapSceneName)
            {
                yield break;
            }

            yield return RunStartupAndLoadMainMenu();
        }

        public void Retry()
        {
            if (State != BootstrapState.Failed || _isRunning ||
                SceneManager.GetActiveScene().name != BootstrapSceneName)
            {
                return;
            }

            StartCoroutine(RunStartupAndLoadMainMenu());
        }

        private IEnumerator RunStartupAndLoadMainMenu()
        {
            _isRunning = true;
            FailureMessage = string.Empty;
            SetState(BootstrapState.Initializing);

            foreach (var behaviour in _startupTaskBehaviours ?? Array.Empty<MonoBehaviour>())
            {
                if (behaviour is not IBootstrapTask startupTask)
                {
                    Fail("부팅 작업 연결이 올바르지 않습니다.", null);
                    yield break;
                }

                Task initializationTask;
                try
                {
                    initializationTask = startupTask.InitializeAsync();
                }
                catch (Exception exception)
                {
                    Fail(startupTask.FailureMessage, exception);
                    yield break;
                }

                while (!initializationTask.IsCompleted)
                {
                    yield return null;
                }

                if (initializationTask.IsCanceled)
                {
                    Fail("온라인 서비스 연결이 취소되었습니다.", null);
                    yield break;
                }

                if (initializationTask.IsFaulted)
                {
                    Fail(
                        startupTask.FailureMessage,
                        initializationTask.Exception?.GetBaseException());
                    yield break;
                }

                if (!startupTask.IsReady)
                {
                    Fail(startupTask.FailureMessage, null);
                    yield break;
                }
            }

            _isRunning = false;
            SetState(BootstrapState.Ready);
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
        }

        private void Fail(string userMessage, Exception exception)
        {
            _isRunning = false;
            FailureMessage = string.IsNullOrWhiteSpace(userMessage)
                ? "온라인 서비스에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요."
                : userMessage;
            SetState(BootstrapState.Failed);

            if (exception != null)
            {
                Debug.LogError(
                    $"[Bootstrap] {exception.GetType().Name}: {exception.Message}",
                    this);
            }
        }

        private void SetState(BootstrapState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }
}
