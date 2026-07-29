using MonkeyLab.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLab.Presentation.UI
{
    public sealed class BootstrapStatusView : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_Bootstrap";
        private const float PanelWidth = 560f;
        private const float PanelHeight = 180f;
        private const float RetryButtonHeight = 44f;

        [SerializeField] private BootstrapEntryPoint _entryPoint;

        public BootstrapEntryPoint EntryPoint => _entryPoint;

        public void Configure(BootstrapEntryPoint entryPoint)
        {
            _entryPoint = entryPoint;
        }

        private void Awake()
        {
            if (_entryPoint == null)
            {
                Debug.LogError("[Bootstrap] Status view is missing its entry point.", this);
            }
        }

        private void OnGUI()
        {
            if (_entryPoint == null ||
                SceneManager.GetActiveScene().name != BootstrapSceneName ||
                _entryPoint.State == BootstrapState.Ready)
            {
                return;
            }

            var panel = new Rect(
                (Screen.width - PanelWidth) * 0.5f,
                (Screen.height - PanelHeight) * 0.5f,
                PanelWidth,
                PanelHeight);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("ESCAPE UKIKKI", GUI.skin.box);

            if (_entryPoint.State == BootstrapState.Failed)
            {
                GUILayout.Label(_entryPoint.FailureMessage, GUI.skin.label);
                if (GUILayout.Button("다시 시도", GUILayout.Height(RetryButtonHeight)))
                {
                    _entryPoint.Retry();
                }
            }
            else
            {
                GUILayout.Label("온라인 서비스에 연결하고 있습니다...", GUI.skin.label);
            }

            GUILayout.EndArea();
        }
    }
}
