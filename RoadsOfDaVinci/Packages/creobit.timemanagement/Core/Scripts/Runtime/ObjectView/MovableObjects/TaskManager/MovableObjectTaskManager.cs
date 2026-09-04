using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static _8floor.TimeManagement.Core.Scripts.Runtime.Utils.RuntimeConstants.Enums;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager
{
    public class MovableObjectTaskManager : IMovableObjectTaskManager
    {
        private readonly IMovableObjectTaskBadgeController _movableObjectTaskBadgeController;
        private readonly MovableObjectController _movableObjectController;

        private readonly OrderedDictionary<ushort, MovableObjectTask> _tasks = new();
        private readonly List<MovableObjectTask> _taskHistory = new();
        private readonly List<MovingUnit> _unitsInMove = new();
        private CancellationTokenSource _cancellationTokenSource;

        private ushort _taskCounter;

        public IReadOnlyOrderedDictionary<ushort, MovableObjectTask> Tasks => _tasks;
        public IReadOnlyList<MovableObjectTask> TaskHistory => _taskHistory;

        public event Action<ResourceAmount[]> TaskCancelled = delegate { };

        public bool CanCancelTaskDuringInteraction { get; private set; }

        public MovableObjectTaskManager(IMovableObjectTaskBadgeController movableObjectTaskBadgeController,
            MovableObjectController movableObjectController)
        {
            _movableObjectTaskBadgeController = movableObjectTaskBadgeController;
            _movableObjectController = movableObjectController;
        }

        private ushort GetTaskID()
        {
            _taskCounter += 1;

            return _taskCounter;
        }

        private void ChangeTaskProgress(ushort taskId, TaskProgress taskProgress)
        {
            var requestedTask = GetTaskById(taskId);

            if (requestedTask == null)
            {
                Log.Gameplay.Info($"Attempted to change progress for non-existent task with ID {taskId} to {taskProgress}");
                return;
            }

            var oldProgress = requestedTask.TaskProgress;
            Log.Gameplay.Info($"Changing task {taskId} progress from {oldProgress} to {taskProgress}");
            requestedTask.TaskProgress = taskProgress;
            UpdateBadges();
            Log.Gameplay.Info($"Task {taskId} progress updated successfully to {taskProgress}");
        }

        public bool IsTaskStarted(ITaskObject objectView)
        {
            return _tasks.Values.Any(movableObjectTask => movableObjectTask.MainObjectView.Equals(objectView)
                                                          && IsObjectTaskRunning(movableObjectTask.TaskProgress));
        }

        public TaskProgress? GetTaskProgress(ITaskObject objectView)
        {
            foreach (var task in _tasks.Values)
            {
                if (task.MainObjectView.Equals(objectView))
                {
                    return task.TaskProgress;
                }
            }

            return null;
        }

        public bool TryRetargetTask(ITaskObject oldObject, ITaskObject newObject, bool interactionStarted)
        {
            if (oldObject == null || newObject == null || ReferenceEquals(oldObject, newObject))
            {
                return false;
            }

            foreach (var task in _tasks.Values)
            {
                if (!task.MainObjectView.Equals(oldObject))
                {
                    continue;
                }

                // Начатое взаимодействие не переводим: объект уже отдаёт юниту результат, и подмена
                // в этот момент означала бы выдачу двух результатов за одно взаимодействие.
                if (task.TaskProgress != TaskProgress.Queued
                    && (task.TaskProgress != TaskProgress.InProgress || interactionStarted))
                {
                    return false;
                }

                if (!task.TryReplaceTaskObject(oldObject, newObject))
                {
                    return false;
                }

                if (ReferenceEquals(oldObject.CurrentTask, task))
                {
                    oldObject.CurrentTask = null;
                }

                newObject.CurrentTask = task;
                UpdateBadges();

                // Только если подменили точку, к которой юниты идут прямо сейчас. Переиздавать
                // маршрут к текущей цели нельзя: пришедший юнит ждёт освобождения объекта, и второй
                // приход запустил бы взаимодействие повторно.
                if (task.TaskProgress == TaskProgress.InProgress
                    && ReferenceEquals(task.CurrentTaskObject, newObject))
                {
                    // MoveTo сбрасывает движение только при той же цели, поэтому на новом объекте он
                    // переписывает маршрут, не останавливая юнита.
                    _movableObjectController.StartNextTaskPoint(task).Forget();
                }

                return true;
            }

            return false;
        }

        public void ReplaceHomePointInTasks(StaticObjectView oldPoint, StaticObjectView newPoint)
        {
            foreach (var task in _tasks.Keys)
            {
                _tasks[task].UnitsFrom = _tasks[task].UnitsFrom
                    .Select(homePoint => homePoint == oldPoint
                        ? newPoint
                        : homePoint)
                    .ToList();
            }
        }

        private static bool IsObjectTaskRunning(TaskProgress taskProgress)
        {
            return taskProgress == TaskProgress.InProgress;
        }

        private void AddTask(MovableObjectTask movableObjectTask, bool prepend = false)
        {
            movableObjectTask.TaskId = GetTaskID();
            Log.Gameplay.Info($"Adding new task with ID {movableObjectTask.TaskId}, prepend: {prepend}, total tasks: {_tasks.Count}");

            if (prepend)
            {
                _tasks.Prepend(movableObjectTask.TaskId, movableObjectTask);
                Log.Gameplay.Info($"Task {movableObjectTask.TaskId} prepended to task queue");
            }
            else
            {
                _tasks.Add(movableObjectTask.TaskId, movableObjectTask);
                Log.Gameplay.Info($"Task {movableObjectTask.TaskId} added to end of task queue");
            }
            
            Log.Gameplay.Info($"Task {movableObjectTask.TaskId} added successfully, new total tasks: {_tasks.Count}");
        }

        private void RemoveTask(ushort taskId)
        {
            if (!_tasks.TryGetValue(taskId, out var movableObjectTask))
            {
                Log.Gameplay.Info($"Attempted to remove non-existent task with ID {taskId}");
                return;
            }

            Log.Gameplay.Info($"Removing task {taskId} with progress {movableObjectTask.TaskProgress}, moving to history");
            _taskHistory.Add(movableObjectTask);

            _tasks.Remove(taskId);
            Log.Gameplay.Info($"Task {taskId} removed successfully, remaining tasks: {_tasks.Count}");
        }

        private void CancelTask(ushort taskId)
        {
            Log.Gameplay.Info($"Cancelling task {taskId}");
            var cancelledTask = GetTaskById(taskId);
            ChangeTaskProgress(taskId, TaskProgress.Cancelled);

            RemoveTask(taskId);
            ClearTaskObjectReferences(cancelledTask);
            Log.Gameplay.Info($"Task {taskId} cancelled successfully");
        }

        private static void ClearTaskObjectReferences(MovableObjectTask task)
        {
            if (task == null)
            {
                return;
            }

            // Objects otherwise point at their last task forever: a stale reference made a
            // click on a building cancel a FOREIGN/dead task (refunding wrong resources) and
            // let COC interval callbacks yank units that already belong to another task.
            foreach (var taskObject in task.TaskObject)
            {
                if (taskObject != null && ReferenceEquals(taskObject.CurrentTask, task))
                {
                    taskObject.CurrentTask = null;
                }
            }
        }

        private void RemoveUnitsInMove(ushort taskId)
        {
            if (!_tasks.ContainsKey(taskId))
            {
                return;
            }

            for (var i = _unitsInMove.Count - 1; i >= 0; i--)
            {
                if (_unitsInMove[i].TaskId == taskId)
                {
                    _unitsInMove.RemoveAt(i);
                }
            }

            foreach (var unit in _tasks[taskId].Units)
            {
                if (unit.State.Value == UnitState.Idle)
                {
                    // Already-Idle units (they finished earlier and waited for the rest of the
                    // task) get no state-change event here — dispatch them home explicitly.
                    _movableObjectController.MoveToHomePointAfterDelay(unit).Forget();
                }
                else
                {
                    unit.State.Value = UnitState.Idle;
                }
            }
        }

        public bool CancelTask(MovableObjectTask task, bool interactionStarted, ResourceAmount[] inputResources)
        {
            if (task == null || interactionStarted && !CanCancelTaskDuringInteraction)
            {
                return false;
            }

            if (task.TaskProgress != TaskProgress.InProgress && task.TaskProgress != TaskProgress.Queued)
            {
                return false;
            }

            CancelTask(task.TaskId);

            for (var i = _unitsInMove.Count - 1; i >= 0; i--)
            {
                if (_unitsInMove[i].TaskId != task.TaskId)
                {
                    continue;
                }

                _unitsInMove[i].MovableObjectView.CancelTask();

                _unitsInMove.RemoveAt(i);
            }

            try
            {
                TaskCancelled?.Invoke(inputResources ?? Array.Empty<ResourceAmount>());
            }
            catch (Exception exception)
            {
                Log.Gameplay.Error($"TaskCancelled handler failed: {exception.Message}");
                Log.Gameplay.Error(exception);
            }

            _movableObjectController.TryStartQueuedTasks();

            return true;
        }

        public List<MovableObjectTask> GetTasksByProgress(TaskProgress taskProgress)
        {
            var tasksWithProgress = new List<MovableObjectTask>();

            foreach (var movableObjectTask in _tasks.Values)
            {
                if (movableObjectTask.TaskProgress == taskProgress)
                {
                    tasksWithProgress.Add(movableObjectTask);
                }
            }

            return tasksWithProgress;
        }

        public MovableObjectTask GetTaskByUnit(MovableObjectView unit)
        {
            if (unit.CurrentRunningTask != null
                && unit.CurrentRunningTask.Units.Contains(unit)
                && unit.CurrentRunningTask.TaskProgress is TaskProgress.InProgress or TaskProgress.RunHome)
            {
                return unit.CurrentRunningTask;
            }
            
            foreach (var movableObjectTask in _tasks.Values)
            {
                if (movableObjectTask.Units != null
                    && movableObjectTask.Units.Contains(unit)
                    && movableObjectTask.TaskProgress is TaskProgress.InProgress or TaskProgress.RunHome)
                {
                    return movableObjectTask;
                }
            }

            return null;
        }

        public bool TryCompleteTask(MovableObjectTask task)
        {
            // An interaction end fires EndUnitInteract once per unit, and every unit's Idle
            // transition lands here. Advance the task only when the LAST unit has gone Idle —
            // otherwise a multi-unit task advances its object index once per unit, skipping
            // sequence objects or completing/RunHome-ing the task prematurely.
            if (task.Units != null && task.Units.Any(taskUnit => taskUnit.State.Value != UnitState.Idle))
            {
                Log.Gameplay.Info($"Task {task.TaskId}: waiting for remaining units to go idle before advancing");
                return false;
            }

            Log.Gameplay.Info($"Attempting to complete task {task.TaskId}, current object index: {task.CurrentTaskObjectIndex}, total objects: {task.TaskObject.Count}");
            task.CurrentTaskObjectIndex++;

            if (task.CurrentTaskObjectIndex >= task.TaskObject.Count)
            {
                Log.Gameplay.Info($"Task {task.TaskId} completed all objects, finalizing task completion");
                if (task.IsNeedToReturn)
                {
                    ChangeTaskProgress(task.TaskId, TaskProgress.RunHome);
                    foreach (var unit in task.Units)
                    {
                        unit.State.Value = UnitState.ReturnHome;
                        unit.CurrentRunningTask = task;
                        _movableObjectController.MoveToHomePoint(unit, onEndMove: TryCompleteTaskWithReturnUnits);
                    }
                    return false;
                }
                else
                {
                    CompleteTask(task.TaskId);
                    return true;
                }
            }
            else
            {
                foreach (var unit in task.Units)
                {
                    unit.State.Value = UnitState.Work;
                }
                Log.Gameplay.Info($"Task {task.TaskId} advancing to next object index {task.CurrentTaskObjectIndex}, continuing task execution");
                _movableObjectController.StartNextTaskPoint(task).Forget();
                return false;
            }
        }

        public void TryCompleteTaskWithReturnUnits(MovableObjectView unit)
        {
            var task = unit.CurrentRunningTask;
            if (task == null || task.TaskProgress != TaskProgress.RunHome)
            {
                // Unsubscribe even when there is no live RunHome task, otherwise the handler
                // leaks and fires on unrelated moves of this unit later, adding it to the
                // ReturnedUnits of whatever task it runs by then.
                unit.MovableObject.EndMove -= TryCompleteTaskWithReturnUnits;
                return;
            }
            task.ReturnedUnits.Add(unit);
            if (task.ReturnedUnits.Count >= task.UnitCount)
            {
                CompleteTask(task.TaskId);
            }

            unit.MovableObject.EndMove -= TryCompleteTaskWithReturnUnits;
        }

        private void CompleteTask(ushort taskId)
        {
            Log.Gameplay.Info($"Completing task {taskId} - setting status to Completed and cleaning up");
            var completedTask = GetTaskById(taskId);
            ChangeTaskProgress(taskId, TaskProgress.Completed);

            RemoveUnitsInMove(taskId);

            RemoveTask(taskId);
            ClearTaskObjectReferences(completedTask);
            Log.Gameplay.Info($"Task {taskId} completion process finished successfully");
        }

        private MovableObjectTask GetTaskById(ushort taskId)
        {
            return !_tasks.TryGetValue(taskId, out var requestedTask)
                ? null
                : requestedTask;
        }

        public async UniTask<MovableObjectTask> RegisterNewTask(
            List<StaticObjectView> possibleTransforms,
            ObjectView mainObjectView,
            List<ITaskObject> objectNeedSequence,
            ResourceBaseSO[] specialResources,
            bool needToReturn = false,
            List<MovableObjectView> specificUnits = null,
            bool waitUntilStarted = true)
        {
            Log.Gameplay.Info($"Registering new task for object {mainObjectView.name} with {objectNeedSequence.Count} task objects");
            var movableObjectTask = new MovableObjectTask(
                mainObjectView.GetCurrentUnitTypeCount().Sum(typeCount => typeCount.count),
                null,
                objectNeedSequence,
                specialResources,
                possibleTransforms,
                mainObjectView.GetCurrentUnitTypeCount(),
                isNeedToReturn: needToReturn);

            AddTask(movableObjectTask);
            UpdateBadges();

            var unitsForTask = specificUnits ?? _movableObjectController.GetFreeUnitsForTask(movableObjectTask);

            Log.Gameplay.Info($"Found {unitsForTask.Count} free units for task {movableObjectTask.TaskId}");

            foreach (var taskObject in movableObjectTask.TaskObject)
            {
                taskObject.CurrentTask = movableObjectTask;
            }

            TryBeginTask(movableObjectTask, unitsForTask).Forget();

            if (!waitUntilStarted)
            {
                Log.Gameplay.Info($"Task {movableObjectTask.TaskId} registration completed while queued");
                return movableObjectTask;
            }

            Log.Gameplay.Info($"Waiting for task {movableObjectTask.TaskId} to become InProgress");
            await UniTask.WaitUntil(() => movableObjectTask.TaskProgress != TaskProgress.Queued);

            if (movableObjectTask.TaskProgress != TaskProgress.InProgress)
            {
                Log.Gameplay.Info($"Task {movableObjectTask.TaskId} registration ended with {movableObjectTask.TaskProgress}");
                return null;
            }

            Log.Gameplay.Info($"Task {movableObjectTask.TaskId} registration completed successfully");
            return movableObjectTask;
        }

        public async UniTask<MovableObjectTask> RegisterNewTask(List<StaticObjectView> from,
            ITaskObject mainTaskObject,
            List<ITaskObject> objectNeedSequence,
            BuildingSettings buildingSettings,
            bool needToReturn = false,
            List<MovableObjectView> specificUnits = null) 
        {
            Log.Gameplay.Info($"Registering new task with building settings for {objectNeedSequence.Count} task objects");
            var movableObjectTask = new MovableObjectTask(
                buildingSettings.UnitTypeCounts.Sum(typeCount => typeCount.count),
                null,
                objectNeedSequence,
                null,
                from,
                buildingSettings.UnitTypeCounts,
                isNeedToReturn: needToReturn); 

            AddTask(movableObjectTask);
            UpdateBadges();

            var freeUnits = specificUnits ?? _movableObjectController.GetFreeUnitsForTask(movableObjectTask);

            Log.Gameplay.Info($"Found {freeUnits.Count} free units for building task {movableObjectTask.TaskId}");

            foreach (var taskObject in movableObjectTask.TaskObject)
            {
                taskObject.CurrentTask = movableObjectTask;
            }

            TryBeginTask(movableObjectTask, freeUnits).Forget();

            Log.Gameplay.Info($"Waiting for building task {movableObjectTask.TaskId} to become InProgress");
            await UniTask.WaitUntil(() => movableObjectTask.TaskProgress != TaskProgress.Queued);

            if (movableObjectTask.TaskProgress != TaskProgress.InProgress)
            {
                Log.Gameplay.Info($"Building task {movableObjectTask.TaskId} registration ended with {movableObjectTask.TaskProgress}");
                return null;
            }

            Log.Gameplay.Info($"Building task {movableObjectTask.TaskId} registration completed successfully");
            return movableObjectTask;
        }

        public async UniTask<bool> TryBeginTask(MovableObjectTask movableObjectTask, List<MovableObjectView> units)
        {
            if (movableObjectTask.TaskProgress != TaskProgress.Queued)
            {
                return false;
            }

            Log.Gameplay.Info($"Attempting to begin task {movableObjectTask.TaskId} with {units?.Count ?? 0} provided units");

            var candidateUnits = units ?? _movableObjectController.GetFreeUnitsForTask(movableObjectTask);

            var assignableUnits = candidateUnits
                .Where(taskUnit => taskUnit.State.Value != UnitState.Work
                                   && taskUnit.State.Value != UnitState.ReturnHome)
                .ToList();

            Log.Gameplay.Info($"Task {movableObjectTask.TaskId} has {assignableUnits.Count} available units (required: {movableObjectTask.UnitCount})");

            // All-or-nothing: partially assigning units used to strand them in Work forever —
            // the task stayed Queued, no movement was issued, and GetFreeUnits never saw them
            // again (it only picks Idle units), so the task never started and the units never
            // left the base.
            if (assignableUnits.Count == 0 || assignableUnits.Count < movableObjectTask.UnitCount)
            {
                Log.Gameplay.Info($"Task {movableObjectTask.TaskId} stays queued - not enough idle units, no units reserved");
                return false;
            }

            movableObjectTask.Units = assignableUnits;

            foreach (var taskUnit in assignableUnits)
            {
                if (taskUnit.CurrentStandingPoint != null)
                {
                    taskUnit.CurrentStandingPoint.FreePoint();
                    taskUnit.CurrentStandingPoint = null;
                    taskUnit.modelTransform.gameObject.SetActive(true);
                }

                Log.Gameplay.Info($"Assigning unit {taskUnit.name} to task {movableObjectTask.TaskId}");
                taskUnit.returnHomeTokenSource?.Cancel();
                taskUnit.State.Value = UnitState.Work;
                taskUnit.CurrentRunningTask = movableObjectTask;

                _unitsInMove.Add(new MovingUnit()
                {
                    MovableObjectView = taskUnit,

                    TaskId = movableObjectTask.TaskId
                });
            }

            Log.Gameplay.Info($"Task {movableObjectTask.TaskId} has sufficient units, changing to InProgress");
            ChangeTaskProgress(movableObjectTask.TaskId, TaskProgress.InProgress);

            foreach (var taskObject in movableObjectTask.TaskObject)
            {
                taskObject.CurrentTask = movableObjectTask;
            }

            Log.Gameplay.Info($"Starting movement for task {movableObjectTask.TaskId} units to first task point");
            await _movableObjectController.StartNextTaskPoint(movableObjectTask);

            var taskStarted = movableObjectTask.TaskProgress == TaskProgress.InProgress;
            Log.Gameplay.Info($"Task {movableObjectTask.TaskId} begun: {taskStarted}");
            return taskStarted;
        }

        public void UpdateBadges()
        {
            try
            {
                _movableObjectTaskBadgeController.UpdateTaskBadgeOrders(_tasks.Values.ToList());
            }
            catch (Exception exception)
            {
                // Badge rendering must never break the task lifecycle: a throw here used to
                // propagate into ChangeTaskProgress and abort CompleteTask/CancelTask cleanup,
                // leaving ghost tasks and units stuck in Work/ReturnHome.
                Log.Gameplay.Error($"UpdateBadges failed: {exception.Message}");
                Log.Gameplay.Error(exception);
            }
        }

        public UniTask Reload()
        {
            _tasks.Clear();
            _taskHistory.Clear();
            _unitsInMove.Clear();
            _taskCounter = 0;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _tasks.Clear();
            _taskHistory.Clear();
            _taskCounter = 0;
        }


        private sealed class MovingUnit
        {
            public ushort TaskId;

            public MovableObjectView MovableObjectView;
        }
    }
}
