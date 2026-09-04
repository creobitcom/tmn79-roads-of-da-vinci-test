using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Cysharp.Threading.Tasks;
using static _8floor.TimeManagement.Core.Scripts.Runtime.Utils.RuntimeConstants.Enums;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager
{
    public interface IMovableObjectTaskManager : IDisposable, IReloadable
    {
        public event Action<ResourceAmount[]> TaskCancelled;

        /// <summary>
        /// Registers a new task for a movable object, including task-specific requirements and unit allocation.
        /// </summary>
        /// <param name="possibleTransforms">List of potential starting points for the task's units.</param>
        /// <param name="mainObjectView">The main object associated with the task.</param>
        /// <param name="objectNeedSequence">Sequence of objects required to complete the task.</param>
        /// <param name="specialResources">Array of special resources required to execute the task.</param>
        /// <returns>A task that resolves to a <see cref="MovableObjectTask"/> representing the registered task.</returns>
        public UniTask<MovableObjectTask> RegisterNewTask(List<StaticObjectView> possibleTransforms,
            ObjectView mainObjectView,
            List<ITaskObject> objectNeedSequence,
            ResourceBaseSO[] specialResources,
            bool isNeedToReturn = false,
            List<MovableObjectView> specificUnits = null,
            bool waitUntilStarted = true);

        /// <summary>
        /// Registers a new task for a movable object, specifying the origin points, main task object, required sequence of task objects, and building-specific settings.
        /// </summary>
        /// <param name="from">List of possible starting points for the units assigned to the task.</param>
        /// <param name="mainTaskObject">The primary object associated with the task.</param>
        /// <param name="objectNeedSequence">Sequence of task objects required to complete the task.</param>
        /// <param name="buildingSettings">Settings of the building relevant to the task being registered.</param>
        /// <returns>A task that resolves to a <see cref="MovableObjectTask"/> representing the new task.</returns>
        public UniTask<MovableObjectTask> RegisterNewTask(List<StaticObjectView> from,
            ITaskObject mainTaskObject,
            List<ITaskObject> objectNeedSequence,
            BuildingSettings buildingSettings,
            bool isNeedToReturn = false,
            List<MovableObjectView> specificUnits = null);

        public bool CancelTask(MovableObjectTask task, bool interactionStarted, ResourceAmount[] inputResources);
        public bool IsTaskStarted(ITaskObject objectView);

        /// <summary>
        /// Переводит задачу на другой объект, сохраняя её место в очереди и назначенных юнитов.
        /// Уже идущим юнитам маршрут переиздаётся на новую точку.
        /// </summary>
        /// <param name="interactionStarted">Началось ли взаимодействие со старым объектом.
        /// Начатое взаимодействие не переводится.</param>
        /// <returns>True, если задача нашлась и была переведена.</returns>
        public bool TryRetargetTask(ITaskObject oldObject, ITaskObject newObject, bool interactionStarted);

        /// <summary>
        /// Replace home point in tasks where it upgraded.
        /// </summary>
        /// <param name="oldPoint">Old point to replace.</param>
        /// <param name="newPoint">New point to replace.</param>
        public void ReplaceHomePointInTasks(StaticObjectView oldPoint, StaticObjectView newPoint);

        public TaskProgress? GetTaskProgress(ITaskObject objectView);

        public void UpdateBadges();
    }
}
