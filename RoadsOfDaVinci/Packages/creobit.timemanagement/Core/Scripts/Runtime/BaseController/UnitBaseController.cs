using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController
{
    public class UnitBaseController : IReloadable
    {
        private readonly StaticObjectController _staticObjectController;
        private readonly MovableObjectController _movableObjectController;
        private readonly MovableObjectInventoryController _inventoryController;
        private readonly IPathFindable _pathFinder;
        private readonly ILevelLoader _levelLoader;
        private readonly IReloadController _reloadController;
        private readonly GameplaySceneReferences _gameplaySceneReferences;
        private readonly IObjectResolver _resolver;
        private readonly IObjectViewController _objectViewController;
        private readonly UnitAvailabilityController _unitAvailabilityController;

        private readonly List<StaticObjectView> _basementList = new();
        private readonly List<StaticObjectView> _allPathBlockList = new();
        private readonly Dictionary<StaticObjectView, List<MovableObjectView>> _occupiedBasements = new();
        private readonly List<PathNode> _lastPathNodes = new();
        
        private BaseUnitsStatesController _unitsStatesController;
        
        private ObjectPool<PathNode> _pathNodes; 

        public event Action<List<StaticObjectView>, IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>>>
            BasementsDistributed = delegate { };
        public event Action<StaticObjectView, IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>>> BasementUpdated;
        public event Action<StaticObjectView, GameplayTagSO[], int> UnitStateChanged = delegate { };
        public event Action<StaticObjectView, GameplayTagSO[], int> UnitMaxStateChanged = delegate { };

        public UnitBaseController(StaticObjectController staticObjectController,
            MovableObjectController movableObjectController,
            IPathFindable pathFinder,
            ILevelLoader levelLoader,
            IReloadController reloadController,
            UnitAvailabilityController unitAvailabilityController,
            GameplaySceneReferences gameplaySceneReferences,
            IObjectResolver resolver,
            MovableObjectInventoryController inventoryController,
            IObjectViewController objectViewController)
        {
            _objectViewController = objectViewController;
            _staticObjectController = staticObjectController;
            _movableObjectController = movableObjectController;
            _pathFinder = pathFinder;
            _levelLoader = levelLoader;
            _reloadController = reloadController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _unitAvailabilityController = unitAvailabilityController;
            _resolver = resolver;
            _inventoryController = inventoryController;
        }
        
        public void Load()
        {
            _staticObjectController.AddedStaticObject += StaticObjectAddedHandler;
            _movableObjectController.OnChangeUnitState += ChangeUnitState;
            _levelLoader.LevelLoaded += LevelLoadedHandler;

            _reloadController.AddReloadableObject(this);

            _unitsStatesController = new BaseUnitsStatesController(_objectViewController, 
                _movableObjectController, _reloadController);
            
            _unitsStatesController.Load();

            _pathNodes = new ObjectPool<PathNode>(CreatePathNode);
        }

        public UniTask Reload()
        {
            _basementList.Clear();
            _occupiedBasements.Clear();
            _allPathBlockList.Clear();

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _staticObjectController.AddedStaticObject -= StaticObjectAddedHandler;
            _levelLoader.LevelLoaded -= LevelLoadedHandler;

            _reloadController.RemoveReloadableObject(this);
        }
        
        public BaseUnitsStatesController UnitsStatesController => _unitsStatesController;

        private void StaticObjectAddedHandler(StaticObjectView staticObjectView)
        {
            _allPathBlockList.Add(staticObjectView);

            if (!staticObjectView.IsBase || staticObjectView.BaseData.IsStandingPoint)
            {
                return;
            }

            if (_occupiedBasements.ContainsKey(staticObjectView))
            {
                Log.Gameplay.Info("Duplicate base key: " + staticObjectView.transform.name);
                return;
            }
            
            _basementList.Add(staticObjectView);
            
            _occupiedBasements.Add(staticObjectView, new List<MovableObjectView>());

            _unitAvailabilityController.RegisterBase(staticObjectView, staticObjectView.BaseData);
        }

        private void LevelLoadedHandler()
        {
            for (var i = _basementList.Count - 1; i >= 0; i--)
            {
                var baseView = _basementList[i];
                if (baseView == null)
                {
                    _basementList.RemoveAt(i);
                    continue;
                }

                if (baseView.BaseData.IsActiveOnStart)
                {
                    baseView.SetupBase();
                    _occupiedBasements[baseView] = baseView.SpecialUnits;
                }
            }

            BasementsDistributed?.Invoke(_basementList, _occupiedBasements);
        }

        private static bool UnitTagCompatible(ObjectView.ObjectView basement, GameplayTagSO[] unitTags)
        {
            unitTags = unitTags.Where(tag => tag.TagType == TagType.Unit).ToArray();
            if (basement == null)
            {
                return false;
            }

            return basement.ObjectDataSO is StaticObjectDataSO &&
                   unitTags.Contains(GameplayTagsContainsMode.OnlyOne,
                       ((StaticObjectView)basement).BaseData.BaseTypeTags);
        }

        private static void QuickDistanceSort(IList<(StaticObjectView transform, float distance, Path path)> list, int low, int high)
        {
            if (low >= high)
            {
                return;
            }

            var pivotIndex = Partition(list, low, high);

            QuickDistanceSort(list, low, pivotIndex - 1);

            QuickDistanceSort(list, pivotIndex + 1, high);
        }

        private static int Partition(IList<(StaticObjectView transform, float distance, Path path)> list, int low, int high)
        {
            var pivot = list[high].distance;
            var i = low - 1;

            for (var j = low; j < high; j++)
            {
                if (!(list[j].distance <= pivot))
                {
                    continue;
                }

                i++;

                (list[i], list[j]) = (list[j], list[i]);
            }

            (list[i + 1], list[high]) = (list[high], list[i + 1]);

            return i + 1;
        }

        private static (float, Transform) ClosestDistance(Vector3 destination, float closestDistance,
            Transform basement, Transform closestBasementTransform)
        {
            var distance = Vector3.Distance(basement.position, destination);

            if (!(distance < closestDistance))
            {
                return (closestDistance, closestBasementTransform);
            }

            closestDistance = distance;

            return (closestDistance, basement);
        }

        public async UniTask<(bool, List<StaticObjectView>)> IsPathWalkable(Vector3 destination,
            List<StaticObjectView> relevantBases)
        {
            var possibleTransformsList = new List<(StaticObjectView transform, float distance, Path path)>();

            var anyPathWalkable = false;

            foreach (var basement in relevantBases)
            {
                if (basement == null)
                {
                    Log.Gameplay.Error("Basement is null");
                    continue;
                }
                
                var basementPosition = basement.transform.position;

                if (_occupiedBasements[basement].Count == 0)
                {
                    continue;
                }

                // O(1) pre-check: bases in a different graph area can never reach the
                // destination — skip the expensive search (and its blocked-node retries).
                if (!_pathFinder.IsPathPossible(basementPosition, destination))
                {
                    continue;
                }

                var (walkable, distance, path) =
                    await _pathFinder.IsPathWalkable(basementPosition, destination, basement.BaseData.UnitPrefab.GetComponent<Seeker>());

                if (!walkable)
                {
                    continue;
                }

                anyPathWalkable = true;

                possibleTransformsList.Add((basement, distance, path));
            }

            QuickDistanceSort(possibleTransformsList, 0, possibleTransformsList.Count - 1);

            var sortedTransforms = new List<StaticObjectView>();

            foreach (var item in possibleTransformsList)
            {
                sortedTransforms.Add(item.transform);
            }

            return (anyPathWalkable, sortedTransforms);
        }

        public async UniTask ShowPath(Vector3 destination, List<StaticObjectView> relevantBases)
        {
            // One search is enough for the visualization, and only from a base that can
            // actually reach the destination's graph area (O(1) IsPathPossible check).
            // The old fallback to the nearest-by-straight-line base when NOTHING is reachable
            // made A* scan that base's entire graph component before failing — a hard
            // per-click hitch — and drew the path from the wrong base (e.g. one by the tree).
            // When no base is connected we skip the search entirely; the NO-PATH tooltip
            // already tells the player.
            StaticObjectView closestPossibleBase = null;
            var closestPossibleDistance = float.MaxValue;

            foreach (var basement in relevantBases)
            {
                if (basement == null || _occupiedBasements[basement].Count == 0) continue;

                var distance = Vector3.Distance(basement.transform.position, destination);

                if (distance < closestPossibleDistance
                    && _pathFinder.IsPathPossible(basement.transform.position, destination))
                {
                    closestPossibleDistance = distance;
                    closestPossibleBase = basement;
                }
            }

            if (closestPossibleBase == null)
            {
                HidePathNodes();
                return;
            }

            var (walkable, path) = await _pathFinder.GetPreviewPath(closestPossibleBase.transform.position,
                destination, closestPossibleBase.BaseData.UnitPrefab.GetComponent<Seeker>());

            if (path == null) return;
            RenderPathNodes(path, walkable);
        }

        public async UniTask ShowPathFromUnit(Vector3 start, Vector3 destination, Seeker seeker)
        {
            var (walkable, path) = await _pathFinder.GetPreviewPath(start, destination, seeker);
            if (path == null) return;

            RenderPathNodes(path, walkable);
        }

        private void HidePathNodes()
        {
            foreach (var lastPathNode in _lastPathNodes)
            {
                lastPathNode.gameObject.SetActive(false);
                _pathNodes.Release(lastPathNode);
            }
            _lastPathNodes.Clear();
        }

        private void RenderPathNodes(Path path, bool isWalkable)
        {
            HidePathNodes();

            for (var index = 0; index < path.vectorPath.Count; index++)
            {
                var node = path.vectorPath[index];
                var nodeInstance = _pathNodes.Get();
                _lastPathNodes.Add(nodeInstance);

                nodeInstance.gameObject.SetActive(true);
                nodeInstance.transform.position = node;
                nodeInstance.OnAwake();

                if (!isWalkable && index >= path.blockNode)
                {
                    nodeInstance.onBlocked?.Invoke();
                }
            }

            foreach (var pathBlock in _allPathBlockList)
            {
                if (path.path.Contains(pathBlock.InteractionGraphNode) && pathBlock.isBlocking)
                {
                    pathBlock.OnVisualReset?.Invoke();
                    pathBlock.OnBlockPathHighlight?.Invoke();
                }
            }
        }

        private PathNode CreatePathNode()
        {
            var instance = _resolver.Instantiate(_gameplaySceneReferences.ShowPathNodePrefab,
                _gameplaySceneReferences.PathNodesParent);

            instance.gameObject.SetActive(false);

            return instance;
        }

        public (List<StaticObjectView> relevantBases, int relevantUnitsCount) GetRelevantBases(GameplayTagSO[] requiredTags,
            GameplayTagsContainsMode mode, ResourceBaseSO[] resource = null)
        {
            return GetRelevantBasesInternal(requiredTags, mode, resource);
        }

        public void ChangeUnitState(StaticObjectView baseTransform, GameplayTagSO[] objectTypeTags, int state)
        {
            UnitStateChanged?.Invoke(baseTransform, objectTypeTags, state);
        }

        private (List<StaticObjectView> relevantBases, int relevantUnitsCount) GetRelevantBasesInternal(
            GameplayTagSO[] requiredTags,
            GameplayTagsContainsMode mode,
            ResourceBaseSO[] resource)
        {
            requiredTags = requiredTags.Where(tag => tag.TagType == TagType.Unit).ToArray();
            var relevantBasesTransform = new List<StaticObjectView>();

            var relevantUnits = 0;

            foreach (var basement in _occupiedBasements)
            {
                if (basement.Value.Count == 0)
                {
                    continue;
                }

                var isBaseRelevant = false;

                foreach (var possibleUnit in basement.Value)
                {
                    var hasRequiredTags = requiredTags == null || requiredTags.Length == 0 ||
                                          possibleUnit.MovableObjectDataSO.ObjectTypeTags.Contains(mode, requiredTags);

                    var hasRequiredResources = resource == null 
                                               || _inventoryController.HasResources(possibleUnit, resource);

                    if (!hasRequiredTags || !hasRequiredResources)
                    {
                        continue;
                    }

                    isBaseRelevant = true;

                    relevantUnits++;
                }

                if (isBaseRelevant)
                {
                    relevantBasesTransform.Add(basement.Key);
                }
            }

            return (relevantBasesTransform, relevantUnits);
        }

        public void UpdateHomePoint(StaticObjectView oldPoint, StaticObjectView newPoint, MovableObjectView movableObjectView)
        {
            if (!oldPoint || !newPoint)
            {
                return;
            }

            if (!UnitTagCompatible(newPoint, movableObjectView.MovableObjectDataSO.BaseTypeTags))
            {
                return;
            }

            oldPoint.SpecialUnits.Remove(movableObjectView);
            _occupiedBasements[oldPoint].Remove(movableObjectView);

            UnitMaxStateChanged?.Invoke(oldPoint, movableObjectView.ObjectDataSO.ObjectTypeTags,
                _occupiedBasements[oldPoint].Count);

            newPoint.SpecialUnits.Add(movableObjectView);
            _occupiedBasements[newPoint].Add(movableObjectView);

            UnitMaxStateChanged?.Invoke(newPoint, movableObjectView.ObjectDataSO.ObjectTypeTags,
                _occupiedBasements[newPoint].Count);

            var rootObject = GameObject.FindWithTag("MovableObjectsRoot");
            movableObjectView.transform.SetParent(rootObject?.transform);
            movableObjectView.SetBasement(newPoint);
        }

        public void MoveMovableObjects(StaticObjectView oldBase, StaticObjectView newBase)
        {
            // HERE WE NEED TO MOVE ALL OBJECTS FROM OLD BASE TO NEW BASE
            if (_occupiedBasements.ContainsKey(oldBase))
            {
                for (var index = _occupiedBasements[oldBase].Count - 1; index >= 0; index--)
                {
                    var movableObject = _occupiedBasements[oldBase][index];
                    UpdateHomePoint(oldBase, newBase, movableObject);
                }
            }

            _movableObjectController.GetTaskManager().ReplaceHomePointInTasks(oldBase, newBase);

            newBase.SetupBase();

            _occupiedBasements[newBase] = newBase.SpecialUnits;


            BasementUpdated?.Invoke(newBase, _occupiedBasements);
            BasementUpdated?.Invoke(oldBase, _occupiedBasements);
        }
    }
}