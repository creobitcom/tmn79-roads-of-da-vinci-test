using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors
{
    public class BalanceResourceTask : ResourceTask
    {
        private ushort _currentBalance;
        
        public BalanceResourceTask(LevelTaskData levelTaskData, ResourceBaseSO observableResource, bool showOnlyAmount =false) 
            : base(levelTaskData, observableResource, showOnlyAmount)
        { }
        
        protected override void ChangeResourceProgress(short amount)
        {
            _currentBalance = (ushort) Mathf.Max(0, _currentBalance + amount);

            CheckProgress(_currentBalance);
        }
        
        protected override string GetProgressDisplay(ushort currentAmount)
        {
            return _showOnlyAmount ? $"{Mathf.Max(0, LevelTaskData.AmountToCompleteTask - currentAmount)}" :
                $"{(ushort)Mathf.Min(currentAmount, LevelTaskData.AmountToCompleteTask)}/{LevelTaskData.AmountToCompleteTask}";
        }
        

        protected override void ObservableResourceChanged(int newValue, int oldValue)
        {
            if (LevelTaskData.TaskStatus != LevelTaskStatus.InProgress)
            {
                return;
            }

            ChangeResourceProgress((short) (newValue - _currentBalance));
        }
    }
}