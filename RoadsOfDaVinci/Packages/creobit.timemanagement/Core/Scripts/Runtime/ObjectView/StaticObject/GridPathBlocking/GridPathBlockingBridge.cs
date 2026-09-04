using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.GridPathBlocking
{
    public sealed class GridPathBlockingBridge : MonoBehaviour
    {
        [Tooltip("Indexes of the grid graphs that objects with BlocksPath make unwalkable. Empty means every grid graph.")]
        [SerializeField] private List<int> _graphIndexes = new() { 2 };

        private static GridPathBlockingBridge _active;

        public static bool IsActive => _active != null;

        public static bool BlocksGraph(uint graphIndex)
        {
            if (_active == null)
            {
                return false;
            }

            return _active._graphIndexes.Count == 0 || _active._graphIndexes.Contains((int)graphIndex);
        }

        private void Awake()
        {
            _active = this;
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
                GridAreaBlock.ClearRegistry();
            }
        }
    }
}
