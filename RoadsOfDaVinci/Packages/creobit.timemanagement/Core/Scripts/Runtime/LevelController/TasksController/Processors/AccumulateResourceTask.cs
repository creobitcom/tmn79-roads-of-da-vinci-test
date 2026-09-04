using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors
{
    public class AccumulateResourceTask : ResourceTask
    {
        private IMovableObjectTaskManager _movableObjectTaskManager;
        
        private ushort _accumulatedAmount;
        private bool _resourcesReturned;
        
        [Inject]
        private void Construct(IObjectViewController objectViewController)
        {
            _movableObjectTaskManager = objectViewController.GetMovableObjectController().GetTaskManager();
        }

        public AccumulateResourceTask(LevelTaskData levelTaskData, ResourceBaseSO observableResource, bool showOnlyAmount = false) 
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

                _resourcesReturned = true;
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
            _accumulatedAmount = (ushort) Mathf.Max(0, _accumulatedAmount + amount);
            
            CheckProgress(_accumulatedAmount);
        }

        protected override void ObservableResourceChanged(int newValue, int oldValue)
        {
            if (LevelTaskData.TaskStatus != LevelTaskStatus.InProgress)
            {
                return;
            }

            var difference = newValue - oldValue;

            if (difference <= 0)
            {
                return;
            }

            if (_resourcesReturned)
            {
                _resourcesReturned = false;
                    
                return;
            }
                
            ChangeResourceProgress((short) difference);
        }
    }
}