using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Antidote", fileName = "SO_AntidoteBalance_Default")]
    public sealed class AntidoteBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "antidote_default";
        [SerializeField, Min(0.1f)] private float _useDurationSeconds = 1.5f;
        [SerializeField, Min(1)] private int _maxCarryCount = 1;
        [SerializeField, Min(1f)] private float _craftDurationSeconds = 180f;
        [SerializeField, Min(1)] private int _fabricatorCount = 2;
        [SerializeField, Min(1)] private int _fabricatorQueueCapacity = 1;
        [SerializeField, Min(1)] private int _storageLockerSlotCount = 2;

        public string Id => _id;
        public float UseDurationSeconds => _useDurationSeconds;
        public int MaxCarryCount => _maxCarryCount;

        /// <summary>제작 버튼을 누른 뒤 완성까지 걸리는 시간이다(GDD §14.3).</summary>
        public float CraftDurationSeconds => _craftDurationSeconds;

        /// <summary>백신실 A와 B에 한 대씩 두는 제작기 수다.</summary>
        public int FabricatorCount => _fabricatorCount;

        /// <summary>제작기 한 대가 동시에 생산하는 개수다(SDD §12.1).</summary>
        public int FabricatorQueueCapacity => _fabricatorQueueCapacity;

        /// <summary>지정 보관 칸 한 개가 가지는 슬롯 수다(SDD §12.3).</summary>
        public int StorageLockerSlotCount => _storageLockerSlotCount;
    }
}
