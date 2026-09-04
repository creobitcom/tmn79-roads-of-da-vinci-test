using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks.Types
{
    public class UniqueLevelTask : LevelTaskBase
    {
        public override LevelTaskType TaskType => LevelTaskType.UniqueTask;
        public override LevelTask GetLevelTask()
        {
            return new UniqueTask(LevelTaskData, _showAmountAlways, _showOnlyAmount);
        }

        public override LevelTask GetLevelTask(bool showAmountAlways, bool showOnlyAmount = false)
        {
            return new UniqueTask(LevelTaskData, showAmountAlways, showOnlyAmount);
        }
    }
}