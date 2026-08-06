using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeyLab.Gameplay.Monsters
{
    public sealed class TopDownNavigationGraph : MonoBehaviour
    {
        [Serializable]
        public struct Link
        {
            public Link(int fromIndex, int toIndex)
            {
                FromIndex = fromIndex;
                ToIndex = toIndex;
            }

            public int FromIndex;
            public int ToIndex;
        }

        [SerializeField] private Transform[] _nodes = Array.Empty<Transform>();
        [SerializeField] private Link[] _links = Array.Empty<Link>();

        private readonly List<int> _reverseIndices = new(24);
        private readonly List<int> _roamCandidateIndices = new(24);
        private readonly List<Vector2> _distanceScratch = new(24);
        private float[] _costs = Array.Empty<float>();
        private int[] _previous = Array.Empty<int>();
        private bool[] _visited = Array.Empty<bool>();

        public int NodeCount => _nodes?.Length ?? 0;
        public int LinkCount => _links?.Length ?? 0;

        public void Configure(Transform[] nodes, Link[] links)
        {
            _nodes = nodes ?? Array.Empty<Transform>();
            _links = links ?? Array.Empty<Link>();
        }

        public bool TryBuildPath(
            Vector2 start,
            Vector2 destination,
            List<Vector2> output,
            out float distance)
        {
            output.Clear();
            distance = 0f;
            if (NodeCount == 0)
            {
                return false;
            }

            var startIndex = FindNearestNodeIndex(start);
            var destinationIndex = FindNearestNodeIndex(destination);
            if (startIndex < 0 || destinationIndex < 0)
            {
                return false;
            }

            if (startIndex == destinationIndex)
            {
                output.Add(destination);
                distance = Vector2.Distance(start, destination);
                return true;
            }

            EnsureScratchCapacity();
            for (var index = 0; index < NodeCount; index++)
            {
                _costs[index] = float.PositiveInfinity;
                _previous[index] = -1;
                _visited[index] = false;
            }

            _costs[startIndex] = 0f;
            for (var step = 0; step < NodeCount; step++)
            {
                var current = FindLowestCostUnvisited(_costs, _visited);
                if (current < 0)
                {
                    break;
                }

                if (current == destinationIndex)
                {
                    break;
                }

                _visited[current] = true;
                RelaxLinkedNodes(current, _costs, _previous, _visited);
            }

            if (float.IsPositiveInfinity(_costs[destinationIndex]))
            {
                return false;
            }

            _reverseIndices.Clear();
            for (var current = destinationIndex;
                 current >= 0;
                 current = _previous[current])
            {
                _reverseIndices.Add(current);
                if (current == startIndex)
                {
                    break;
                }
            }

            if (_reverseIndices[^1] != startIndex)
            {
                return false;
            }

            for (var index = _reverseIndices.Count - 1; index >= 0; index--)
            {
                output.Add(_nodes[_reverseIndices[index]].position);
            }

            output.Add(destination);
            distance = Vector2.Distance(start, output[0]);
            for (var index = 1; index < output.Count; index++)
            {
                distance += Vector2.Distance(output[index - 1], output[index]);
            }

            return true;
        }

        public bool TryGetPathDistance(
            Vector2 start,
            Vector2 destination,
            out float distance)
        {
            return TryBuildPath(
                start,
                destination,
                _distanceScratch,
                out distance);
        }

        public Vector2 GetNearestPosition(Vector2 position)
        {
            var index = FindNearestNodeIndex(position);
            return index >= 0 ? (Vector2)_nodes[index].position : position;
        }

        public bool TryGetRoamDestination(
            Vector2 start,
            Vector2 origin,
            float radius,
            float minimumTravelDistance,
            out Vector2 destination)
        {
            destination = default;
            _roamCandidateIndices.Clear();
            var radiusSquared = radius * radius;
            var minimumTravelDistanceSquared =
                minimumTravelDistance * minimumTravelDistance;
            for (var index = 0; index < NodeCount; index++)
            {
                if (_nodes[index] == null)
                {
                    continue;
                }

                var position = (Vector2)_nodes[index].position;
                if (Vector2.SqrMagnitude(position - origin) > radiusSquared ||
                    Vector2.SqrMagnitude(position - start) <
                    minimumTravelDistanceSquared ||
                    !TryGetPathDistance(start, position, out _))
                {
                    continue;
                }

                _roamCandidateIndices.Add(index);
            }

            if (_roamCandidateIndices.Count == 0)
            {
                return false;
            }

            var selectedIndex = _roamCandidateIndices[
                UnityEngine.Random.Range(0, _roamCandidateIndices.Count)];
            destination = _nodes[selectedIndex].position;
            return true;
        }

        private int FindNearestNodeIndex(Vector2 position)
        {
            var bestIndex = -1;
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < NodeCount; index++)
            {
                if (_nodes[index] == null)
                {
                    continue;
                }

                var distance = Vector2.SqrMagnitude(
                    (Vector2)_nodes[index].position - position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestIndex = index;
                bestDistance = distance;
            }

            return bestIndex;
        }

        private void EnsureScratchCapacity()
        {
            if (_costs.Length == NodeCount)
            {
                return;
            }

            _costs = new float[NodeCount];
            _previous = new int[NodeCount];
            _visited = new bool[NodeCount];
        }

        private int FindLowestCostUnvisited(float[] costs, bool[] visited)
        {
            var bestIndex = -1;
            var bestCost = float.PositiveInfinity;
            for (var index = 0; index < NodeCount; index++)
            {
                if (visited[index] || costs[index] >= bestCost)
                {
                    continue;
                }

                bestIndex = index;
                bestCost = costs[index];
            }

            return bestIndex;
        }

        private void RelaxLinkedNodes(
            int current,
            float[] costs,
            int[] previous,
            bool[] visited)
        {
            foreach (var link in _links)
            {
                var neighbour = link.FromIndex == current
                    ? link.ToIndex
                    : link.ToIndex == current
                        ? link.FromIndex
                        : -1;
                if (neighbour < 0 || neighbour >= NodeCount || visited[neighbour] ||
                    _nodes[current] == null || _nodes[neighbour] == null)
                {
                    continue;
                }

                var candidate = costs[current] + Vector2.Distance(
                    _nodes[current].position,
                    _nodes[neighbour].position);
                if (candidate >= costs[neighbour])
                {
                    continue;
                }

                costs[neighbour] = candidate;
                previous[neighbour] = current;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.8f);
            foreach (var link in _links)
            {
                if (link.FromIndex < 0 || link.FromIndex >= NodeCount ||
                    link.ToIndex < 0 || link.ToIndex >= NodeCount ||
                    _nodes[link.FromIndex] == null || _nodes[link.ToIndex] == null)
                {
                    continue;
                }

                Gizmos.DrawLine(
                    _nodes[link.FromIndex].position,
                    _nodes[link.ToIndex].position);
            }
        }
    }
}
