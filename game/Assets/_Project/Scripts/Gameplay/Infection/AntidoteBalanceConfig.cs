using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Antidote", fileName = "SO_AntidoteBalance_Default")]
    public sealed class AntidoteBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "antidote_default";
        [SerializeField, Min(0.1f)] private float _useDurationSeconds = 1.5f;
        [SerializeField, Min(1)] private int _maxCarryCount = 1;

        public string Id => _id;
        public float UseDurationSeconds => _useDurationSeconds;
        public int MaxCarryCount => _maxCarryCount;
    }
}
