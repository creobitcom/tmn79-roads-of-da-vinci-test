using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager
{
    public class MovableObjectTask
    {
        public readonly List<ITaskObject> TaskObject;
        public ITaskObject MainObjectView { get; private set; }
        public readonly bool IsNeedToReturn;
        public readonly List<UnitTypeCount> UnitTypeCount;

        public List<MovableObjectView> Units;
        public RuntimeConstants.Enums.TaskProgress TaskProgress;
        public int CurrentTaskObjectIndex;
        public bool IsInWork;
        public readonly HashSet<MovableObjectView> ReturnedUnits = new();
        
        public ushort TaskId { get; set; }
        public int UnitCount { get; private set; }
        public ResourceBaseSO[] SpecialResources { get; private set; }
        public List<StaticObjectView> UnitsFrom { get; set; }
        
        public ITaskObject CurrentTaskObject => TaskObject[CurrentTaskObjectIndex];
        public Vector3 Destination => CurrentTaskObject.Position;

        public MovableObjectTask(int unitCount,
            List<MovableObjectView> units,
            List<ITaskObject> taskObject, 
            ResourceBaseSO[] specialResources, 
            List<StaticObjectView> unitsFrom, 
            List<UnitTypeCount> unitTypeCount,
            RuntimeConstants.Enums.TaskProgress taskProgress = RuntimeConstants.Enums.TaskProgress.Queued,
            bool isNeedToReturn = false)
        {
            UnitCount = unitCount;
            // Never null: a queued task keeps this list empty until TryBeginTask assigns
            // units; consumers (GetTaskByUnit, StartInteract) iterate it without guards.
            Units = units ?? new List<MovableObjectView>();
            TaskProgress = taskProgress;
            TaskObject = taskObject;
            SpecialResources = specialResources;
            UnitsFrom = unitsFrom;
            MainObjectView = taskObject.Last();
            CurrentTaskObjectIndex = 0;
            UnitTypeCount = unitTypeCount;
            IsNeedToReturn = isNeedToReturn;
        }

        /// <summary>
        /// Подменяет объект задачи на другой, не трогая её место в очереди и назначенных юнитов.
        /// Нужно, когда объект заменяется в рантайме, оставаясь для игрока тем же (апгрейд стадии COC).
        /// </summary>
        public bool TryReplaceTaskObject(ITaskObject oldObject, ITaskObject newObject)
        {
            if (oldObject == null || newObject == null)
            {
                return false;
            }

            var replaced = false;

            // Все вхождения: MainObjectView обязан оставаться тем же объектом, что и TaskObject.Last(),
            // иначе юнит на последнем шаге пойдёт к подменённому объекту.
            for (var i = 0; i < TaskObject.Count; i++)
            {
                if (!ReferenceEquals(TaskObject[i], oldObject))
                {
                    continue;
                }

                TaskObject[i] = newObject;
                replaced = true;
            }

            if (!replaced)
            {
                return false;
            }

            if (ReferenceEquals(MainObjectView, oldObject))
            {
                MainObjectView = newObject;
            }

            return true;
        }
    }
}