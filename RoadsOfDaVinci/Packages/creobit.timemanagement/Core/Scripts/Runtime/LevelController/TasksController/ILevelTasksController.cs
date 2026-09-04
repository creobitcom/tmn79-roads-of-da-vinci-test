using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController
{
    public interface ILevelTasksController : ILoadUnit, IDisposable, IReloadable
    {
        public IReadOnlyDictionary<LevelTaskBase, LevelTask> LevelTasks { get; }
        public event Action<LevelTask> LevelTaskAdded;
        public event Action AllTaskCompleted;
        public event Action OnLevelDataLoaded;
        public void AddTask(LevelTaskBase levelTaskBase);
        public void ChangeTaskStatus(LevelTaskBase levelTaskBase, LevelTaskStatus levelTaskStatus);
        public void ChangeTaskProgress(LevelTaskBase taskToChange, short amount);
        public void ChangeTaskVisibility(LevelTaskBase taskToChange, bool visibility);
        public LevelTask GetLevelTask(LevelTaskBase taskBase);
    }

    public enum LevelTaskType
    {
        ResourceTask,
        UniqueTask
    }

    public enum LevelTaskStatus
    {
        Inactive,
        InProgress,
        Done
    }

}
