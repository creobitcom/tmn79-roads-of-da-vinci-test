using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public interface IPathFindable : ILoadUnit, IReloadable, IDisposable
    {
        public UniTask<(bool, float, Path)> IsPathWalkable(Vector3 start, Vector3 end, Seeker seeker);

        /// <summary>
        /// Single-search path for the on-screen preview only: no penalize-and-retry cascade.
        /// Returns the direct path and marks the first blocked node (path.blockNode) so it can
        /// be drawn red locally instead of the convoluted detour the retry search produces.
        /// </summary>
        public UniTask<(bool walkable, Path path)> GetPreviewPath(Vector3 start, Vector3 end, Seeker seeker);

        /// <summary>
        /// O(1) reachability pre-check via graph connected components. False means no path
        /// can exist, so the expensive search can be skipped entirely. True is optimistic
        /// (the path may still be game-blocked) — callers must still verify with
        /// IsPathWalkable when they need the actual path.
        /// </summary>
        public bool IsPathPossible(Vector3 start, Vector3 end);
    }
}