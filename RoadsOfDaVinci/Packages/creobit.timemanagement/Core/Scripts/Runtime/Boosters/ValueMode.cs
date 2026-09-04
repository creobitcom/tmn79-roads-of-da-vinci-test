using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters
{

    [Serializable]
    public struct ValueMode {
        [field: SerializeField] public float Value { get; set; }
        [field: SerializeField] public ArithmeticModes Mode { get; set; }
    }

}