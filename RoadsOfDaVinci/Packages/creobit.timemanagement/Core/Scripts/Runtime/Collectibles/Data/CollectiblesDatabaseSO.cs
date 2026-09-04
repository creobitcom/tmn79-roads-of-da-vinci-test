using System;
using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    [CreateAssetMenu(fileName = "CollectiblesDatabase", menuName = "8floor/TimeManager/Collectibles/Database")]
    public class CollectiblesDatabaseSO : ScriptableObject
    {
        [field: SerializeField] public CollectibleGroupSO[] Groups { get; private set; }

        public CollectibleGroupSO[] GetGroups(CollectibleType type)
        {
            if (Groups == null) return Array.Empty<CollectibleGroupSO>();

            var result = new List<CollectibleGroupSO>();
            for (int i = 0; i < Groups.Length; i++)
            {
                var group = Groups[i];
                if (group == null) continue;

                if (group.GetGroupType() == type)
                {
                    result.Add(group);
                }
            }
            return result.ToArray();
        }
    }
}
