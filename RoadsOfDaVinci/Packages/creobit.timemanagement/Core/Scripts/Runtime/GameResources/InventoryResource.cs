using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    [CreateAssetMenu(fileName = "InventoryResource", menuName = "8floor/TimeManager/GameResources/Create new inventory resource")]
    public class InventoryResource : ResourceBaseSO
    {
        public bool isMerge;
        public InventoryResource mergeWith;
        public InventoryResource mergedResource;

        /// <summary>
        /// Показывать иконку предмета над несущим его юнитом, как у обычных ресурсов.
        /// По умолчанию выключено: раньше особые предметы не показывались НИКОГДА
        /// (проверка в MovableObjectInventoryController), и проекты на merge-логике
        /// рассчитывают именно на это. Галка — опт-ин для отдельного предмета.
        /// </summary>
        [Tooltip("Показывать иконку над юнитом, пока он несёт предмет на базу.")]
        public bool showCarryIcon;
    }
}