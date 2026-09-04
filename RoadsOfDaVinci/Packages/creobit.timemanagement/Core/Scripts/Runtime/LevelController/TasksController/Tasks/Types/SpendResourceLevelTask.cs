using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks.Types
{
    public class SpendResourceLevelTask : ResourceLevelTask
    {
        protected override LevelTask ProvideTask()
        {
            return new SpendResourceTask(LevelTaskData, ObservableResource);
        }

        public override LevelTask GetLevelTask(bool showAmountAlways, bool showOnlyAmount = false)
        {
            return new SpendResourceTask(LevelTaskData, ObservableResource);
        }
    }
}