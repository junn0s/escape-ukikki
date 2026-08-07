using UnityEngine;

namespace MonkeyLab.Gameplay.Infection
{
    [CreateAssetMenu(menuName = "Monkey Lab/Balance/Antidote", fileName = "SO_AntidoteBalance_Default")]
    public sealed class AntidoteBalanceConfig : ScriptableObject
    {
        [SerializeField] private string _id = "antidote_default";
        [SerializeField, Min(0.1f)] private float _useDurationSeconds = 1.5f;
        [SerializeField, Min(1)] private int _maxCarryCount = 1;
        [SerializeField, Min(1)] private int _fabricatorCount = 2;
        [SerializeField, Min(1)] private int _fabricatorQueueCapacity = 1;
        [SerializeField, Min(0.1f)] private float _codeAnalysisSeconds = 1.5f;
        [SerializeField, Min(1)] private int _codeLength = 5;
        [SerializeField, Min(1)] private int _maxCodeAttempts = 3;
        [SerializeField, Min(0.1f)] private float _synthesisSeconds = 4f;

        public string Id => _id;
        public float UseDurationSeconds => _useDurationSeconds;
        public int MaxCarryCount => _maxCarryCount;

        /// <summary>백신실 A와 B에 한 대씩 두는 제작대 수다.</summary>
        public int FabricatorCount => _fabricatorCount;

        /// <summary>제작대 한 대가 동시에 생산하는 개수다(SDD §12.2).</summary>
        public int FabricatorQueueCapacity => _fabricatorQueueCapacity;

        /// <summary>중앙 제어 PC의 혈청 분석 연출 시간이다(GDD §14.2, SDD §12.1).</summary>
        public float CodeAnalysisSeconds => _codeAnalysisSeconds;

        /// <summary>배합 코드 자릿수다(GDD §14.2).</summary>
        public int CodeLength => _codeLength;

        /// <summary>코드가 무효화되기까지 허용하는 오입 횟수다(GDD §14.2, SDD §12.4).</summary>
        public int MaxCodeAttempts => _maxCodeAttempts;

        /// <summary>코드 정답 입력 후 완성까지 걸리는 합성 시간이다(GDD §14.3, SDD §12.2).</summary>
        public float SynthesisSeconds => _synthesisSeconds;
    }
}
