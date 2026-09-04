using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges
{
    public interface IMovableObjectTaskBadgeController : ILoadUnit, IReloadable, IDisposable
    {
        public void SetTaskBadge(MovableObjectTask movableObjectTask, int queuedTaskCount);

        public void UpdateTaskBadgeOrder();
        public void UpdateTaskBadgeOrders(List<MovableObjectTask> tasks);
    }
}