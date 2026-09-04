using System;
using System.Collections.Generic;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Minigame
{
    [Serializable]
    public struct MinigameAdditionalObjectStage
    {
        [field: SerializeField]
        public string key;
        
        [field: SerializeField]
        public UltEvent onKeySwitched;
    }
}