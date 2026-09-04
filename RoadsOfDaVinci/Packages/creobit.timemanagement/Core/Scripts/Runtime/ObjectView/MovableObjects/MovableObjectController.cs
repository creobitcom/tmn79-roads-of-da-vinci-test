using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Rotator;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Transport;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Pathfinding;
using R3;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class MovableObjectController : IReloadable, IDisposable
    {
        private readonly IObjectResolver _resolver;
        private readonly IReloadController _reloadController;
        private readonly ILevelController _levelController;
        private readonly ILevelLoader _levelLoader;
        private readonly IGameplayIntervalsController _gameplayIntervals;
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly IMovableObjectTaskBadgeController _badgeController;
        private readonly GameplaySceneReferences _sceneReferences;

        private readonly List<MovableObjectView> _units = new();
        private readonly CompositeDisposable _disposable = new();
        private readonly UnitAvailabilityController _unitAvailabilityController = new();
        
        private MovableObjectInventoryController _inventoryController;
        private MovableObjectRotatorController _rotatorController;
        private IPathFindable _pathFindable;
        private MovableObjectTaskManager _movableObjectTaskManager;

        private bool _moveToFinishPoint;
        private Transform _finishPoint;
        
        public event Action<MovableObjectView> MovableUnitAdded = delegate { };
        public event Action<StaticObjectView, GameplayTagSO[], int> OnChangeUnitState;

        /// <summary>
        /// Юнит сменил состояние (свободен / занят / в пути). Нужен UI, который показывает
        /// доступность юнитов: тултип («хватает ли рабочих») и панель выбора альтернативы
        /// (серая иконка дрона, пока дрон занят).
        ///
        /// Отдельно от <see cref="OnChangeUnitState"/>: тот стреляет только для юнитов
        /// с UnitHideInBase и только на пересечении границы базы — как сигнал «данные для UI
        /// устарели» он пропускает половину случаев.
        /// </summary>
        public event Action<MovableObjectView> UnitStateChanged = delegate { };
        public bool WasLoaded { get; private set; }

        public MovableObjectController(IReloadController reloadController,
            IObjectResolver resolver,
            ILevelController levelController,
            ILevelLoader levelLoader,
            IGameplayIntervalsController gameplayIntervals,
            IGameResourcesSystem gameResourcesSystem,
            GameplaySceneReferences gameplaySceneReferences,
            IMovableObjectTaskBadgeController badgeController)
        {
            _resolver = resolver;
            _reloadController = reloadController;
            _levelController = levelController;
            _levelLoader = levelLoader;
            _gameplayIntervals = gameplayIntervals;
            _gameResourcesSystem = gameResourcesSystem;
            _sceneReferences = gameplaySceneReferences;
            _badgeController = badgeController;
        }

        public void Load()
        {
            _reloadController.AddReloadableObject(this);
            
            _inventoryController = new MovableObjectInventoryController(_gameResourcesSystem, _sceneReferences);
            _rotatorController = new MovableObjectRotatorController(this);
            _pathFindable = new Runtime.ObjectView.AStarPathfindingBridge(_reloadController);
            _movableObjectTaskManager = new MovableObjectTaskManager(_badgeController, this);
            
            _resolver.Inject(_movableObjectTaskManager);
            
            _rotatorController.Load();
            
            _levelLoader.LevelBaseSO.Skip(1).Subscribe(UpdateFinishPointInfo).AddTo(_disposable);
            _levelLoader.LevelLoaded += LevelLoadedHandler;
            _levelController.FinishScreenShowed += MoveAllUnitsToFinishPoint;
            _levelController.LevelFinished += FreezeAllUnitsOnLevelFinished;
            WasLoaded = true;
        }

        public UniTask Reload()
        {
            _movableObjectTaskManager.Reload();

            // Юниты уровня пересоздаются вместе с его префабом, так что заморозка финиша уедет
            // вместе с ними. Снимаем на всякий случай: цена — один флаг, цена ошибки — юнит,
            // который не двигается весь следующий заход.
            foreach (var unit in _units)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.UnfreezeAfterLevelEnd();
            }

            _units.Clear();

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _levelController.FinishScreenShowed -= MoveAllUnitsToFinishPoint;
            _levelController.LevelFinished -= FreezeAllUnitsOnLevelFinished;
            _levelLoader.LevelLoaded -= LevelLoadedHandler;
            
            foreach (var unit in _units)
            {
                unit.OnEndInteract -= OnEndUnitInteract;
                unit.EndMove -= OnUnitEndMove;
                unit.OnCancelTask -= MoveToHomePointOnCancelTask;
                unit.TransportSealChanged -= OnUnitTransportSealChanged;
                unit.State.Dispose();
            }
            
            _reloadController.RemoveReloadableObject(this);
            _movableObjectTaskManager.Dispose();
            _rotatorController.Dispose();
            _disposable.Dispose();
        }

        public IPathFindable GetPathFindable()
        {
            return _pathFindable;
        }
        public List<MovableObjectView> GetUnits() => _units;
        
        public void AddUnit(MovableObjectView movableObjectView)
        {
            Log.Gameplay.Info($"Adding unit {movableObjectView.name} to controller");
            _units.Add(movableObjectView);

            movableObjectView.State
                .Subscribe(_ => OnUnitStateChanged(movableObjectView))
                .AddTo(_disposable);

            // Отдельной подпиской, а не внутри OnUnitStateChanged: тот async void и делает
            // тяжёлую работу (перебор задач, поиск пути). UI должен узнать о смене состояния
            // сразу, а не после того, как отработает вся эта цепочка.
            movableObjectView.State
                .Subscribe(_ => UnitStateChanged(movableObjectView))
                .AddTo(_disposable);

            movableObjectView.OnEndInteract += OnEndUnitInteract;
            movableObjectView.EndMove += OnUnitEndMove;
            movableObjectView.OnCancelTask += MoveToHomePointOnCancelTask;
            movableObjectView.TransportSealChanged += OnUnitTransportSealChanged;

            MovableUnitAdded?.Invoke(movableObjectView);

            Log.Gameplay.Info($"Unit {movableObjectView.name} successfully added, total units: {_units.Count}");
        }

        public IMovableObjectTaskManager GetTaskManager()
        {
            return _movableObjectTaskManager;
        }

        public UnitAvailabilityController GetUnitAvailabilityController()
        {
            return _unitAvailabilityController;
        }

        /// <summary>
        /// Retrieves a list of free units based on the specified parameters.
        /// </summary>
        /// <param name="from">The list of home points, sorted by priority (e.g., distance to target).</param>
        /// <param name="units">A list of unit types and their respective required counts.</param>
        /// <returns>A list of available units that meet the specified criteria.</returns>
        public List<MovableObjectView> GetFreeUnits(
            List<StaticObjectView> from,
            List<UnitTypeCount> units,
            Vector3 target,
            List<MovableObjectView> current = null,
            ICollection<MovableObjectView> excluded = null)
        {
            var result = current != null
                ? new List<MovableObjectView>(current.Where(u => u.State.Value == UnitState.Idle))
                : new List<MovableObjectView>();
            var resultByType = new List<MovableObjectView>();

            foreach (var unitType in units)
            {
                resultByType.Clear();
                var countAdded = 0;

                foreach (var basementTransform in from)
                {
                    foreach (var unit in _units)
                    {
                        var objectTypeTagsIsEmpty = unitType.unitType == null || unitType.unitType.Length == 0;
                        var maxAvailableUnits = _unitAvailabilityController.GetAvailableUnitCount(basementTransform);

                        if (unit.State.Value != UnitState.Idle
                            || GetUnitIndexInBase(unit, basementTransform) >= maxAvailableUnits)
                        {
                            if (unit.State.Value == UnitState.ReturnHome)
                            {
                            }
                            continue;
                        }

                        if (excluded != null && excluded.Contains(unit))
                        {
                            continue;
                        }

                        if (!IsUnitValid(unit, objectTypeTagsIsEmpty, unitType.tagMode,
                                unitType.unitType, basementTransform))
                        {
                            continue;
                        }

                        resultByType.Add(unit);

                        countAdded++;
                    }
                }
                
                resultByType.Sort((unit1, unit2) => 
                    UnitsByDistance(unit1, target).CompareTo(UnitsByDistance(unit2, target)));
                
                result.AddRange(resultByType.Take(unitType.count - result.Count(unit => 
                    unit.MovableObjectDataSO.ObjectTypeTags.Contains(unitType.tagMode, unitType.unitType))));
            }

            return result;
        }

        public List<MovableObjectView> GetFreeUnitsForTask(MovableObjectTask task)
        {
            return GetFreeUnits(task.UnitsFrom, task.UnitTypeCount, task.Destination, task.Units,
                GetUnitsReservedByEarlierTasks(task));
        }

        private HashSet<MovableObjectView> GetUnitsReservedByEarlierTasks(MovableObjectTask requestingTask)
        {
            var reserved = new HashSet<MovableObjectView>();

            foreach (var task in _movableObjectTaskManager.Tasks.Values)
            {
                if (ReferenceEquals(task, requestingTask))
                {
                    break;
                }

                if (task.TaskProgress != RuntimeConstants.Enums.TaskProgress.Queued || task.UnitCount <= 1)
                {
                    continue;
                }

                var candidates = GetFreeUnits(task.UnitsFrom, task.UnitTypeCount, task.Destination, task.Units,
                    reserved);

                if (candidates.Count == 0 || !HasEnoughPotentialUnits(task))
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    reserved.Add(candidate);
                }
            }

            return reserved;
        }

        private bool HasEnoughPotentialUnits(MovableObjectTask task)
        {
            foreach (var unitType in task.UnitTypeCount)
            {
                var objectTypeTagsIsEmpty = unitType.unitType == null || unitType.unitType.Length == 0;
                var potentialCount = 0;

                foreach (var unit in _units)
                {
                    foreach (var basementTransform in task.UnitsFrom)
                    {
                        if (GetUnitIndexInBase(unit, basementTransform)
                            >= _unitAvailabilityController.GetAvailableUnitCount(basementTransform))
                        {
                            continue;
                        }

                        if (!IsUnitValid(unit, objectTypeTagsIsEmpty, unitType.tagMode, unitType.unitType,
                                basementTransform))
                        {
                            continue;
                        }

                        potentialCount++;

                        break;
                    }
                }

                if (potentialCount < unitType.count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Стоимость кандидата для сортировки «кто ближе». К прямой дистанции добавляется цена
        /// пролётов транспорта между юнитом и целью: по прямой пролёт бесплатен, и на карте с
        /// лифтом задачу регулярно получал юнит С ДРУГОЙ СТОРОНЫ — два рабочих менялись
        /// местами, катаясь в кабине навстречу друг другу.
        /// </summary>
        private float UnitsByDistance(MovableObjectView unit, Vector3 target)
        {
            var position = unit.transform.position;

            return Vector3.Distance(position, target) + TransportSpans.GetCrossingPenalty(position, target);
        }

        private bool IsUnitValid(MovableObjectView unit,
            bool objectTypeTagsIsEmpty,
            GameplayTagsContainsMode tagsContainsMode,
            GameplayTagSO[] objectTypeTags,
            StaticObjectView basementTransform = null)
        {
            // Едущего в кабине юнита НЕ исключаем. Его удерживает MovableObjectTransportHold:
            // приказ во время поездки ничего не двигает и не ломает, он лишь записывает
            // намерение и отрабатывает после высадки. А как фильтр доступности исключение
            // делало подбор хуже: правильный (ближний) исполнитель выпадал из пула ровно на
            // время поездки, и задача уходила юниту с другой стороны пролёта.
            var tagsMatch = objectTypeTagsIsEmpty
                            || unit.MovableObjectDataSO.ObjectTypeTags.Contains(tagsContainsMode, objectTypeTags);

            var basementMatches = basementTransform == null
                                  || (unit.CurrentBasement == basementTransform);

            return tagsMatch && basementMatches;
        }

        public List<MovableObjectView> GetRelevantUnits(
            List<StaticObjectView> from,
            GameplayTagsContainsMode tagsContainsMode,
            GameplayTagSO[] objectTypeTags)
        {
            var result = new List<MovableObjectView>();
            var objectTypeTagsIsEmpty = objectTypeTags == null || objectTypeTags.Length == 0;

            foreach (var basementTransform in from)
            {
                foreach (var unit in _units)
                {
                    if (!IsUnitValid(unit, objectTypeTagsIsEmpty, tagsContainsMode, objectTypeTags, basementTransform))
                    {
                        continue;
                    }

                    // Deliberately no returnHomeTokenSource.Cancel() here: this is a read-only
                    // candidate query (called from UI refreshes and boosters); cancelling the
                    // go-home timer of mere candidates left them loitering in the field.
                    // Actual assignment cancels the timer in TryBeginTask.
                    result.Add(unit);
                }
            }

            return result;
        }

        private bool TryCompleteTasks(MovableObjectView unit)
        {
            Log.Gameplay.Info($"Attempting to complete tasks for unit {unit.name}");
            var inProgressTasks = _movableObjectTaskManager.GetTaskByUnit(unit);
            
            if (inProgressTasks == null)
            {
                Log.Gameplay.Info($"No tasks found for unit {unit.name}, unit is free to proceed");
                return true;
            }

            var taskCompleted = _movableObjectTaskManager.TryCompleteTask(inProgressTasks);
            Log.Gameplay.Info($"Task completion attempt for unit {unit.name}: {(taskCompleted ? "SUCCESS" : "FAILED")}");
            return taskCompleted;
        }

        private async void OnUnitEndMove(MovableObjectView unit)
        {
            // Per-unit spread offset so several units interacting with the same object don't stack
            // on one point. Consumed here: for an anchored object it is applied relative to the
            // anchor (below); otherwise the unit is physically nudged out along it now.
            var spreadOffset = (Vector3)unit.Offset;
            unit.Offset = Vector2.zero;

            var hasInteractionAnchor = unit.CurrentTaskObject is ComplexObject anchorObject
                                       && anchorObject.InteractionAnchor != null;

            // Move a unit if needed. Used for multiple units to spread around an object.
            // Skipped for anchored objects — the destination snap below would overwrite the nudge,
            // so the spread is folded into the anchor position instead.
            if (spreadOffset != Vector3.zero && !hasInteractionAnchor)
            {
                await MoveUnitOutOfPath(unit, spreadOffset);
            }

            // Move unit to exact destination
            if (unit.CurrentTaskObject != null)
            {
                // When an object defines an explicit interaction anchor (a hand-placed point, used
                // for large objects whose pivot sits inside the footprint — e.g. unit camps that
                // upgrade themselves), the unit stands on that anchor and faces the object itself.
                // Otherwise it uses the pivot-relative offset based on the approach side.
                var lookTarget = unit.CurrentTaskObject.Position;

                if (unit.CurrentTaskObject is ComplexObject complexObject
                    && complexObject.InteractionAnchor != null)
                {
                    // Add the per-unit spread so simultaneous workers fan out around the anchor
                    // instead of overlapping on the exact same spot. Per-object multiplier tunes
                    // how far apart they stand.
                    unit.transform.position = complexObject.InteractionAnchor.position
                                              + spreadOffset * complexObject.InteractionAnchorSpread;
                    // Face the building (its pivot) rather than the anchor the unit stands on.
                    lookTarget = complexObject.transform.position;
                }
                else
                {
                    var direction = (unit.transform.position - unit.currentDestination).normalized;
                    unit.transform.position = unit.currentDestination + (direction * unit.CurrentTaskObject.InteractionOffset);
                }

                var lookDirection = (lookTarget - unit.transform.position).normalized;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    var angle = Mathf.Atan2(lookDirection.x, lookDirection.y) * Mathf.Rad2Deg;
                    unit.modelTransform.rotation = unit.initialRotation * Quaternion.AngleAxis(angle, Vector3.up);
                }
            }
            
            if (unit.CurrentTaskObject != null)
            {
                var taskObject = unit.CurrentTaskObject;

                taskObject.CurrentMovableObjectTaskView = unit.movableObjectTaskView;

                taskObject.InteractionSpeed = unit.InteractionSpeed;

                // While waiting for the object to become free the task can be cancelled
                // (CurrentTaskObject becomes null) or the unit re-assigned to another object.
                // Bail out in that case instead of throwing NRE / interacting with a stale object.
                await UniTask.WaitUntil(() => unit.CurrentTaskObject != taskObject || taskObject.IsFree);

                if (unit.CurrentTaskObject != taskObject)
                {
                    return;
                }

                await taskObject.Interact(unit);

                // Objects with no unit requirements never register the unit in their
                // interaction lists, so EndInteractEventInvoke cannot release it — release
                // it here or the unit stays in Work at the object forever.
                if (unit.CurrentTaskObject == taskObject
                    && taskObject is ObjectView objectViewTask
                    && objectViewTask.GetCurrentUnitTypeCount().Count == 0)
                {
                    unit.EndUnitInteract();
                }
            }
            else
            {
                CheckUnitState(unit, 1);

                if (unit.MovableObjectDataSO.StashResourcesAfterEndMove)
                {
                    _inventoryController.StashResources(unit);
                }

                if (!unit.ActivationByConditions)
                {
                    unit.SetActivationState(!unit.UnitHideInBase);
                }
            }
        }
        
        private async UniTask MoveUnitOutOfPath(MovableObjectView unit, Vector3 offset)
        {
            var newPosition = unit.transform.position + offset;
            
            while (Vector3.Distance(unit.transform.position, newPosition) > 0.3f)
            {
                unit.transform.position = newPosition;
                await UniTask.Yield();
            }
        }
        
        private void CheckUnitState(MovableObjectView unit, int stateValue)
        {
            if (!unit.UnitHideInBase)
            {
                return;
            }

            // The base panel keeps free workers as a running +1/-1 total, so a homecoming
            // reported twice permanently hands the base a worker it does not have. That is the
            // normal case for a task with output resources: the RunHome arrival raises +1 from
            // OnUnitEndMove, then CompleteTask -> RemoveUnitsInMove dispatches the (already home)
            // unit home again and MoveToHomePoint's "already there" branch raises +1 once more.
            // The count saturates at MaxUnits and units out in the field keep showing as in-base.
            // Gate both directions on the unit's own ledger flag so each transition counts once.
            var isInBase = stateValue > 0;

            if (unit.IsCountedInBase == isInBase)
            {
                return;
            }

            unit.IsCountedInBase = isInBase;

            OnChangeUnitState?.Invoke(unit.CurrentBasement, unit.ObjectDataSO.ObjectTypeTags, stateValue);
        }

        /// <summary>
        /// Транспорт отпустил юнита: он снова подвижен и стоит уже в другом месте — его увезло
        /// на ту сторону пролёта. Сам по себе State при высадке не меняется (был Idle — остался
        /// Idle), поэтому обычный разбор очереди, висящий на СМЕНЕ состояния, этот момент не
        /// заметит. Прогоняем ту же цепочку руками: закрыть текущую задачу, разобрать очередь,
        /// иначе домой.
        /// </summary>
        private void OnUnitTransportSealChanged(MovableObjectView unit)
        {
            if (unit.IsTransportSealed)
            {
                return;
            }

            OnUnitStateChanged(unit);
        }

        private async void OnUnitStateChanged(MovableObjectView unit)
        {
            try
            {
                if (unit.State.Value != UnitState.Idle)
                {
                    Log.Gameplay.Info($"Unit {unit} is busy, skipping processing");
                    return;
                }
                
                if (!TryCompleteTasks(unit))
                {
                    return;
                }

                if (await TryBeginQueuedTask())
                {
                    return;
                }

                await MoveToHomePointAfterDelay(unit);
            }
            catch (Exception ex)
            {
                Log.Gameplay.Error($"Error in OnUnitBusyStateChanged: {ex.Message}");
                Log.Gameplay.Error(ex);
            }
        }

        private void OnEndUnitInteract(MovableObjectView unit)
        {
            unit.State.Value = UnitState.Idle;
        }

        private int GetUnitIndexInBase(MovableObjectView unit, StaticObjectView basementTransform)
        {
            var baseUnits = 
                _units.Where(u => u.CurrentBasement == basementTransform).ToList();
            return baseUnits.IndexOf(unit);
        }

        private void LevelLoadedHandler()
        {
            _finishPoint = GameObject.FindGameObjectWithTag(RuntimeConstants.SpecialObjects.FinishPointTag).transform;
        }

        private void UpdateFinishPointInfo(LevelBaseSO levelBaseSo)
        {
            _moveToFinishPoint = levelBaseSo.HaveFinalPoint;
        }

        /// <summary>
        /// Уровень пройден — юниты замирают в Idle. Без этого они доигрывают то, чем были заняты:
        /// стоят и бьют объект, к которому уже никто не придёт, или бегут на месте к цели, до
        /// которой путь больше не строится (ReachedDestination edge-triggered, EndMove не придёт).
        /// </summary>
        private void FreezeAllUnitsOnLevelFinished(LevelBaseSO levelBaseSo)
        {
            // На уровнях с финишной точкой юниты по задумке уходят к ней уже ПОСЛЕ победы —
            // там замораживать нельзя, см. MoveAllUnitsToFinishPoint.
            if (_moveToFinishPoint)
            {
                return;
            }

            foreach (var unit in _units)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.FreezeForLevelEnd();
            }
        }

        private void MoveAllUnitsToFinishPoint()
        {
            if (!_moveToFinishPoint)
            {
                return;
            }

            foreach (var unit in _units)
            {
                unit.MoveToFinishPoint(_finishPoint);
            }
        }
        
        public async UniTask MoveToHomePointAfterDelay(MovableObjectView unit, float? customDelay = null, [CanBeNull] Action<MovableObjectView> onEndMove = null)
        {
            if (unit.State.Value != UnitState.Idle)
                return;
            
            if (unit.CurrentStandingPoint != null)
                return; 
            
            unit.MovableObject?.Stop().Forget();
            var delay = customDelay ?? unit.delayToMoveHome;
            delay += unit.delayBeforeMoveToHome / 1000f;

            unit.delayBeforeMoveToHome = 0;

            try
            {
                // Supersede any pending go-home delay: at most one lives per unit, so a
                // cancel through returnHomeTokenSource always reaches the active one.
                unit.returnHomeTokenSource?.Cancel();
                unit.returnHomeTokenSource = new CancellationTokenSource();

                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: unit.returnHomeTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Don't recreate the token source here: it would orphan the CTS a newer
                // caller just installed, making its delay uncancellable.
                return;
            }

            // The unit may have been assigned a task during the delay through a path that
            // doesn't cancel returnHomeTokenSource (e.g. specific-unit tasks), or it may be
            // idle-waiting for the rest of its task's units. Going home now would override
            // the task movement and strand the unit at its house.
            if (unit.State.Value != UnitState.Idle || _movableObjectTaskManager.GetTaskByUnit(unit) != null)
            {
                return;
            }

            await MoveToHomePoint(unit, onEndMove);
        }
        
        public async UniTask MoveToHomePoint(MovableObjectView unit, [CanBeNull] Action<MovableObjectView> onEndMove = null)
        {
            var homePosition = unit.CurrentBasement
                ? unit.CurrentBasement.BaseStandPosition
                : unit.transform.position;

            // Stale task pointers must not survive into the home run: on arrival
            // OnUnitEndMove would teleport the unit back to the old object and re-interact
            // it, and a leftover Offset would shove the unit into the base footprint.
            unit.CurrentTaskObject = null;
            unit.Offset = Vector2.zero;

            if (unit.AStarAI != null)
            {
                // The arrival radius still holds the last task object's InteractionOffset;
                // reset it so the home run (and the "already home" check) use a sane value.
                unit.AStarAI.EndReachedDistance = 0.5f;
            }

            // Дома юнит стоит НЕ в BaseStandPosition, а на ближайшей к ней ноде графа: туда его
            // ставит SetupBase и там заканчивается любой путь A* (reachedEndOfPath в AITMNPath
            // считается до конца пути, а не до точки назначения). База стоит сбоку от дороги,
            // шаг нод 0.7 — сравнение с сырым пивотом не проходило НИКОГДА, и ветка «уже дома»
            // была мёртвой. Из-за этого каждая повторная отправка домой (после задачи с выходными
            // ресурсами их две: RunHome, а следом CompleteTask -> RemoveUnitsInMove -> Idle ->
            // MoveToHomePointAfterDelay) включала уже спрятанного юнита и выдавала нулевой
            // переход: юнит на кадр выпрыгивал из базы и прятался обратно.
            var homeRestPosition = homePosition;

            if (unit.CurrentBasement && AstarPath.active != null)
            {
                // Тот же безконстрейнтный GetNearest, что и в SetupBase, — чтобы «дом» совпадал
                // с точкой, куда юнитов этой базы ставят при спавне.
                var homeNode = AstarPath.active.GetNearest(homePosition, NNConstraint.Default).node;

                if (homeNode != null)
                {
                    homeRestPosition = (Vector3)homeNode.position;
                }
            }

            // Ветка «уже дома» позиционная: она выгружает ресурсы, отчитывается о приходе
            // и прячет юнита без всякого движения. Пока юнита везёт транспорт, его позиция
            // принадлежит транспорту — кабина, проехавшая в полуметре от базы, иначе
            // разгрузила бы рабочего прямо в воздухе. Уходим в обычный MoveTo: приказ
            // сохранится и отработает после высадки.
            //
            // Проверка по ноде добавлена через ||, а не вместо старой: так мы только РАСШИРЯЕМ
            // множество случаев раннего выхода и ни один работающий сценарий не может
            // превратиться в «юнит не пошёл домой».
            if (unit.AStarAI != null &&
                !unit.IsHeldByTransport &&
                (Vector3.Distance(unit.transform.position, homePosition) <= unit.AStarAI.EndReachedDistance
                 || Vector3.Distance(unit.transform.position, homeRestPosition) <= unit.AStarAI.EndReachedDistance
                 || (unit.UnitHideInBase && !unit.ActivationByConditions && !unit.gameObject.activeSelf)))
            {
                // Движения не будет, значит не будет и EndMove, а с ним и разгрузки в
                // OnUnitEndMove. Ветка и так дублирует остальные эффекты прибытия
                // (CheckUnitState, SetActivationState) — разгрузка здесь третья из них.
                // Порядок как при обычном прибытии: сначала разгрузка, потом закрытие задачи.
                // На пустом рюкзаке это no-op — ровно как на любом обычном приходе домой.
                if (unit.MovableObjectDataSO.StashResourcesAfterEndMove)
                {
                    _inventoryController.StashResources(unit);
                }

                onEndMove?.Invoke(unit);
                CheckUnitState(unit, 1);
                if (!unit.ActivationByConditions)
                    unit.SetActivationState(!unit.UnitHideInBase);
                return;
            }

            if (unit.MovableObject != null)
            {
                // A deactivated unit cannot move (a disabled AIPath drops its path and never
                // reports arrival); subscribe before MoveTo so a completion is never missed.
                unit.SetActivationState(true);
                unit.MovableObject.EndMove += onEndMove;
                // Home run ignores Blocked: a unit that just finished a dig must always be able
                // to get home, even if an obstacle was revealed boxing it in (which used to leave
                // it running in place forever). Task moves still respect Blocked.
                // rescueOnUnreachable: if home is ALSO unreachable for a non-Blocked reason
                // (disconnected graph area / unwalkable node), the bounded retries end in a
                // snap-home completion instead of an endless run-in-place — otherwise a RunHome
                // task return would stay stuck in ReturnHome, unassignable, forever.
                unit.MovableObject.MoveTo(homePosition, respectBlocked: false, rescueOnUnreachable: true);
            }
        }

        public MovableObjectInventoryController GetInventoryController()
        {
            return _inventoryController;
        }

        private void MoveToHomePointOnCancelTask(MovableObjectView unit)
        {
            MoveToHomePointAfterDelay(unit, 0f).Forget();
        }

        #region Tasks
        public async UniTask StartNextTaskPoint(MovableObjectTask movableObjectTask)
        {
            try
            {
                var delay = 0;

                for (var i = 0; i < movableObjectTask.Units.Count; i++)
                {
                    if (movableObjectTask.TaskProgress != RuntimeConstants.Enums.TaskProgress.InProgress)
                    {
                        return;
                    }

                    var taskUnit = movableObjectTask.Units[i];
                    
                    // No activeSelf gate: it was a stand-in for "is at home" and missed the exit
                    // whenever the unit was dispatched while still visible (re-tasked on the way
                    // home, or left active by ActivationByConditions), so the base kept counting
                    // it as free. CheckUnitState is idempotent now and owns that decision.
                    CheckUnitState(taskUnit, -1);

                    try
                    {
                        if (movableObjectTask.Units.Count > 1)
                        {
                            // Add delay to movement of second, third etc. unit
                            if (i > 0) delay += 200;
                            
                            // Calculate default offset for each unit
                            var offset = Vector2.up;
                            
                            if (i % 2 == 0)
                            {
                                offset *= (i + 1) / 2.5f;
                            }
                            else
                            {
                                offset *= (i * -1) / 2.5f;;
                            }

                            // Get direction to object from neighbour node
                            if (AstarPath.active != null)
                            {
                                var nearestNode = AstarPath.active.GetNearest(movableObjectTask.Destination).node;
                                if (nearestNode != null)
                                {
                                    var reachableNodes = PathUtilities.GetReachableNodes(nearestNode);
                                    if (reachableNodes != null && reachableNodes.Count > 0)
                                    {
                                        var directionNode = reachableNodes[0];
                                        var direction = movableObjectTask.Destination - (Vector3) directionNode.position;
                                        
                                        // Rotate offset to match direction angle
                                        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction);
                                        offset = rotation * offset;
                                    }
                                }
                            }

                            // Give offset to the unit
                            taskUnit.Offset = offset;
                            
                            await UniTask.Delay(delay);
                        }

                        if (movableObjectTask.TaskProgress != RuntimeConstants.Enums.TaskProgress.InProgress ||
                            taskUnit.CurrentRunningTask != movableObjectTask)
                        {
                            continue;
                        }

                        taskUnit.MoveTo(movableObjectTask.Destination,
                            movableObjectTask.CurrentTaskObject, movableObjectTask.CurrentTaskObject.InteractionOffset);
                    }
                    catch (Exception ex)
                    {
                        Log.Gameplay.Error($"Error processing unit {i} in StartNextTaskPoint: {ex.Message}");
                        Log.Gameplay.Error(ex);
                        // Continue with next unit even if this one fails
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Gameplay.Error($"Error in StartNextTaskPoint: {ex.Message}");
                Log.Gameplay.Error(ex);
            }
        }
        
        public void TryStartQueuedTasks()
        {
            TryBeginQueuedTask().Forget();
        }

        private async UniTask<bool> TryBeginQueuedTask()
        {
            try
            {
                var tasks = _movableObjectTaskManager.Tasks.Values.ToList();
                Log.Gameplay.Info($"TryBeginQueuedTask: Processing {tasks.Count} total tasks");
                
                tasks.Reverse();
                var queuedTasksCount = tasks.Count(t => t.TaskProgress == RuntimeConstants.Enums.TaskProgress.Queued);
                Log.Gameplay.Info($"Found {queuedTasksCount} queued tasks to process");
                
                for (var i = tasks.Count - 1; i >= 0; i--)
                {
                    var task = tasks[i];
                    try
                    {
                        await UniTask.WaitUntil(() => !task.IsInWork);
                        
                        if (task.TaskProgress != RuntimeConstants.Enums.TaskProgress.Queued)
                        {
                            continue;
                        }

                        Log.Gameplay.Info($"Attempting to begin queued task: {task.GetHashCode()}");
                        
                        var freeUnits = GetFreeUnitsForTask(task);
                        if (freeUnits.Count < task.UnitCount)
                        {
                            continue;
                        }

                        var taskComplexObject = task.MainObjectView as ComplexObject;
                        var pathCheckTarget = taskComplexObject != null
                            ? taskComplexObject.transform.position
                            : task.Destination;

                        bool pathWalkable = false;
                        foreach (var unit in freeUnits)
                        {
                            var seeker = unit.GetComponent<Seeker>();
                            var pathResult = await _pathFindable.IsPathWalkable(unit.transform.position, pathCheckTarget, seeker);
                            if (pathResult.Item1)
                            {
                                pathWalkable = true;
                                break;
                            }
                        }

                        if (task.TaskProgress != RuntimeConstants.Enums.TaskProgress.Queued)
                        {
                            continue;
                        }

                        if (!pathWalkable)
                        {
                            Log.Gameplay.Info($"Failed to start queued task: {task.GetHashCode()} - path is currently blocked. Cancelling task.");

                            if (taskComplexObject != null)
                            {
                                if (_movableObjectTaskManager.CancelTask(task, interactionStarted: false,
                                        inputResources: taskComplexObject.spentResources))
                                {
                                    _gameResourcesSystem.AddResource(taskComplexObject.spentResources);
                                    taskComplexObject.SetInteractionPending(false);
                                }

                                continue;
                            }

                            var currentObj = task.MainObjectView as ObjectView;
                            if (currentObj != null)
                            {
                                // Never touch the object's interaction state while another task
                                // is running on it: cancelling the shared token/arrival lists
                                // aborts that live interaction and strands its units in Work.
                                if (!_movableObjectTaskManager.IsTaskStarted(currentObj))
                                {
                                    currentObj._cancellationTokenSource?.Cancel();
                                    currentObj.ResetInteractionState();
                                }

                                if (currentObj.GetCurrentInteractionNeedInputResources() && currentObj.resourcesSubtracted)
                                {
                                    _gameResourcesSystem.AddResource(currentObj.GetCurrentInputResources());
                                    currentObj.resourcesSubtracted = false;
                                }
                            }

                            _movableObjectTaskManager.CancelTask(task, interactionStarted: false, inputResources: null);

                            continue;
                        }

                        // The path check above spans multiple frames; units picked before it may
                        // have been taken by another task meanwhile. Re-validate on fresh state.
                        freeUnits = GetFreeUnitsForTask(task);
                        if (freeUnits.Count < task.UnitCount)
                        {
                            continue;
                        }

                        task.IsInWork = true;

                        if (await _movableObjectTaskManager.TryBeginTask(task, freeUnits))
                        {
                            Log.Gameplay.Info($"Successfully started queued task: {task.GetHashCode()}");
                            task.IsInWork = false;
                            return true;
                        }
                        Log.Gameplay.Info($"Failed to start queued task: {task.GetHashCode()} - insufficient units or conditions not met");
                        task.IsInWork = false;
                    }
                    catch (Exception ex)
                    {
                        task.IsInWork = false;
                        Log.Gameplay.Error($"Error processing task in TryBeginQueuedTask: {ex.Message}");
                        Log.Gameplay.Error(ex);
                        // Continue with next task instead of failing completely
                    }
                }

                Log.Gameplay.Info("TryBeginQueuedTask: No queued tasks could be started");
                return false;
            }
            catch (Exception ex)
            {
                Log.Gameplay.Error($"Error in TryBeginQueuedTask: {ex.Message}");
                Log.Gameplay.Error(ex);
                return false;
            }
        }
        #endregion
    }
}
