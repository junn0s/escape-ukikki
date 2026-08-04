using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    [CreateAssetMenu(
        menuName = "Monkey Lab/Balance/World Lighting Balance Config",
        fileName = "SO_WorldLightingBalance_Default")]
    public sealed class WorldLightingBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "world_lighting_default";
        [SerializeField, Range(0f, 1f)]
        private float _darkGlobalIntensityRatio = 0.12f;
        [Tooltip("전역광 색조. 실제 밝기는 강도 × 이 색이라 어두운 색을 쓰면 " +
                 "강도를 올려도 검게 눌린다.")]
        [SerializeField]
        private Color _darkGlobalTint = new Color(0.45f, 0.58f, 0.78f);
        [SerializeField, Range(0f, 1f)]
        private float _restoredLightIntensityRatio = 0.15f;

        public string Id => _id;
        public float DarkGlobalIntensityRatio =>
            Mathf.Clamp01(_darkGlobalIntensityRatio);
        public Color DarkGlobalTint => _darkGlobalTint;
        public float RestoredLightIntensityRatio =>
            Mathf.Clamp01(_restoredLightIntensityRatio);

        /// <summary>
        /// 화면에 실제로 도달하는 암부 밝기다. Light2D가 강도와 색을 곱하므로
        /// 강도만으로는 체감 밝기를 알 수 없다. 밸런스 판단과 테스트는 이 값을 쓴다.
        /// </summary>
        public float EffectiveDarkLuminance =>
            DarkGlobalIntensityRatio *
            (_darkGlobalTint.r * 0.2126f +
             _darkGlobalTint.g * 0.7152f +
             _darkGlobalTint.b * 0.0722f);
    }
}
