using System;
using MonkeyLab.Network;
using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    public static class NetworkSessionTestRunner
    {
        [MenuItem("Tools/Monkey Lab/Test Create Relay Session")]
        public static async void CreateRelaySession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MonkeyLab] Enter Play Mode before testing a Relay session.");
                return;
            }

            var controller = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            if (controller == null)
            {
                Debug.LogError("[MonkeyLab] GameSessionController was not found.");
                return;
            }

            try
            {
                await controller.CreateSessionAsync();
                if (controller.State != GameSessionState.Connected ||
                    controller.CurrentSession == null)
                {
                    Debug.LogError(
                        $"[MonkeyLab] Relay session validation failed: {controller.FailureMessage}");
                    return;
                }

                var session = controller.CurrentSession;
                Debug.Log(
                    $"[MonkeyLab] Relay session validation passed: " +
                    $"code={session.JoinCode}, players={session.PlayerCount}/{session.MaxPlayers}, " +
                    $"host={session.IsHost}.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MonkeyLab] Relay session validation failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        [MenuItem("Tools/Monkey Lab/Test Leave Relay Session")]
        public static async void LeaveRelaySession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MonkeyLab] Enter Play Mode before leaving a Relay session.");
                return;
            }

            var controller = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            if (controller == null)
            {
                Debug.LogError("[MonkeyLab] GameSessionController was not found.");
                return;
            }

            try
            {
                await controller.LeaveSessionAsync();
                Debug.Log("[MonkeyLab] Relay test session was removed.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MonkeyLab] Relay session cleanup failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
