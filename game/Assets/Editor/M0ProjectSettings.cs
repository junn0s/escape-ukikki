using UnityEditor;
using UnityEngine;

namespace MonkeyLab.EditorTools
{
    /// <summary>
    /// M0 1회용. 프로젝트 설정을 문서 기준으로 맞춘다.
    /// docs/project-structure.md §10.3:
    ///   "Asset Serialization은 Force Text, Version Control은 Visible Meta Files로 설정한다."
    ///
    /// 씬을 바이너리로 두면 diff와 병합이 불가능해 협업에서 씬 충돌을 해결할 수 없다.
    /// </summary>
    public static class M0ProjectSettings
    {
        public static void Run()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[M0] 직렬화 모드: {EditorSettings.serializationMode}");
            Debug.Log($"[M0] 버전 관리 모드: {VersionControlSettings.mode}");
        }
    }
}
