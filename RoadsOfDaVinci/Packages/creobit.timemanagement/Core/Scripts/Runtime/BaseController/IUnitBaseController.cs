using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController
{
    public interface IUnitBaseController : ILoadUnit, IDisposable, IReloadable
    {
        public event Action<List<StaticObjectView>, IReadOnlyDictionary<Transform, 
            List<MovableObjectView>>> BasementsDistributed;
        public event Action<StaticObjectView, IReadOnlyDictionary<Transform, List<MovableObjectView>>> BasementUpdated;
        public event Action<Transform, GameplayTagSO[], int> UnitStateChanged;
        public event Action<Transform, GameplayTagSO[], int> UnitMaxStateChanged;
        
        /// <summary>
        /// Check if it is possible to follow the path.
        /// </summary>
        /// <param name="destination">The destination to which the path will be built.</param>
        /// <param name="relevantBases">The home points from which path will be build.</param>
        /// <returns>Pair: true if path walkable and walkable home points.</returns>
        public UniTask<(bool, List<Transform>)> IsPathWalkable(Vector3 destination, List<Transform> relevantBases);
        
        /// <summary>
        /// Show path from closest base to destination (if path not walkable).
        /// </summary>
        /// <param name="destination">The destination to which the path will be built.</param>
        /// <param name="relevantBases">The home points from which path will be build.</param>
        public UniTask ShowPath(Vector3 destination, List<Transform> relevantBases);

        /// <summary>
        /// Move unit between home points.
        /// </summary>
        /// <param name="oldPoint">The home point from which unit will be transferred.</param>
        /// <param name="newPoint">The home point to which unit will be transferred.</param>
        /// <param name="movableObjectView">The unit to be moved.</param>
        public void UpdateHomePoint(Transform oldPoint, Transform newPoint, MovableObjectView movableObjectView);
        
        /// <summary>
        /// Get relevant home points and unit count by parameters.
        /// </summary>
        /// <param name="requiredTags">Tags to check unit is relevant.</param>
        /// <param name="mode">Mode to compare tags.</param>
        /// <returns>Pair: list of relevant home points and count of relevant units.</returns>
        public (List<Transform> relevantBases, int relevantUnitsCount) GetRelevantBases(GameplayTagSO[] requiredTags, 
            GameplayTagsContainsMode mode, ResourceBaseSO[] resource = null);

        public void ChangeUnitState(Transform baseTransform, GameplayTagSO[] objectTypeTags, int state);

        public void SetStandingPoint(Transform standingPoint, MovableObjectView unit);

        public void RemoveStandingPoint(Transform standingPoint, MovableObjectView unit);
        
        /// <summary>
        /// Move units between home points.
        /// </summary>
        /// <param name="oldBase">The home point from which all units will be transferred.</param>
        /// <param name="newBase">The home point to which all units will be transferred.</param>
        public void MoveMovableObjects(StaticObjectView oldBase, StaticObjectView newBase);
    }
}