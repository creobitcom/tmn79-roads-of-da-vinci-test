using System;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters
{
    [Serializable]
    public class GameplayIntervalSpecificParametersUlt
    {
        [field: SerializeField] 
        public UltEvent OnIntervalStarted { get; private set; }
        
        [field: SerializeField] 
        public UltEvent OnIntervalCanceled { get; private set; }
        
        [field: SerializeField] 
        public UltEvent OnIntervalCompleted { get; private set; }
    }
}