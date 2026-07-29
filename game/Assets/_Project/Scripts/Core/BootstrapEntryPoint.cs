using System.Collections;
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

            yield return null;
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
