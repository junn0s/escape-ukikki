using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    public static class EditModeTestRunner
    {
        private static TestRunnerApi _testRunnerApi;
        private static TestCallbacks _callbacks;

        [MenuItem("Tools/Monkey Lab/Run EditMode Tests")]
        public static void RunAll()
        {
            if (_testRunnerApi != null)
            {
                Debug.LogWarning("[MonkeyLab] EditMode tests are already running.");
                return;
            }

            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new TestCallbacks(ReleaseRunner);
            _testRunnerApi.RegisterCallbacks(_callbacks);
            _testRunnerApi.Execute(new ExecutionSettings(
                new Filter { testMode = TestMode.EditMode }));
        }

        private static void ReleaseRunner()
        {
            if (_testRunnerApi != null && _callbacks != null)
            {
                _testRunnerApi.UnregisterCallbacks(_callbacks);
                Object.DestroyImmediate(_testRunnerApi);
            }

            _testRunnerApi = null;
            _callbacks = null;
        }

        private sealed class TestCallbacks : ICallbacks
        {
            private readonly System.Action _onFinished;

            public TestCallbacks(System.Action onFinished)
            {
                _onFinished = onFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log(
                    $"[MonkeyLab] EditMode tests finished: " +
                    $"passed={result.PassCount}, failed={result.FailCount}, " +
                    $"skipped={result.SkipCount}, inconclusive={result.InconclusiveCount}.");
                _onFinished();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.FailCount > 0)
                {
                    Debug.LogError(
                        $"[MonkeyLab] EditMode test failed: {result.FullName}\n" +
                        $"{result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
