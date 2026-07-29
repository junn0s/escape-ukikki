using UnityEngine;

namespace MonkeyLab.Gameplay.Missions
{
    [CreateAssetMenu(menuName = "Monkey Lab/Missions/Fuse Mission Config", fileName = "SO_FuseMission_Default")]
    public sealed class FuseMissionConfig : ScriptableObject
    {
        [SerializeField] private string _id = "fuse_default";

        [SerializeField, Range(FuseMissionInstance.MinimumFuseCount, FuseMissionInstance.MaximumFuseCount)]
        private int _fuseCount = FuseMissionInstance.MinimumFuseCount;

        public string Id => _id;
        public int FuseCount => Mathf.Clamp(
            _fuseCount,
            FuseMissionInstance.MinimumFuseCount,
            FuseMissionInstance.MaximumFuseCount);
    }
}
