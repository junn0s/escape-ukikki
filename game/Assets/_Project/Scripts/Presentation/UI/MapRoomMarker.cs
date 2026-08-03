using System;
using UnityEngine;

namespace MonkeyLab.Presentation.UI
{
    /// <summary>
    /// 전자지도에 찍는 방 하나다. 이름과 월드 좌표만 가진다.
    /// 런타임 검색 대신 씬 빌더가 미리 채워 넣는다.
    /// </summary>
    [Serializable]
    public struct MapRoomMarker
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Vector2 _worldPosition;

        public MapRoomMarker(string displayName, Vector2 worldPosition)
        {
            _displayName = displayName;
            _worldPosition = worldPosition;
        }

        public string DisplayName => _displayName;
        public Vector2 WorldPosition => _worldPosition;
    }
}
