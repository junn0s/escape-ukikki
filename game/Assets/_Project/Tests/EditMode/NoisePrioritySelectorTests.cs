using System.Collections.Generic;
using MonkeyLab.Core;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// docs/system-design-document.md §9.2 소음 우선순위 5단계 규칙 검증.
    /// 이 규칙이 흔들리면 호스트와 클라이언트의 괴물 행동이 갈라진다.
    /// </summary>
    public sealed class NoisePrioritySelectorTests
    {
        private static NoiseEvent Noise(
            int id,
            float radius = 14f,
            NoiseIntensity intensity = NoiseIntensity.Medium,
            float createdTime = 0f)
        {
            return new NoiseEvent(
                id, NoiseSourceType.MissionFailure, Vector3.zero, radius, intensity, createdTime);
        }

        [Test]
        public void NoCandidates_ReturnsFalse()
        {
            bool found = NoisePrioritySelector.TrySelect(
                new List<NoiseEvent>(), new List<float>(), out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void NoiseOutsideRadius_IsIgnored()
        {
            var candidates = new List<NoiseEvent> { Noise(1, radius: 8f) };
            var distances = new List<float> { 10f }; // 반경 8m를 넘어섬

            Assert.IsFalse(NoisePrioritySelector.TrySelect(candidates, distances, out _));
        }

        [Test]
        public void UnreachableNoise_IsIgnored()
        {
            // 경로가 없으면 음수 거리로 표현한다.
            var candidates = new List<NoiseEvent> { Noise(1) };
            var distances = new List<float> { -1f };

            Assert.IsFalse(NoisePrioritySelector.TrySelect(candidates, distances, out _));
        }

        [Test]
        public void Rule2_ClosestPathDistanceWins()
        {
            var candidates = new List<NoiseEvent> { Noise(1), Noise(2) };
            var distances = new List<float> { 10f, 4f };

            Assert.IsTrue(NoisePrioritySelector.TrySelect(candidates, distances, out NoiseEvent picked));
            Assert.AreEqual(2, picked.NoiseId, "더 가까운 소음을 선택해야 한다");
        }

        [Test]
        public void Rule3_SameDistance_LouderWins()
        {
            var candidates = new List<NoiseEvent>
            {
                Noise(1, radius: 24f, intensity: NoiseIntensity.Small),
                Noise(2, radius: 24f, intensity: NoiseIntensity.Large)
            };
            var distances = new List<float> { 5f, 5f };

            Assert.IsTrue(NoisePrioritySelector.TrySelect(candidates, distances, out NoiseEvent picked));
            Assert.AreEqual(2, picked.NoiseId, "같은 거리면 더 강한 소음");
        }

        [Test]
        public void Rule4_SameDistanceAndIntensity_MoreRecentWins()
        {
            var candidates = new List<NoiseEvent>
            {
                Noise(1, createdTime: 1f),
                Noise(2, createdTime: 9f)
            };
            var distances = new List<float> { 5f, 5f };

            Assert.IsTrue(NoisePrioritySelector.TrySelect(candidates, distances, out NoiseEvent picked));
            Assert.AreEqual(2, picked.NoiseId, "같으면 더 최근 소음");
        }

        [Test]
        public void Rule5_FullTie_LowestNoiseIdWins()
        {
            // 거리·강도·시각이 모두 같은 완전 동점.
            // 결정 규칙이 없으면 목록 순서에 따라 결과가 달라져 재현이 깨진다.
            var candidates = new List<NoiseEvent> { Noise(7), Noise(3), Noise(5) };
            var distances = new List<float> { 5f, 5f, 5f };

            Assert.IsTrue(NoisePrioritySelector.TrySelect(candidates, distances, out NoiseEvent picked));
            Assert.AreEqual(3, picked.NoiseId, "완전 동점이면 NoiseId가 작은 쪽");
        }

        [Test]
        public void Rule5_ResultIsOrderIndependent()
        {
            // 같은 집합을 순서만 바꿔 넣어도 결과가 같아야 한다.
            var forward = new List<NoiseEvent> { Noise(1), Noise(2), Noise(3) };
            var backward = new List<NoiseEvent> { Noise(3), Noise(2), Noise(1) };
            var distances = new List<float> { 5f, 5f, 5f };

            NoisePrioritySelector.TrySelect(forward, distances, out NoiseEvent a);
            NoisePrioritySelector.TrySelect(backward, distances, out NoiseEvent b);

            Assert.AreEqual(a.NoiseId, b.NoiseId, "입력 순서가 결과를 바꾸면 안 된다");
        }

        [Test]
        public void MismatchedListLengths_Throws()
        {
            var candidates = new List<NoiseEvent> { Noise(1) };
            var distances = new List<float> { 1f, 2f };

            Assert.Throws<System.ArgumentException>(
                () => NoisePrioritySelector.TrySelect(candidates, distances, out _));
        }
    }
}
