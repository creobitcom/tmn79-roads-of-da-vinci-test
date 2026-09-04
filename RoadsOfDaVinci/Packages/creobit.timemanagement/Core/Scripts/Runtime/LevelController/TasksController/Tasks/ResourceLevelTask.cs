using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks
{
    public abstract class ResourceLevelTask : LevelTaskBase
    {
        public override LevelTaskType TaskType => LevelTaskType.ResourceTask;
        
        [field: SerializeField] 
        public ResourceBaseSO ObservableResource { get; private set; }

        protected abstract LevelTask ProvideTask();

        public override LevelTask GetLevelTask()
        {
            return ProvideTask();
        }
    }
}