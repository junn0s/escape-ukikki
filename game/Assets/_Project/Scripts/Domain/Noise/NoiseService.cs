using System.Collections.Generic;
using MonkeyLab.Core;
using UnityEngine;

namespace MonkeyLab.Gameplay.Noise
{
    /// <summary>
    /// 소음 사건을 생성하고 유효한 후보 목록을 유지한다.
    /// M1은 로컬 단독이므로 씬에 하나만 두고 참조로 주입한다.
    /// M2에서 서버 권한으로 옮길 때 이 클래스의 호출부는 그대로 두는 것이 목표다.
    ///
    /// docs/system-design-document.md §9
    /// </summary>
    public sealed class NoiseService : MonoBehaviour
    {
        [SerializeField] private SO_GameBalance _balance;

        [Tooltip("소음이 후보 목록에 남아있는 시간. 괴물이 도착·수색할 여유를 준다")]
        [SerializeField] private float _noiseLifetimeSeconds = 10f;

        private readonly List<NoiseEvent> _active = new();
        private int _nextNoiseId = 1;

        /// <summary>현재 유효한 소음 목록.</summary>
        public IReadOnlyList<NoiseEvent> ActiveNoises => _active;

        /// <summary>소음이 발생할 때 발생. 표현 계층이 구독한다.</summary>
        public event System.Action<NoiseEvent> NoiseEmitted;

        private void Awake()
        {
            if (_balance == null)
            {
                Debug.LogError($"[{nameof(NoiseService)}] {nameof(_balance)} 미할당", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // 수명이 지난 소음을 제거한다. 뒤에서부터 지워 인덱스가 밀리지 않게 한다.
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (now - _active[i].CreatedTime > _noiseLifetimeSeconds)
                {
                    _active.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 소음을 발생시킨다. 반경은 강도에 따라 Balance에서 읽는다.
        /// </summary>
        public NoiseEvent Emit(NoiseSourceType sourceType, Vector3 worldPosition, NoiseIntensity intensity)
        {
            var noise = new NoiseEvent(
                _nextNoiseId++,
                sourceType,
                worldPosition,
                GetRadius(intensity),
                intensity,
                Time.time);

            _active.Add(noise);
            NoiseEmitted?.Invoke(noise);

            Debug.Log($"[Noise] id={noise.NoiseId} {sourceType} {intensity} r={noise.PathRadius}m @{worldPosition}");
            return noise;
        }

        private float GetRadius(NoiseIntensity intensity) => intensity switch
        {
            NoiseIntensity.Small => _balance.NoiseRadiusSmall,
            NoiseIntensity.Medium => _balance.NoiseRadiusMedium,
            NoiseIntensity.Large => _balance.NoiseRadiusLarge,
            _ => _balance.NoiseRadiusSmall
        };
    }
}
