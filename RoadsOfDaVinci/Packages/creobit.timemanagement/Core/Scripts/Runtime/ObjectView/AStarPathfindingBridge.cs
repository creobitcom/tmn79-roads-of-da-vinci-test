using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.GridPathBlocking;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public class AStarPathfindingBridge : IPathFindable
    {
        private readonly IReloadController _reloadController;

        public AStarPathfindingBridge(IReloadController reloadController)
        {
            _reloadController = reloadController;
        }
        
        public UniTask Load()
        {
            _reloadController.AddReloadableObject(this);
            
            return UniTask.CompletedTask;
        }
        
        public async UniTask Reload()
        {
            Dispose();
            await Load();
        }

        public void Dispose()
        {
            _reloadController.RemoveReloadableObject(this);
        }

        public bool IsPathPossible(Vector3 start, Vector3 end)
        {
            if (AstarPath.active == null)
            {
                return true;
            }

            var startNode = AstarPath.active.GetNearest(start, NNConstraint.Default).node;
            var endNode = AstarPath.active.GetNearest(end, NNConstraint.Default).node;

            if (startNode == null || endNode == null)
            {
                return false;
            }

            return PathUtilities.IsPathPossible(startNode, endNode);
        }

        public async UniTask<(bool, float, Path)> IsPathWalkable(Vector3 start, Vector3 end, Seeker seeker)
        {
            // Single search that honors GraphNode.Blocked exactly like the units' movement does
            // (both go through BlockedNodeTraversalProvider). This guarantees validation and
            // movement AGREE: if this returns walkable a unit can actually reach the target, and
            // if not the task is never registered — so a unit is never dispatched to a spot it
            // then can't path to (which used to freeze it in place), and never cuts straight
            // through an obstacle to a target validation wrongly believed reachable.
            var path = ABPath.Construct(start, end, null);
            path.traversalProvider = MovableObjects.BlockedNodeTraversalProvider.Instance;

            if (seeker != null)
            {
                path.nnConstraint.graphMask = seeker.graphMask;
                path.enabledTags = seeker.traversableTags;
                path.tagPenalties = seeker.tagPenalties;
            }

            AstarPath.StartPath(path);

            await UniTask.WaitUntil(() => path.IsDone());

            if (seeker != null && !path.error)
            {
                seeker.PostProcess(path);
            }

            // Unreachable: A* failed, or the closest path it found stops short of the target.
            if (path.error || path.vectorPath.Count == 0 || !HasReachedEndPoint(path, end))
            {
                return (false, 0f, path);
            }

            var distance = 0f;
            for (var i = 0; i < path.vectorPath.Count - 1; i++)
                distance += Vector3.Distance(path.vectorPath[i], path.vectorPath[i + 1]);

            return (true, distance, path);
        }

        public async UniTask<(bool walkable, Path path)> GetPreviewPath(Vector3 start, Vector3 end, Seeker seeker)
        {
            // Preview-only: a SINGLE search, no penalize-and-retry cascade. That cascade is
            // what hitched on click and drew a convoluted detour up toward the tree for a
            // stone hemmed in by blocked nodes. Here we return the direct path and mark the
            // first blocked node so the dots turn red locally, at the real obstruction.
            var path = ABPath.Construct(start, end, null);

            if (seeker != null)
            {
                path.nnConstraint.graphMask = seeker.graphMask;
                path.enabledTags = seeker.traversableTags;
                path.tagPenalties = seeker.tagPenalties;
            }

            AstarPath.StartPath(path);

            await UniTask.WaitUntil(() => path.IsDone());

            if (seeker != null && !path.error)
            {
                seeker.PostProcess(path);
            }

            if (path.error || path.vectorPath.Count == 0)
            {
                return (false, path);
            }

            var reachedTarget = HasReachedEndPoint(path, end);

            for (var index = 0; index < path.path.Count; index++)
            {
                var graphNode = path.path[index];
                if (graphNode == path.path.First() || graphNode == path.path.Last())
                    continue;
                if (graphNode.Blocked < 0)
                    continue;

                path.blockNode = index;
                return (false, path);
            }

            return (reachedTarget, path);
        }

        private static bool HasReachedEndPoint(Path path, Vector3 end)
        {
            var pathEnd = new Vector2(path.vectorPath.Last().x, path.vectorPath.Last().y);
            var target = new Vector2(end.x, end.y);

            return Vector2.Distance(pathEnd, target) <= 1f
                   || GridAreaBlock.ReachedTargetInsideBlockedArea(pathEnd, target, 1f);
        }
    }
}