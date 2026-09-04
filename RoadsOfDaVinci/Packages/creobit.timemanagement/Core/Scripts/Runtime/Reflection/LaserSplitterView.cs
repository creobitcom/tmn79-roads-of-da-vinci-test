using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public sealed class LaserSplitterView : MonoBehaviour
    {
        [SerializeField]
        private List<LaserSplitDirection> splitDirections = new();

        public IReadOnlyList<LaserSplitDirection> SplitDirections =>
            splitDirections;
    }
}