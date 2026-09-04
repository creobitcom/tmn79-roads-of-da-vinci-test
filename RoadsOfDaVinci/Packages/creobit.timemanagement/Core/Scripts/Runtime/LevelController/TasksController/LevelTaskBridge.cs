using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController
{
    public class LevelTaskBridge : MonoBehaviour
    {
        [SerializeField]
        private UltEvent _allTaskCompleted; 
        
        [SerializeField]
        private TaskCompleteWrapper[] _taskCompletedEvents;
        
        private ILevelTasksController _levelTasksController;
        
        [Inject]
        private void Construct(ILevelTasksController levelTasksController)
        {
            _levelTasksController = levelTasksController; 
        }

        private void Start()
        {
            _levelTasksController.AllTaskCompleted += RaiseAllTaskCompletedEvent;

            foreach (var taskCompletedEvents in _taskCompletedEvents)
            {
                _levelTasksController.GetLevelTask(taskCompletedEvents.LevelTaskBase)
                    .AddTaskCompleted(() => taskCompletedEvents.TaskCompletedEvent?.Invoke());
            }
        }

        private void RaiseAllTaskCompletedEvent()
        {
            _allTaskCompleted?.Invoke();
        }

        public void AddTask(LevelTaskBase taskBase)
        {
            _levelTasksController.AddTask(taskBase);

            foreach (var taskCompleteWrapper in _taskCompletedEvents)
            {
                if (!taskCompleteWrapper.LevelTaskBase.Equals(taskBase))
                {
                    continue;
                }
                
                _levelTasksController.GetLevelTask(taskBase).AddTaskCompleted(() => taskCompleteWrapper.TaskCompletedEvent?.Invoke());
            }
        }

        public void ChangeTaskStatus(LevelTaskBase taskToChange, LevelTaskStatus statusToSet)
        {
            _levelTasksController.ChangeTaskStatus(taskToChange, statusToSet);
        }

        public void ChangeTaskVisibility(LevelTaskBase taskToChange, bool visibility)
        {
            _levelTasksController.ChangeTaskVisibility(taskToChange, visibility);
        }

        public void AddTaskProgress(LevelTaskBase taskToChange)
        {
            _levelTasksController.ChangeTaskProgress(taskToChange, 1);
        }
        
        public void SubtractTaskProgress(LevelTaskBase taskToChange)
        {
            _levelTasksController.ChangeTaskProgress(taskToChange, -1);
        }

        private void OnDestroy()
        {
            _levelTasksController.AllTaskCompleted -= RaiseAllTaskCompletedEvent;
        }

        [Serializable]
        private class TaskCompleteWrapper
        {
            [field: SerializeField]
            public LevelTaskBase LevelTaskBase { get; private set; }

            [field: SerializeField]
            public UltEvent TaskCompletedEvent { get; private set; }
        }
    }
}