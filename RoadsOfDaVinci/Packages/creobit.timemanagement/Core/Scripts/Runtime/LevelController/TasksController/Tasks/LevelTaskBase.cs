using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks
{
    public abstract class LevelTaskBase : ScriptableObject, ITimeManagerSO, ITaskProvider
    {
        public abstract LevelTaskType TaskType { get; }
        
        [field: SerializeField] 
        [field: InlineProperty]
        public LevelTaskData LevelTaskData { get; private set; }
        [SerializeField] protected bool _showAmountAlways;
        [SerializeField] protected bool _showOnlyAmount;

        public abstract LevelTask GetLevelTask();
        public abstract LevelTask GetLevelTask(bool showAmountAlways, bool showOnlyAmount = false);
    }
}