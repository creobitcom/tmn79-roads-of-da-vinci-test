using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks.Types
{
    public class AccumulateResourceLevelTask : ResourceLevelTask
    {
        protected override LevelTask ProvideTask()
        {
            return new AccumulateResourceTask(LevelTaskData, ObservableResource);
        }

        public override LevelTask GetLevelTask(bool showAmountAlways, bool showOnlyAmount = false)
        {
            return new AccumulateResourceTask(LevelTaskData, ObservableResource);
        }
    }
}