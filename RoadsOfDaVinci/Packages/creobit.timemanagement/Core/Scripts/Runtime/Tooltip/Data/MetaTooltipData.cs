using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data
{
    [InlineEditor]
    public class MetaTooltipData : ScriptableObject, ITimeManagerSO
    {
        [field: Space(10f)]
        [field: SerializeField] public string Name { get; set; }

        [field: Space(10f)]
        [field: TextArea]
        [field: SerializeField] public string Description { get; set; }

        [field: Space(10f)]
        [field: SerializeField] public Vector3 Offset { get; set; }
    }
}