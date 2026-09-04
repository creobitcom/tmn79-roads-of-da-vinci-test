using System;
using System.Diagnostics.CodeAnalysis;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController
{
    [System.Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ResourceAmount : IComparable // TODO : think about naming
    {
        [field: SerializeField] public ResourceBaseSO Resource { get; private set; }

        [field: MinValue(1)]
        [field: SerializeField] 
        public int Amount { get; set; }

        public ResourceAmount(ResourceBaseSO resource, int amount)
        {
            Resource = resource;
            Amount = amount;
        }

        public int CompareTo(object obj) 
        {
            if(obj.GetType() != typeof(ResourceAmount)) return -1;
            ResourceAmount resourceAmount = (ResourceAmount) obj;
            return Resource.name.CompareTo(resourceAmount.Resource.name);
        }
    }
}