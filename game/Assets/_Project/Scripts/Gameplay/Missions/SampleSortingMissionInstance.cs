using System;
using System.Collections.Generic;
using MonkeyLab.Gameplay.Domain;

namespace MonkeyLab.Gameplay.Missions
{
    /// <summary>시료 라벨과 같은 보관함을 골라 분류하는 미션이다.</summary>
    public sealed class SampleSortingMissionInstance
    {
        private readonly int[] _requiredCategories;
        private readonly bool[] _sortedSamples;

        public SampleSortingMissionInstance(
            IReadOnlyList<int> requiredCategories,
            int categoryCount)
        {
            if (requiredCategories == null)
            {
                throw new ArgumentNullException(nameof(requiredCategories));
            }

            if (requiredCategories.Count < FuseMissionInstance.MinimumFuseCount ||
                requiredCategories.Count > FuseMissionInstance.MaximumFuseCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredCategories));
            }

            if (categoryCount < 2 ||
                categoryCount > requiredCategories.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(categoryCount));
            }

            CategoryCount = categoryCount;
            _requiredCategories = new int[requiredCategories.Count];
            _sortedSamples = new bool[requiredCategories.Count];
            for (var index = 0; index < requiredCategories.Count; index++)
            {
                var category = requiredCategories[index];
                if (category < 1 || category > categoryCount)
                {
                    throw new ArgumentException(
                        "Every sample category must reference a valid bin.",
                        nameof(requiredCategories));
                }

                _requiredCategories[index] = category;
            }
        }

        public MissionState State { get; private set; } = MissionState.Assigned;
        public int SampleCount => _requiredCategories.Length;
        public int CategoryCount { get; }
        public int SelectedSampleId { get; private set; }
        public int SortedSampleCount { get; private set; }

        public void Begin()
        {
            if (State == MissionState.Assigned)
            {
                State = MissionState.InProgress;
            }
        }

        public bool SelectSample(int sampleId)
        {
            if (State != MissionState.InProgress ||
                sampleId < 1 || sampleId > _requiredCategories.Length ||
                _sortedSamples[sampleId - 1])
            {
                return false;
            }

            SelectedSampleId = sampleId;
            return true;
        }

        public int GetRequiredCategory(int sampleId)
        {
            return sampleId >= 1 && sampleId <= _requiredCategories.Length
                ? _requiredCategories[sampleId - 1]
                : 0;
        }

        public bool IsSorted(int sampleId)
        {
            return sampleId >= 1 && sampleId <= _sortedSamples.Length &&
                   _sortedSamples[sampleId - 1];
        }

        public FuseMissionInputResult SubmitCategory(int categoryId)
        {
            if (State != MissionState.InProgress || SelectedSampleId == 0 ||
                categoryId < 1 || categoryId > CategoryCount)
            {
                return FuseMissionInputResult.Ignored;
            }

            var sampleIndex = SelectedSampleId - 1;
            if (_requiredCategories[sampleIndex] != categoryId)
            {
                State = MissionState.Failed;
                return FuseMissionInputResult.Failed;
            }

            _sortedSamples[sampleIndex] = true;
            SortedSampleCount++;
            SelectedSampleId = 0;
            if (SortedSampleCount < _sortedSamples.Length)
            {
                return FuseMissionInputResult.Accepted;
            }

            State = MissionState.Completed;
            return FuseMissionInputResult.Completed;
        }

        public void Cancel()
        {
            if (State == MissionState.InProgress)
            {
                State = MissionState.Cancelled;
            }
        }
    }
}
