using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks;
using Cysharp.Threading.Tasks;
using R3;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController
{
    public class LevelTasksController : ILevelTasksController
    {
        private ILevelLoader _levelLoader;
        private IReloadController _reloadController;
        private IObjectResolver _objectResolver;
        
        private readonly CompositeDisposable _taskControllerDisposable = new ();
        private readonly CompositeDisposable _taskDisposable = new ();
        
        private readonly Dictionary<LevelTaskBase, LevelTask> _levelTasks = new ();

        public IReadOnlyDictionary<LevelTaskBase, LevelTask> LevelTasks => _levelTasks;

        public event Action<LevelTask> LevelTaskAdded = delegate { };
        public event Action AllTaskCompleted = delegate { };
        public event Action OnLevelDataLoaded;

        private byte _amountOfTaskCompleted;

        [Inject]
        private void Construct(ILevelLoader levelController,
            IReloadController reloadController,
            IObjectResolver objectResolver)
        {
            _levelLoader = levelController;

            _objectResolver = objectResolver;

            _reloadController = reloadController;
        }

        private void LevelDataLoaded(LevelBaseSO levelBaseSo)
        {
            if (_levelTasks.Count > 0)
                return;
                
            for (var i = 0; i < levelBaseSo.LevelTasks.Length; i++)
            {
                AddTask(levelBaseSo.LevelTasks[i]);
            }
            
            OnLevelDataLoaded?.Invoke();
        }

        private void TaskStatusChangedHandler(LevelTaskData levelTaskData)
        {
            if (levelTaskData.TaskStatus != LevelTaskStatus.Done)
            {
                return;
            }

            if (levelTaskData.IsBonusTask)
            {
                return;
            }

            if (++_amountOfTaskCompleted >= _levelTasks.Count)
            {
                AllTaskCompleted?.Invoke();
            }
        }

        private void ClearTaskStatusHandler()
        {
            foreach (var levelTask in _levelTasks.Values)
            {
                levelTask.TaskStatusChanged -= TaskStatusChangedHandler;
            }
        }

        public void ChangeTaskStatus(LevelTaskBase levelTaskBase, LevelTaskStatus levelTaskStatus)
        {
            _levelTasks[levelTaskBase].ChangeTaskStatus(levelTaskStatus);
        }

        public void ChangeTaskProgress(LevelTaskBase taskToChange, short amount)
        {
            _levelTasks[taskToChange].ChangeProgress(amount);
        }

        public void ChangeTaskVisibility(LevelTaskBase taskToChange, bool visibility)
        {
            _levelTasks[taskToChange].ChangeTaskVisibility(visibility);
        }

        public LevelTask GetLevelTask(LevelTaskBase taskBase)
        {
            return _levelTasks[taskBase];
        }

        public void AddTask(LevelTaskBase taskBase)
        {
            if (_levelTasks.ContainsKey(taskBase)) // todo resolve
                return;
            
            var levelTask = taskBase.GetLevelTask();
            
            _objectResolver.Inject(levelTask);
            
            _taskDisposable.Add(levelTask);
            
            _levelTasks.Add(taskBase, levelTask);
            
            LevelTaskAdded?.Invoke(levelTask);
            
            levelTask.TaskStatusChanged += TaskStatusChangedHandler;
            
            levelTask.Setup();
        }

        public void Dispose()
        {
            ClearTaskStatusHandler();
            
            _taskControllerDisposable.Dispose();
            
            _taskDisposable.Dispose();
            
            _reloadController.RemoveReloadableObject(this);
        }

        public UniTask Reload()
        {
            ClearTaskStatusHandler();
            
            _levelTasks.Clear();
            
            _taskDisposable.Clear();

            _amountOfTaskCompleted = 0;
            
            return UniTask.CompletedTask;
        }

        public UniTask Load()
        {
            _levelLoader.LevelBaseSO
                .Skip(1)
                .Subscribe(LevelDataLoaded)
                .AddTo(_taskControllerDisposable);
            
            _reloadController.AddReloadableObject(this);
            
            return UniTask.CompletedTask;
        }
    }
}