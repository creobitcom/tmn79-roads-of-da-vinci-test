using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Transportable
{
    [Serializable]
    [HideLabel]
    [InlineProperty]
    public class TransportableSettings
    {
        [field: SerializeField]
        [field: DisableIf(nameof(IsTransportableSpot))]
        public bool IsTransportableItem { get; private set; }

        [field: SerializeField] 
        [field: DisableIf(nameof(IsTransportableItem))]
        public bool IsTransportableSpot { get; private set; }

        [field: SerializeField]
        [field: ShowIf(nameof(IsTransportableSpot))]
        public ResourceBaseSO[] SpecialResources { get; private set; }
    }
}