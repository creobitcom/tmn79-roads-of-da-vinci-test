using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors
{
    public class SpendResourceTask : ResourceTask
    {
        private IMovableObjectTaskManager _movableObjectTaskManager;
        
        private ushort _spentAmount;
        
        [Inject]
        private void Construct(IObjectViewController objectViewController)
        {
            _movableObjectTaskManager = objectViewController.GetMovableObjectController().GetTaskManager();
        }
        
        public SpendResourceTask(LevelTaskData levelTaskData, ResourceBaseSO observableResource, bool showOnlyAmount = false)
            : base(levelTaskData, observableResource, showOnlyAmount)
        { }

        private void TaskCancelledHandler(ResourceAmount[] inputResources)
        {
            foreach (var resourceAmount in inputResources)
            {
                if (!resourceAmount.Resource.Equals(ObservableResource))
                {
                    continue;
                }
                
                ChangeResourceProgress((short) -resourceAmount.Amount);
            }
        }

        protected override void SetupTask()
        {
            base.SetupTask();
            
            _movableObjectTaskManager.TaskCancelled += TaskCancelledHandler;
        }

        protected override void DisposeTask()
        {
            base.DisposeTask();
            
            _movableObjectTaskManager.TaskCancelled -= TaskCancelledHandler;
        }

        protected override void ChangeResourceProgress(short amount)
        {
            _spentAmount = (ushort) Mathf.Max(0, _spentAmount + amount);

            CheckProgress(_spentAmount);
        }

        protected override void ObservableResourceChanged(int newValue, int oldValue)
        {
            if (LevelTaskData.TaskStatus != LevelTaskStatus.InProgress)
            {
                return;
            }
            
            var difference = oldValue - newValue;
            
            if (difference > 0)
            {
                ChangeResourceProgress((short) difference);
            }
        }
    }
}