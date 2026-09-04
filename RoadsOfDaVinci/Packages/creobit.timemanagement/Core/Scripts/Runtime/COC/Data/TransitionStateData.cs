using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data
{
    [Serializable]
    public struct TransitionStateData
    {
        [field: SerializeField] 
        public ObjectView.ObjectView TransitionFrom { get; set; }

        [field: SerializeField] 
        public ObjectView.ObjectView TransitionTo { get; set; }
    }
}