using System.Collections.Generic;
using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 회의 호출 버튼과 토론·투표·결과 화면이다.
    /// 표는 투표 중에 공개하지 않고 진행 수만 보여준다.
    /// </summary>
    public sealed class MeetingView : MonoBehaviour
    {
        private readonly List<NetworkPlayerAvatar> _candidates = new();

        private NetworkRoundState _roundState;
        private NetworkMeetingAuthority _meetingAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        private ulong _localVoteTargetId =
            NetworkMeetingAuthority.NoExileTargetId;
        private bool _hasLocalVote;

        private void OnEnable()
        {
            NetworkRoundState.CurrentChanged += BindRound;
            NetworkMeetingAuthority.CurrentChanged += BindMeeting;
            BindRound();
            BindMeeting();
        }

        private void OnDisable()
        {
            NetworkRoundState.CurrentChanged -= BindRound;
            NetworkMeetingAuthority.CurrentChanged -= BindMeeting;
            UnbindRound();
            UnbindMeeting();
        }

        private void BindRound()
        {
            UnbindRound();
            _roundState = NetworkRoundState.Current;
            if (_roundState != null)
            {
                _roundState.StateChanged += HandleRoundStateChanged;
            }
        }

        private void UnbindRound()
        {
            if (_roundState != null)
            {
                _roundState.StateChanged -= HandleRoundStateChanged;
            }

            _roundState = null;
        }

        private void BindMeeting()
        {
            UnbindMeeting();
            _meetingAuthority = NetworkMeetingAuthority.Current;
            if (_meetingAuthority != null)
            {
                _meetingAuthority.MeetingStateChanged +=
                    HandleMeetingStateChanged;
            }
        }

        private void UnbindMeeting()
        {
            if (_meetingAuthority != null)
            {
                _meetingAuthority.MeetingStateChanged -=
                    HandleMeetingStateChanged;
            }

            _meetingAuthority = null;
        }

        private void HandleRoundStateChanged()
        {
            if (_roundState != null &&
                _roundState.Phase != RoundPhase.MeetingVote)
            {
                _hasLocalVote = false;
                _localVoteTargetId =
                    NetworkMeetingAuthority.NoExileTargetId;
            }
        }

        private void HandleMeetingStateChanged()
        {
        }

        private void OnGUI()
        {
            if (_roundState == null || !_roundState.IsSpawned)
            {
                return;
            }

            EnsureStyles();
            switch (_roundState.Phase)
            {
                case RoundPhase.Exploration:
                    DrawCallButton();
                    break;
                case RoundPhase.MeetingDiscussion:
                    DrawDiscussion();
                    break;
                case RoundPhase.MeetingVote:
                    DrawVote();
                    break;
                case RoundPhase.MeetingResult:
                    DrawResult();
                    break;
            }
        }

        private void DrawCallButton()
        {
            if (_meetingAuthority == null || !IsLocalPlayerAlive())
            {
                return;
            }

            var remainingMeetings =
                _roundState.Config.MaximumMeetingCount -
                _roundState.UsedMeetingCount;
            if (remainingMeetings <= 0)
            {
                return;
            }

            var canCall =
                _roundState.ElapsedExplorationSeconds >=
                    _roundState.Config.FirstMeetingLockSeconds &&
                (_roundState.UsedMeetingCount == 0 ||
                 _roundState.SecondsSinceLastMeeting >=
                     _roundState.Config.MeetingCooldownSeconds);

            var rect = new Rect(16f, Screen.height - 96f, 220f, 30f);
            GUI.enabled = canCall;
            var label = canCall
                ? $"회의 호출 (남은 {remainingMeetings}회)"
                : "회의 호출 대기 중";
            if (GUI.Button(rect, label, _buttonStyle))
            {
                _meetingAuthority.RequestMeeting();
            }

            GUI.enabled = true;
        }

        private void DrawDiscussion()
        {
            var area = new Rect(
                Screen.width * 0.5f - 220f,
                80f,
                440f,
                120f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("회의 — 토론", _titleStyle);
            GUILayout.Label(
                $"남은 시간 {_roundState.RemainingPhaseSeconds:0}초",
                _bodyStyle);
            GUILayout.Label(
                "단서와 동선을 근거로 이야기한 뒤 투표합니다.",
                _bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawVote()
        {
            RefreshCandidates();
            var height = 120f + _candidates.Count * 26f;
            var area = new Rect(
                Screen.width * 0.5f - 220f,
                80f,
                440f,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("회의 — 투표", _titleStyle);
            GUILayout.Label(
                $"남은 시간 {_roundState.RemainingPhaseSeconds:0}초",
                _bodyStyle);
            if (_meetingAuthority != null)
            {
                GUILayout.Label(
                    $"투표 완료 {_meetingAuthority.CastVoteCount}" +
                    $"/{_meetingAuthority.EligibleVoterCount}",
                    _bodyStyle);
            }

            var canVote = IsLocalPlayerAlive() && _meetingAuthority != null;
            GUI.enabled = canVote;
            for (var index = 0; index < _candidates.Count; index++)
            {
                var candidate = _candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                var clientId = candidate.OwnerClientId;
                var isChosen = _hasLocalVote &&
                               _localVoteTargetId == clientId;
                var label = isChosen
                    ? $"▶ {FormatPlayerName(candidate)}"
                    : FormatPlayerName(candidate);
                if (GUILayout.Button(label, _buttonStyle))
                {
                    _meetingAuthority.RequestVote(clientId);
                    _localVoteTargetId = clientId;
                    _hasLocalVote = true;
                }
            }

            var isAbstaining = _hasLocalVote &&
                               _localVoteTargetId ==
                               NetworkMeetingAuthority.NoExileTargetId;
            if (GUILayout.Button(
                    isAbstaining ? "▶ 기권" : "기권",
                    _buttonStyle))
            {
                _meetingAuthority.RequestVote(
                    NetworkMeetingAuthority.NoExileTargetId);
                _localVoteTargetId =
                    NetworkMeetingAuthority.NoExileTargetId;
                _hasLocalVote = true;
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawResult()
        {
            var area = new Rect(
                Screen.width * 0.5f - 220f,
                80f,
                440f,
                110f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));
            GUILayout.Label("회의 — 결과", _titleStyle);
            if (_meetingAuthority == null ||
                !_meetingAuthority.HasLocalResult ||
                _meetingAuthority.LocalExiledClientId ==
                NetworkMeetingAuthority.NoExileTargetId)
            {
                GUILayout.Label("아무도 퇴출되지 않았습니다.", _bodyStyle);
            }
            else
            {
                // 퇴출된 플레이어의 역할은 라운드가 끝날 때까지 공개하지 않는다.
                GUILayout.Label(
                    $"{FormatClientName(_meetingAuthority.LocalExiledClientId)} 퇴출",
                    _bodyStyle);
            }

            GUILayout.EndArea();
        }

        private void RefreshCandidates()
        {
            _candidates.Clear();
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            foreach (var client in networkManager.ConnectedClients)
            {
                var playerObject = client.Value.PlayerObject;
                if (playerObject == null ||
                    !playerObject.TryGetComponent<NetworkPlayerAvatar>(
                        out var avatar) ||
                    !avatar.HasAssignedRole ||
                    !IsClientAlive(playerObject))
                {
                    continue;
                }

                _candidates.Add(avatar);
            }
        }

        private static bool IsClientAlive(NetworkObject playerObject)
        {
            return !playerObject.TryGetComponent<NetworkInfectionAuthority>(
                       out var infection) ||
                   infection.LifeState != PlayerLifeState.DeadGhost;
        }

        private static bool IsLocalPlayerAlive()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject =
                networkManager != null && networkManager.IsClient
                    ? networkManager.LocalClient?.PlayerObject
                    : null;
            return playerObject != null && IsClientAlive(playerObject);
        }

        private static string FormatPlayerName(NetworkPlayerAvatar avatar)
        {
            return $"{avatar.SlotIndex + 1}번 ({avatar.Color})";
        }

        private static string FormatClientName(ulong clientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null &&
                networkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out var client) &&
                client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent<NetworkPlayerAvatar>(
                    out var avatar))
            {
                return FormatPlayerName(avatar);
            }

            return $"클라이언트 {clientId}";
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
        }
    }
}
