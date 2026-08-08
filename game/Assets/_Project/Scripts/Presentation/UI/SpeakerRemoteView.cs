using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 빌런 전용 스피커 지도다. 생존자 화면에는 아무것도 그리지 않는다.
    /// 남은 쿨타임도 빌런 본인에게만 표시한다(GDD §13.1).
    /// </summary>
    public sealed class SpeakerRemoteView : MonoBehaviour
    {
        private NetworkSpeakerAuthority _speakerAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;
        private bool _isMapOpen;

        private void OnEnable()
        {
            NetworkSpeakerAuthority.CurrentChanged += BindAuthority;
            BindAuthority();
        }

        private void OnDisable()
        {
            NetworkSpeakerAuthority.CurrentChanged -= BindAuthority;
            UnbindAuthority();
        }

        private void BindAuthority()
        {
            UnbindAuthority();
            _speakerAuthority = NetworkSpeakerAuthority.Current;
            if (_speakerAuthority != null)
            {
                _speakerAuthority.LocalCooldownChanged += RepaintView;
            }
        }

        private void UnbindAuthority()
        {
            if (_speakerAuthority != null)
            {
                _speakerAuthority.LocalCooldownChanged -= RepaintView;
            }

            _speakerAuthority = null;
            _isMapOpen = false;
        }

        private void RepaintView()
        {
        }

        private void OnGUI()
        {
            if (_speakerAuthority == null ||
                !_speakerAuthority.IsSpawned ||
                !IsLocalPlayerVillain() ||
                MissionOverlayState.IsOpen ||
                !IsExplorationHudVisible())
            {
                return;
            }

            EnsureStyles();
            var upgradeRect = VillainUpgradeHudView.GetUpgradePanelRect();
            var toggleRect = new Rect(
                upgradeRect.x,
                upgradeRect.yMax + VillainUpgradeHudView.PanelGap,
                VillainUpgradeHudView.PanelWidth,
                30f);
            var remaining = _speakerAuthority.LocalRemainingCooldownSeconds;
            var isReady = _speakerAuthority.IsLocallyReady;
            var toggleLabel = isReady
                ? (_isMapOpen ? "스피커 지도 닫기" : "스피커 지도 열기")
                : $"스피커 쿨타임 {remaining:0.0}초";

            GUI.enabled = isReady;
            if (GUI.Button(toggleRect, toggleLabel, _buttonStyle))
            {
                _isMapOpen = !_isMapOpen;
            }

            GUI.enabled = true;
            if (!_isMapOpen || !isReady)
            {
                return;
            }

            DrawSpeakerMap(toggleRect);
        }

        private void DrawSpeakerMap(Rect toggleRect)
        {
            var speakers = _speakerAuthority.Speakers;
            var height = 44f + speakers.Count * 26f;
            var area = new Rect(
                toggleRect.x,
                toggleRect.yMax + 6f,
                VillainUpgradeHudView.PanelWidth,
                height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(
                new Rect(
                    area.x + 12f,
                    area.y + 10f,
                    area.width - 24f,
                    area.height - 20f));
            GUILayout.Label("스피커를 울릴 방", _titleStyle);
            for (var index = 0; index < speakers.Count; index++)
            {
                var speaker = speakers[index];
                if (speaker == null)
                {
                    continue;
                }

                if (GUILayout.Button(speaker.DisplayName, _bodyStyle))
                {
                    _speakerAuthority.RequestSpeaker(speaker.RoomId);
                    _isMapOpen = false;
                }
            }

            GUILayout.EndArea();
        }

        private static bool IsLocalPlayerVillain()
        {
            var networkManager = NetworkManager.Singleton;
            var playerObject = networkManager != null && networkManager.IsClient
                ? networkManager.LocalClient?.PlayerObject
                : null;
            return playerObject != null &&
                   playerObject.TryGetComponent<NetworkPlayerAvatar>(
                       out var avatar) &&
                   avatar.Role == PlayerRole.Villain;
        }

        private static bool IsExplorationHudVisible()
        {
            var roundState = NetworkRoundState.Current;
            return roundState == null ||
                   roundState.Phase == RoundPhase.Exploration ||
                   roundState.Phase == RoundPhase.GracePeriod;
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.55f, 0.2f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
        }
    }
}
