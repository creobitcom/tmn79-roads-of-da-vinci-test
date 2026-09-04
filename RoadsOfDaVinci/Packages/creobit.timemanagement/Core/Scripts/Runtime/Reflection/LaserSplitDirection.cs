using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    [Serializable]
    public sealed class LaserSplitDirection
    {
        [SerializeField]
        private float angleOffset;

        public float AngleOffset => angleOffset;
    }
}