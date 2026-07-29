using UnityEngine;

namespace MonkeyLab.Gameplay.Application
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Round", fileName = "SO_RoundBalance_Default")]
    public sealed class RoundBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "round_default";
        [SerializeField, Min(0f)] private float _initialGracePeriodSeconds = 30f;

        public string Id => _id;
        public float InitialGracePeriodSeconds => _initialGracePeriodSeconds;
    }
}
