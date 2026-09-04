using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors
{
    public class UniqueTask : LevelTask
    {
        private ushort _currentAmount;

        private readonly LevelTaskData _levelTaskData;

        private bool _showSingleAmount;
        
        public UniqueTask(LevelTaskData levelTaskData, bool showSingleAmount = false, bool showOnlyAmount = false)
            : base(levelTaskData, showOnlyAmount)
        {
            _levelTaskData = levelTaskData;
            _showSingleAmount = showSingleAmount;
            _showOnlyAmount = showOnlyAmount;
        }

        public override void ChangeProgress(short amount)
        {
            _currentAmount += (ushort) Mathf.Max(0, amount);

            CheckProgress(_currentAmount);
        }

        protected override string GetProgressDisplay(ushort currentAmount)
        {
            return _levelTaskData.AmountToCompleteTask <= 1 ? _showSingleAmount ? "1" : string.Empty : base.GetProgressDisplay(currentAmount);
        }
    }
}