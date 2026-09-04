using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Cysharp.Threading.Tasks;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController
{
    public class BaseUnitsStatesController : IReloadable, IDisposable
    { 
        private UnitBaseController _unitBaseController;
        private readonly MovableObjectController _movableObjectController;
        private readonly IReloadController _reloadController;
        private readonly IObjectViewController _objectViewController;

        private readonly List<BaseUnitsStateView> _views = new();

        private readonly Dictionary<(StaticObjectView, GameplayTagSO[]), BaseUnitsStateView> _viewsDictionary = new();

        /// <summary>
        /// Dependency injection constructor.
        /// </summary>
        public BaseUnitsStatesController(
            IObjectViewController objectViewController,
            MovableObjectController movableObjectController,
            IReloadController reloadController)
        {
            _objectViewController = objectViewController;
            _movableObjectController = movableObjectController;
            _reloadController = reloadController;
        }

        /// <summary>
        /// Initializes the controller by subscribing to events and registering as a reloadable object.
        /// </summary>
        public void Load()
        {
            _unitBaseController = _objectViewController.GetUnitBaseController();
            
            _unitBaseController.BasementsDistributed += OnBasementDistributed;
            _unitBaseController.BasementUpdated += OnBasementUpdated;
            _unitBaseController.UnitStateChanged += OnUnitStateChanged;
            _unitBaseController.UnitMaxStateChanged += OnUnitMaxStateChanged;
            
            _reloadController.AddReloadableObject(this);
        }

        /// <summary>
        /// Clears internal state and resets stored unit views.
        /// </summary>
        public UniTask Reload()
        {
            foreach (var view in _views)
            {
                if (view == null)
                {
                    continue;
                }

                view.SetMaxWorkers(0);
            }
            _views.Clear();
            _viewsDictionary.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Cleans up resources and unsubscribes from events.
        /// </summary>
        public void Dispose()
        {
            _unitBaseController.BasementsDistributed -= OnBasementDistributed;
            _unitBaseController.BasementUpdated -= OnBasementUpdated;
            _unitBaseController.UnitStateChanged -= OnUnitStateChanged;
            _unitBaseController.UnitMaxStateChanged -= OnUnitMaxStateChanged;
            
            _reloadController.RemoveReloadableObject(this);
        }

        /// <summary>
        /// Save view to list
        /// </summary>
        public void AddView(BaseUnitsStateView view)
        {
            _views.Add(view);
        }

        /// <summary>
        /// Remove view from list
        /// </summary>
        public void RemoveView(BaseUnitsStateView view)
        {
            _views.Remove(view);

            foreach (var key in _viewsDictionary
                         .Where(pair => pair.Value == view)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _viewsDictionary.Remove(key);
            }
        }

        /// <summary>
        /// Handles event when unit basement distribution and initializes UI views accordingly.
        /// </summary>
        private void OnBasementDistributed(List<StaticObjectView> basementList,
            IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>> occupiedBasements)
        {
            foreach (var view in _views)
            {
                if (occupiedBasements.TryGetValue(view.Basement, out var units) 
                    && units.Count > 0)
                {
                    var firstUnit = units.First();
                    view.Initialize(firstUnit.MovableObjectDataSO.UnitIconInsideBase, 
                        firstUnit.MovableObjectDataSO.UnitIconOutsideBase);
                    view.SetMaxWorkers(units.Count);
                    view.SetFreeWorkers(units.Count(unit => unit.IsCountedInBase));
                    _viewsDictionary[(view.Basement, firstUnit.ObjectDataSO.ObjectTypeTags)] = view;
                }
            }
        }
        
        /// <summary>
        /// Handles event when unit basement update and update UI views accordingly.
        /// </summary>
        private void OnBasementUpdated(StaticObjectView basement,
            IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>> occupiedBasements)
        {
            var viewOnBasement = basement.GetComponentInChildren<BaseUnitsStateView>(true);
            if (viewOnBasement != null && !_views.Contains(viewOnBasement))
            {
                AddView(viewOnBasement);
            }

            foreach (var view in _views)
            {
                if (view.Basement == basement
                    && occupiedBasements.TryGetValue(view.Basement, out var units) 
                    && units.Count > 0)
                {
                    var firstUnit = units.First();
            
                    view.Initialize(firstUnit.MovableObjectDataSO.UnitIconInsideBase, 
                        firstUnit.MovableObjectDataSO.UnitIconOutsideBase);
                
                    view.SetMaxWorkers(units.Count);

                    // Absolute, not a delta: ChangeFreeWorkers ADDS its argument, so feeding it a
                    // head count re-added the whole base on every upgrade. Counted from the same
                    // list SetMaxWorkers uses, and by the same in-base ledger the +1/-1 events use
                    // (Idle alone is wrong — a unit walking home from a finished task is Idle but
                    // is not in the base yet).
                    view.SetFreeWorkers(units.Count(unit => unit.IsCountedInBase));

                    _viewsDictionary[(view.Basement, firstUnit.ObjectDataSO.ObjectTypeTags)] = view;
                }
            }
        }

        /// <summary>
        /// Handle event when the number of free workers was changed.
        /// </summary>
        private void OnUnitStateChanged(StaticObjectView baseTransform, GameplayTagSO[] objectTypeTags, int state)
        {
            if (!_viewsDictionary.TryGetValue((baseTransform, objectTypeTags), out var unitView))
            {
                return;
            }
            unitView.ChangeFreeWorkers(state);
        }

        /// <summary>
        /// Handle event when the maximum number of workers in a given unit was changed.
        /// </summary>
        private void OnUnitMaxStateChanged(StaticObjectView baseTransform, GameplayTagSO[] objectTypeTags, int maxWorkers)
        {
            if (!_viewsDictionary.TryGetValue((baseTransform, objectTypeTags), out var unitView))
            {
                return;
            }
            unitView.SetMaxWorkers(maxWorkers);
        }
    }
}