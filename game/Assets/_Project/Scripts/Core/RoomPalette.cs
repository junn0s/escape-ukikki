using UnityEngine;

namespace MonkeyLab.Core
{
    /// <summary>
    /// 방을 색으로 구분하는 기준 팔레트다. 바닥 타일과 미션 목록의 방 표식이 같은
    /// 값을 써야 "저 색 방으로 가라"가 성립하므로 한 곳에서만 정의한다.
    ///
    /// 값은 방별 바닥 텍스처(Resources/Environment/Floors)의 지배색에서 뽑았고,
    /// 색상각이 겹쳤던 실험실 A·액체 보관실·중앙 보안 광장은 서로 벌려 두었다.
    /// UI에서는 어두운 배경 위에 올라가므로 명도를 올린 값을 쓴다.
    /// </summary>
    public static class RoomPalette
    {
        private static readonly Color Fallback = new(0.62f, 0.68f, 0.72f);

        /// <summary>바닥 타일에 곱해 방 색을 벌리는 값이다.</summary>
        public static Color GetFloorTint(string roomId)
        {
            return roomId switch
            {
                // 원래 색이 뚜렷한 방은 타일을 그대로 살린다.
                "VaccineA" => Color.white,
                "VaccineB" => Color.white,
                "Ward" => Color.white,
                "QuarantineA" => Color.white,
                "QuarantineB" => Color.white,
                "Power" => Color.white,
                "LabB" => Color.white,

                // 파랑 계열 세 방만 서로 다른 쪽으로 민다.
                "LabA" => new Color(0.78f, 0.95f, 1f),
                "Storage" => new Color(0.72f, 0.88f, 0.98f),
                "Security" => new Color(0.80f, 0.84f, 1f),
                _ => Color.white
            };
        }

        /// <summary>미션 목록·지도에서 방을 가리키는 표식 색이다.</summary>
        public static Color GetMarkerColor(string roomId)
        {
            return roomId switch
            {
                "VaccineA" => new Color(0.44f, 0.83f, 0.72f),
                "VaccineB" => new Color(0.28f, 0.68f, 0.78f),
                "LabA" => new Color(0.42f, 0.72f, 0.92f),
                "LabB" => new Color(0.60f, 0.52f, 0.90f),
                "QuarantineA" => new Color(0.88f, 0.48f, 0.36f),
                "QuarantineB" => new Color(0.86f, 0.36f, 0.56f),
                "Storage" => new Color(0.52f, 0.86f, 0.96f),
                "Security" => new Color(0.40f, 0.50f, 0.95f),
                "Power" => new Color(0.95f, 0.68f, 0.28f),
                "Ward" => new Color(0.90f, 0.88f, 0.68f),
                _ => Fallback
            };
        }
    }
}
