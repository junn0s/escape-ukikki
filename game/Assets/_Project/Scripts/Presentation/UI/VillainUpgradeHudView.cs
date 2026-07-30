using MonkeyLab.Gameplay.Villain;
using MonkeyLab.Network;
using Unity.Netcode;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 강화 단계를 빌런 본인에게만 표시한다.
    /// 생존자 화면에는 어떤 값도 그리지 않는다(docs/system-design-document.md §5).
    /// </summary>
    public sealed class VillainUpgradeHudView : MonoBehaviour
    {
        private NetworkVillainUpgradeAuthority _upgradeAuthority;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        private void OnEnable()
        {
            NetworkVillainUpgradeAuthority.CurrentChanged += BindAuthority;
            BindAuthority();
        }

        private void OnDisable()
        {
            NetworkVillainUpgradeAuthority.CurrentChanged -= BindAuthority;
            UnbindAuthority();
        }

        private void BindAuthority()
        {
            UnbindAuthority();
            _upgradeAuthority = NetworkVillainUpgradeAuthority.Current;
            if (_upgradeAuthority != null)
            {
                _upgradeAuthority.LocalUpgradeStateChanged += RepaintView;
            }
        }

        private void UnbindAuthority()
        {
            if (_upgradeAuthority != null)
            {
                _upgradeAuthority.LocalUpgradeStateChanged -= RepaintView;
            }

            _upgradeAuthority = null;
        }

        private void RepaintView()
        {
        }

        private void OnGUI()
        {
            if (_upgradeAuthority == null ||
                !_upgradeAuthority.IsSpawned ||
                !IsLocalPlayerVillain())
            {
                return;
            }

            EnsureStyles();
            var area = new Rect(Screen.width - 250f, 16f, 234f, 118f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label("강화 단계", _titleStyle);
            GUILayout.Label(
                $"후각 {FormatLevel(UpgradeAxis.Scent)}",
                _bodyStyle);
            GUILayout.Label(
                $"개체 {FormatLevel(UpgradeAxis.Population)}",
                _bodyStyle);
            GUILayout.Label(
                $"독성 {FormatLevel(UpgradeAxis.Toxicity)}",
                _bodyStyle);
            GUILayout.EndArea();
        }

        private string FormatLevel(UpgradeAxis axis)
        {
            var level = _upgradeAuthority.GetLocalLevel(axis);
            return level >= VillainUpgradeState.MaximumLevel
                ? $"{level}/{VillainUpgradeState.MaximumLevel} (최대)"
                : $"{level}/{VillainUpgradeState.MaximumLevel}";
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

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.55f, 0.2f) }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
        }
    }
}
