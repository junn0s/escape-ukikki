using System;
using System.IO;
using System.Threading.Tasks;
using MonkeyLab.Network;
using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    [InitializeOnLoad]
    public static class NetworkSessionTestRunner
    {
        private const string HostClientCommandFileName =
            "MonkeyLabHostClientRelay.command";
        private const string HostClientResultFileName =
            "MonkeyLabHostClientRelay.result";
        private const string HostClientAcknowledgeFileName =
            "MonkeyLabHostClientRelay.ack";
        private const string HostClientDoneFileName =
            "MonkeyLabHostClientRelay.done";

        private static readonly string HostClientCommandPath =
            Path.Combine(Path.GetTempPath(), HostClientCommandFileName);
        private static readonly string HostClientResultPath =
            Path.Combine(Path.GetTempPath(), HostClientResultFileName);
        private static readonly string HostClientAcknowledgePath =
            Path.Combine(Path.GetTempPath(), HostClientAcknowledgeFileName);
        private static readonly string HostClientDonePath =
            Path.Combine(Path.GetTempPath(), HostClientDoneFileName);
        private static readonly bool IsPlayerTwoInstance =
            DetectPlayerTwoInstance();

        private static bool _isPlayerTwoJoinRunning;
        private static string _lastPlayerTwoJoinCode;
        private static double _nextPlayerTwoPollTime;

        static NetworkSessionTestRunner()
        {
            EditorApplication.update += PollPlayerTwoRelayCommand;
        }

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

        [MenuItem("Tools/Monkey Lab/Test Lobby Roster")]
        public static async void TestLobbyRoster()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MonkeyLab] Enter Play Mode before testing the lobby roster.");
                return;
            }

            var roster = UnityEngine.Object.FindFirstObjectByType<LobbyRosterNetwork>();
            if (roster == null || !roster.IsSpawned)
            {
                Debug.LogError("[MonkeyLab] Spawned LobbyRosterNetwork was not found.");
                return;
            }

            if (!roster.TryGetLocalPlayer(out var before))
            {
                Debug.LogError("[MonkeyLab] Local lobby player was not registered.");
                return;
            }

            var requestedReady = !before.IsReady;
            roster.RequestSetReady(requestedReady);
            await Task.Yield();

            if (!roster.TryGetLocalPlayer(out var after) ||
                after.IsReady != requestedReady)
            {
                Debug.LogError("[MonkeyLab] Lobby ready-state validation failed.");
                return;
            }

            Debug.Log(
                $"[MonkeyLab] Lobby roster validation passed: " +
                $"players={roster.PlayerCount}, slot={after.SlotIndex + 1}, " +
                $"color={after.Color}, ready={after.IsReady}, host={after.IsHost}.");
        }

        [MenuItem("Tools/Monkey Lab/Test Host Client Relay")]
        public static async void TestHostClientRelay()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError(
                    "[MonkeyLab] Start the HostClient2 Play Mode Scenario first.");
                return;
            }

            if (IsPlayerTwoInstance)
            {
                Debug.LogError(
                    "[MonkeyLab] Run the Host+Client test from the main Editor.");
                return;
            }

            var controller =
                UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            var roster =
                UnityEngine.Object.FindFirstObjectByType<LobbyRosterNetwork>();
            if (controller == null || roster == null)
            {
                Debug.LogError(
                    "[MonkeyLab] Session controller or lobby roster was not found.");
                return;
            }

            ClearHostClientRelayFiles();
            var sessionWasRemoved = false;

            try
            {
                await controller.CreateSessionAsync();
                var session = controller.CurrentSession;
                if (controller.State != GameSessionState.Connected ||
                    session == null ||
                    !session.IsHost)
                {
                    throw new InvalidOperationException(
                        controller.FailureMessage);
                }

                File.WriteAllText(
                    HostClientCommandPath,
                    session.JoinCode);

                var result = await WaitForFileAsync(
                    HostClientResultPath,
                    TimeSpan.FromSeconds(45));
                if (!result.StartsWith(
                        "PASS|",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(result);
                }

                await WaitForRosterCountAsync(
                    roster,
                    2,
                    TimeSpan.FromSeconds(10));
                if (!roster.TryGetLocalPlayer(out var hostPlayer) ||
                    !hostPlayer.IsHost)
                {
                    throw new InvalidOperationException(
                        "The main Editor was not registered as the lobby host.");
                }

                var verifiedPlayerCount = roster.PlayerCount;
                File.WriteAllText(
                    HostClientAcknowledgePath,
                    "ACK");
                await WaitForFileAsync(
                    HostClientDonePath,
                    TimeSpan.FromSeconds(15));

                Debug.Log(
                    $"[MonkeyLab] Host+Client Relay validation passed: " +
                    $"code={session.JoinCode}, players={verifiedPlayerCount}/6, " +
                    $"player2={result.Substring(5)}.");

                await controller.LeaveSessionAsync();
                sessionWasRemoved = true;
                Debug.Log(
                    "[MonkeyLab] Host+Client Relay test session was removed.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MonkeyLab] Host+Client Relay validation failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                if (!sessionWasRemoved &&
                    controller.CurrentSession != null)
                {
                    try
                    {
                        await controller.LeaveSessionAsync();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"[MonkeyLab] Host+Client Relay cleanup failed: " +
                            $"{exception.GetType().Name}: {exception.Message}");
                    }
                }

                ClearHostClientRelayFiles();
            }
        }

        private static void PollPlayerTwoRelayCommand()
        {
            if (!IsPlayerTwoInstance ||
                _isPlayerTwoJoinRunning ||
                !Application.isPlaying ||
                EditorApplication.timeSinceStartup < _nextPlayerTwoPollTime)
            {
                return;
            }

            _nextPlayerTwoPollTime =
                EditorApplication.timeSinceStartup + 0.5d;
            if (!File.Exists(HostClientCommandPath))
            {
                return;
            }

            var joinCode = File.ReadAllText(
                HostClientCommandPath).Trim();
            if (string.IsNullOrWhiteSpace(joinCode) ||
                string.Equals(
                    joinCode,
                    _lastPlayerTwoJoinCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastPlayerTwoJoinCode = joinCode;
            _isPlayerTwoJoinRunning = true;
            JoinHostClientRelayAsPlayerTwo(joinCode);
        }

        private static async void JoinHostClientRelayAsPlayerTwo(
            string joinCode)
        {
            try
            {
                var controller =
                    UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
                var roster =
                    UnityEngine.Object.FindFirstObjectByType<LobbyRosterNetwork>();
                if (controller == null || roster == null)
                {
                    _isPlayerTwoJoinRunning = false;
                    return;
                }

                await controller.JoinSessionAsync(joinCode);
                await WaitForRosterCountAsync(
                    roster,
                    2,
                    TimeSpan.FromSeconds(20));

                if (!roster.TryGetLocalPlayer(out var player))
                {
                    throw new InvalidOperationException(
                        "Player 2 was not registered in the lobby roster.");
                }

                File.WriteAllText(
                    HostClientResultPath,
                    $"PASS|slot={player.SlotIndex + 1}," +
                    $"color={player.Color},host={player.IsHost}");
                await WaitForFileAsync(
                    HostClientAcknowledgePath,
                    TimeSpan.FromSeconds(15));
                await controller.LeaveSessionAsync();
                File.WriteAllText(
                    HostClientDonePath,
                    "DONE");
            }
            catch (Exception exception)
            {
                File.WriteAllText(
                    HostClientResultPath,
                    $"FAIL|{exception.GetType().Name}:{exception.Message}");
                Debug.LogError(
                    $"[MonkeyLab] Player 2 Relay join failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                _isPlayerTwoJoinRunning = false;
            }
        }

        private static async Task WaitForRosterCountAsync(
            LobbyRosterNetwork roster,
            int expectedCount,
            TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (roster != null &&
                    roster.IsSpawned &&
                    roster.PlayerCount == expectedCount)
                {
                    return;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException(
                $"Lobby roster did not reach {expectedCount} players.");
        }

        private static async Task<string> WaitForFileAsync(
            string path,
            TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }

                await Task.Delay(100);
            }

            throw new TimeoutException(
                $"Timed out while waiting for {Path.GetFileName(path)}.");
        }

        private static bool DetectPlayerTwoInstance()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(
                        arguments[index],
                        "-name",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        arguments[index + 1].Replace(" ", string.Empty),
                        "Player2",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearHostClientRelayFiles()
        {
            TryDeleteFile(HostClientCommandPath);
            TryDeleteFile(HostClientResultPath);
            TryDeleteFile(HostClientAcknowledgePath);
            TryDeleteFile(HostClientDonePath);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MonkeyLab] Could not delete " +
                    $"{Path.GetFileName(path)}: {exception.Message}");
            }
        }
    }
}
