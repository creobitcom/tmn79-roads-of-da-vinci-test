using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController
{
    public interface ITaskProvider
    {
        public LevelTask GetLevelTask();
    }
}