using System.Collections.Generic;
using Pathfinding;
using Pathfinding.Util;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.GridPathBlocking
{
    public sealed class GridAreaBlock
    {
        private static readonly Dictionary<GridAreaBlock, Bounds> AppliedBoundsRegistry = new();

        private readonly List<GridNodeBase> _blockedNodes = new();

        public bool IsApplied { get; private set; }

        public static bool ReachedTargetInsideBlockedArea(Vector2 pathEnd, Vector2 target, float tolerance)
        {
            foreach (var bounds in AppliedBoundsRegistry.Values)
            {
                if (Contains2D(bounds, target) && Distance2D(bounds, pathEnd) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        public static void ClearRegistry()
        {
            AppliedBoundsRegistry.Clear();
        }

        private static bool Contains2D(Bounds bounds, Vector2 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x
                   && point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        private static float Distance2D(Bounds bounds, Vector2 point)
        {
            var dx = Mathf.Max(bounds.min.x - point.x, 0f, point.x - bounds.max.x);
            var dy = Mathf.Max(bounds.min.y - point.y, 0f, point.y - bounds.max.y);

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public void Apply(Bounds bounds)
        {
            if (IsApplied || AstarPath.active == null || !GridPathBlockingBridge.IsActive)
            {
                return;
            }

            IsApplied = true;
            AppliedBoundsRegistry[this] = bounds;

            AstarPath.active.AddWorkItem(() =>
            {
                foreach (var graph in AstarPath.active.graphs)
                {
                    if (graph is not GridGraph gridGraph || !GridPathBlockingBridge.BlocksGraph(graph.graphIndex))
                    {
                        continue;
                    }

                    BlockGraphArea(gridGraph, bounds);
                }
            });
        }

        public void Release()
        {
            if (!IsApplied)
            {
                return;
            }

            IsApplied = false;
            AppliedBoundsRegistry.Remove(this);

            if (AstarPath.active == null)
            {
                _blockedNodes.Clear();
                return;
            }

            AstarPath.active.AddWorkItem(() =>
            {
                foreach (var gridNode in _blockedNodes)
                {
                    if (gridNode.Destroyed)
                    {
                        continue;
                    }

                    gridNode.Blocked--;

                    if (gridNode.Blocked < 0)
                    {
                        gridNode.Walkable = true;
                    }

                    if (AstarData.GetGraph(gridNode) is GridGraph gridGraph)
                    {
                        gridGraph.CalculateConnectionsForCellAndNeighbours(gridNode.XCoordinateInGrid,
                            gridNode.ZCoordinateInGrid);
                    }
                }

                _blockedNodes.Clear();
            });
        }

        private void BlockGraphArea(GridGraph gridGraph, Bounds bounds)
        {
            var nodesInRegion = gridGraph.GetNodesInRegion(bounds);
            var blockedInGraph = new List<GridNodeBase>();

            foreach (var node in nodesInRegion)
            {
                TryBlockNode(node, blockedInGraph);
            }

            ListPool<GraphNode>.Release(ref nodesInRegion);

            if (blockedInGraph.Count == 0)
            {
                TryBlockNode(gridGraph.GetNearest(bounds.center, NNConstraint.None).node, blockedInGraph);
            }

            foreach (var gridNode in blockedInGraph)
            {
                gridGraph.CalculateConnectionsForCellAndNeighbours(gridNode.XCoordinateInGrid,
                    gridNode.ZCoordinateInGrid);
            }
        }

        private void TryBlockNode(GraphNode node, List<GridNodeBase> blockedInGraph)
        {
            if (node is not GridNodeBase gridNode || (!gridNode.Walkable && gridNode.Blocked < 0))
            {
                return;
            }

            gridNode.Blocked++;
            gridNode.Walkable = false;

            _blockedNodes.Add(gridNode);
            blockedInGraph.Add(gridNode);
        }
    }
}
